using Json.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TypeShape.Applications.JsonSchema;
using TypeShape.Applications.JsonSerializer;
using TypeShape.ReflectionProvider;
using Xunit;
using Xunit.Sdk;

namespace TypeShape.Tests;

public abstract class JsonSchemaTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GeneratesExpectedSchema(ITestCase testCase)
    {
        ITypeShape? shape = Provider.GetShape(testCase.Type);
        Assert.NotNull(shape);

        JsonObject schema = JsonSchemaGenerator.Generate(shape);

        switch (shape)
        {
            case IEnumTypeShape enumShape:
                AssertType("string");
                if (enumShape.Type.GetCustomAttribute<FlagsAttribute>() != null)
                {
                    Assert.DoesNotContain("enum", schema);
                }
                else
                {
                    Assert.Equal(Enum.GetNames(enumShape.Type), schema["enum"]!.AsArray().Select(node => (string)node!));
                }
                break;

            case INullableTypeShape nullableShape:
                JsonObject nullableElementSchema = JsonSchemaGenerator.Generate(nullableShape.ElementType);
                schema.Remove("type");
                nullableElementSchema.Remove("type");
                Assert.True(JsonNode.DeepEquals(nullableElementSchema, schema));
                break;

            case IEnumerableTypeShape enumerableShape:
                if (enumerableShape.Type == typeof(byte[]))
                {
                    AssertType("string");
                    break;
                }

                AssertType("array");
                JsonObject elementSchema = JsonSchemaGenerator.Generate(enumerableShape.ElementType);
                for (int i = 0; i < enumerableShape.Rank; i++) schema = (JsonObject)schema["items"]!;
                Assert.True(JsonNode.DeepEquals(elementSchema, schema));
                break;

            case IDictionaryTypeShape dictionaryTypeShape:
                AssertType("object");
                JsonObject valueSchema = JsonSchemaGenerator.Generate(dictionaryTypeShape.ValueType);
                Assert.True(JsonNode.DeepEquals(valueSchema, schema["additionalProperties"]));
                break;

            default:
                if (shape.HasProperties)
                {
                    AssertType("object");
                    Assert.Contains("properties", schema);
                }
                else
                {
                    Assert.DoesNotContain("properties", schema);
                    Assert.DoesNotContain("required", schema);
                }
                break;
        }

        void AssertType(string type)
        {
            JsonNode? typeValue = Assert.Contains("type", schema);
            if (!shape.Type.IsValueType || Nullable.GetUnderlyingType(shape.Type) != null)
            {
                Assert.Equal([type, "null"], ((JsonArray)typeValue!).Select(x => (string)x!));
            }
            else
            {
                Assert.Equal(type, (string)typeValue!);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void SchemaMatchesJsonSerializer<T>(TestCase<T> testCase)
    {
        if (typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128))
        {
            return; // Not supported by JsonSchema.NET
        }

        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        JsonObject schema = JsonSchemaGenerator.Generate(shape);
        string json = TypeShapeJsonSerializer.CreateConverter(shape).Serialize(testCase.Value);

        JsonSchema jsonSchema = JsonSerializer.Deserialize<JsonSchema>(schema)!;
        EvaluationOptions options = new() { OutputFormat = OutputFormat.List };
        EvaluationResults results = jsonSchema.Evaluate(JsonNode.Parse(json), options);
        if (!results.IsValid)
        {
            IEnumerable<string> errors = results.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(error => $"Path:${d.InstanceLocation} {error.Key}:{error.Value}"));

            throw new XunitException($"""
                Instance JSON document does not match the specified schema.
                Schema:
                {JsonSerializer.Serialize(schema)}
                Instance:
                {json}
                Errors:
                {string.Join(Environment.NewLine, errors)}
                """);
        }
    }
}

public sealed class JsonSchemaTests_Reflection : JsonSchemaTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class JsonSchemaTests_ReflectionEmit : JsonSchemaTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class JsonSchemaTests_SourceGen : JsonSchemaTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}
