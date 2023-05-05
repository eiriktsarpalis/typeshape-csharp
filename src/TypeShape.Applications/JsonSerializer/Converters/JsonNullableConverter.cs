using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeShape.Applications.JsonSerializer.Converters;

internal sealed class JsonNullableConverter<T> : JsonConverter<T?>
    where T : struct
{
    private readonly JsonConverter<T> _elementConverter;

    public JsonNullableConverter(JsonConverter<T> elementConverter)
        => _elementConverter = elementConverter;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;

        return _elementConverter.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter.Write(writer, value.Value, options);
    }
}
