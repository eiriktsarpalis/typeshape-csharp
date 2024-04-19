using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using TypeShape.Abstractions;

namespace TypeShape.Applications.JsonSchema;

/// <summary>
/// A JSON schema generator for .NET types inspired by https://github.com/eiriktsarpalis/typeshape-csharp
/// </summary>
public static class JsonSchemaGenerator
{
    public static JsonObject Generate<T>(ITypeShapeProvider provider)
        => Generate(provider.Resolve<T>());

    public static JsonObject Generate(ITypeShape typeShape)
        => new Generator().GenerateSchema(typeShape);

    public static JsonObject Generate<T, TProvider>() where TProvider : ITypeShapeProvider<T>
        => Generate(TProvider.GetShape());

    public static JsonObject Generate<T>() where T : ITypeShapeProvider<T>
        => Generate(T.GetShape());

    private sealed class Generator
    {
        private readonly Dictionary<(Type, bool AllowNull), string> _locations = new();
        private readonly List<string> _path = new();

        public JsonObject GenerateSchema(ITypeShape typeShape, bool allowNull = true, bool cacheLocation = true)
        {
            allowNull = allowNull && IsNullableType(typeShape.Type);

            if (s_simpleTypeInfo.TryGetValue(typeShape.Type, out SimpleTypeJsonSchema simpleType))
            {
                return ApplyNullability(simpleType.ToSchemaDocument(), allowNull);
            }

            if (cacheLocation)
            {
                ref string? location = ref CollectionsMarshal.GetValueRefOrAddDefault(_locations, (typeShape.Type, allowNull), out bool exists);
                if (exists)
                {
                    return new JsonObject
                    {
                        ["$ref"] = (JsonNode)location!,
                    };
                }
                else
                {
                    location = _path.Count == 0 ? "#" : $"#/{string.Join("/", _path)}";
                }
            }

            JsonObject schema;
            switch (typeShape)
            {
                case IEnumTypeShape enumShape:
                    schema = new JsonObject { ["type"] = "string" };
                    if (enumShape.Type.GetCustomAttribute<FlagsAttribute>() is null)
                    {
                        schema["enum"] = CreateArray(Enum.GetNames(enumShape.Type).Select(name => (JsonNode)name));
                    }

                    break;

                case INullableTypeShape nullableShape:
                    schema = GenerateSchema(nullableShape.ElementType, cacheLocation: false);
                    break;

                case IEnumerableTypeShape enumerableTypeShape:
                    for (int i = 0; i < enumerableTypeShape.Rank; i++)
                    {
                        Push("items");
                    }

                    schema = GenerateSchema(enumerableTypeShape.ElementType);

                    for (int i = 0; i < enumerableTypeShape.Rank; i++)
                    {
                        schema = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = schema,
                        };

                        Pop();
                    }

                    break;

                case IDictionaryTypeShape dictionaryTypeShape:
                    Push("additionalProperties");
                    JsonObject additionalPropertiesSchema = GenerateSchema(dictionaryTypeShape.ValueType);
                    Pop();

                    schema = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = additionalPropertiesSchema,
                    };

                    break;

                default:
                    schema = new();

                    if (typeShape.HasProperties)
                    {
                        IConstructorShape? ctor = typeShape.GetConstructors()
                            .MaxBy(c => c.ParameterCount);

                        Dictionary<string, IConstructorParameterShape>? ctorParams = ctor?.GetParameters()
                            .Where(p => p.Kind is ConstructorParameterKind.ConstructorParameter || p.IsRequired)
                            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                        JsonObject properties = new();
                        JsonArray? required = null;

                        Push("properties");
                        foreach (IPropertyShape prop in typeShape.GetProperties())
                        {
                            IConstructorParameterShape? associatedParameter = null;
                            ctorParams?.TryGetValue(prop.Name, out associatedParameter);

                            bool isNonNullable = 
                                (!prop.HasGetter || prop.IsGetterNonNullable) &&
                                (!prop.HasSetter || prop.IsSetterNonNullable) &&
                                (associatedParameter is null || associatedParameter.IsNonNullable);

                            bool cachePropertyLocation = !isNonNullable || !IsNullableType(prop.PropertyType.Type);

                            Push(prop.Name);
                            JsonObject propSchema = GenerateSchema(prop.PropertyType, allowNull: !isNonNullable, cachePropertyLocation);
                            Pop();

                            properties.Add(prop.Name, propSchema);
                            if (associatedParameter?.IsRequired is true)
                            {
                                (required ??= new()).Add((JsonNode)prop.Name);
                            }
                        }
                        Pop();

                        schema["type"] = "object";
                        schema["properties"] = properties;
                        if (required != null)
                        {
                            schema["required"] = required;
                        }
                    }

                    break;
            }

            return ApplyNullability(schema, allowNull);
        }

        private void Push(string name)
        {
            _path.Add(name);
        }

        private void Pop()
        {
            _path.RemoveAt(_path.Count - 1);
        }

        private static JsonObject ApplyNullability(JsonObject schema, bool allowNull)
        {
            if (allowNull && schema.TryGetPropertyValue("type", out JsonNode? typeValue))
            {
                if (schema["type"] is JsonArray types)
                {
                    types.Add((JsonNode)"null");
                }
                else
                {
                    schema["type"] = new JsonArray { (JsonNode)(string)typeValue!, (JsonNode)"null" };
                }
            }

            return schema;
        }

        private static JsonArray CreateArray(IEnumerable<JsonNode> elements)
        {
            var arr = new JsonArray();
            foreach (JsonNode elem in elements)
            {
                arr.Add(elem);
            }

            return arr;
        }

        private static bool IsNullableType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private readonly struct SimpleTypeJsonSchema
        {
            public SimpleTypeJsonSchema(string? type, string? format = null, string? pattern = null)
            {
                Type = type;
                Format = format;
                Pattern = pattern;
            }

            public string? Type { get; }
            public string? Format { get; }
            public string? Pattern { get; }

            public JsonObject ToSchemaDocument()
            {
                var schema = new JsonObject();
                if (Type != null)
                {
                    schema["type"] = Type;
                }

                if (Format != null)
                {
                    schema["format"] = Format;
                }

                if (Pattern != null)
                {
                    schema["pattern"] = Pattern;
                }

                return schema;
            }
        }

        private static readonly Dictionary<Type, SimpleTypeJsonSchema> s_simpleTypeInfo = new()
        {
            [typeof(object)] = new(),
            [typeof(bool)] = new("boolean"),
            [typeof(byte)] = new("integer"),
            [typeof(ushort)] = new("integer"),
            [typeof(uint)] = new("integer"),
            [typeof(ulong)] = new("integer"),
            [typeof(sbyte)] = new("integer"),
            [typeof(short)] = new("integer"),
            [typeof(int)] = new("integer"),
            [typeof(long)] = new("integer"),
            [typeof(float)] = new("number"),
            [typeof(double)] = new("number"),
            [typeof(decimal)] = new("number"),
            [typeof(Half)] = new("number"),
            [typeof(UInt128)] = new("integer"),
            [typeof(Int128)] = new("integer"),
            [typeof(char)] = new("string"),
            [typeof(string)] = new("string"),
            [typeof(byte[])] = new("string"),
            [typeof(Memory<byte>)] = new("string"),
            [typeof(ReadOnlyMemory<byte>)] = new("string"),
            [typeof(DateTime)] = new("string", format: "date-time"),
            [typeof(DateTimeOffset)] = new("string", format: "date-time"),
            [typeof(TimeSpan)] = new("string", pattern: @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"),
            [typeof(DateOnly)] = new("string", format: "date"),
            [typeof(TimeOnly)] = new("string", format: "time"),
            [typeof(Guid)] = new("string", format: "uuid"),
            [typeof(Uri)] = new("string", format: "uri"),
            [typeof(Version)] = new("string"),
            [typeof(JsonDocument)] = new(),
            [typeof(JsonElement)] = new(),
            [typeof(JsonNode)] = new(),
            [typeof(JsonValue)] = new(),
            [typeof(JsonObject)] = new("object"),
            [typeof(JsonArray)] = new("array"),
        };
    }
}
