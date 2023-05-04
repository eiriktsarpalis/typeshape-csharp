namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;

internal abstract class JsonConstructor<TDeclaringType>
{
    public abstract TDeclaringType ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options);
}

internal sealed class JsonDefaultConstructor<TDeclaringType> : JsonConstructor<TDeclaringType>
{
    private readonly Func<TDeclaringType> _defaultConstructor;

    public JsonDefaultConstructor(Func<TDeclaringType> defaultConstructor)
        => _defaultConstructor = defaultConstructor;

    public override TDeclaringType ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        => _defaultConstructor();
}

internal sealed class JsonParameterizedConstructor<TDeclaringType, TArgumentState> : JsonConstructor<TDeclaringType>
{
    private readonly Func<TArgumentState> _createArgumentState;
    private readonly Dictionary<string, JsonProperty<TArgumentState>> _constructorParameters;
    private readonly Func<TArgumentState, TDeclaringType> _createObject;

    public JsonParameterizedConstructor(IConstructorShape<TDeclaringType, TArgumentState> constructor, JsonProperty<TArgumentState>[] constructorParameters)
    {
        Debug.Assert(constructorParameters.Length > 0);
        _createArgumentState = constructor.GetArgumentStateConstructor();
        _constructorParameters = constructorParameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _createObject = constructor.GetParameterizedConstructor();
    }

    public override TDeclaringType ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        Debug.Assert(reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.EndObject);

        Dictionary<string, JsonProperty<TArgumentState>> ctorParams = _constructorParameters;
        TArgumentState argumentState = _createArgumentState();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            string propertyName = reader.GetString()!;

            if (!ctorParams.TryGetValue(propertyName, out JsonProperty<TArgumentState>? jsonProperty))
            {
                // stop reading constructor arguments on the first unrecognized parameter
                break;
            }

            reader.EnsureRead();
            jsonProperty.Deserialize(ref reader, ref argumentState, options);
            reader.EnsureRead();
        }

        return _createObject(argumentState);
    }
}