using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json.Serialization;
using PolyType.Abstractions;
using PolyType.Examples.ConfigurationBinder;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class ConfigurationBinderTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void BoundResultEqualsOriginalValue<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        if (!providerUnderTest.HasConstructor(testCase))
        {
            
            Assert.Throws<NotSupportedException>(() => ConfigurationBinderTS.Create(shape));
            return;
        }

        Func<IConfiguration, T?> binder = ConfigurationBinderTS.Create(shape);
        IEqualityComparer<T> comparer = StructuralEqualityComparer.Create(shape);
        IConfiguration configuration = CreateConfiguration(testCase, shape);

        T? result = binder(configuration);

        if (testCase.Value is "")
        {
            // https://github.com/dotnet/runtime/issues/36510
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(testCase.Value, result, comparer!);
        }
    }
    
    private static IConfiguration CreateConfiguration<T>(TestCase<T> testCase, ITypeShape<T> shape)
    {
        JsonConverter<T> converter = JsonSerializerTS.CreateConverter(shape);
        string json = converter.Serialize(testCase.Value);
        if (testCase.IsStack)
        {
            T? value = converter.Deserialize(json.AsSpan());
            json = converter.Serialize(value);
        }
        
        string rootJson = $$"""{ "Root" : {{json}} }""";
        var builder = new ConfigurationBuilder();
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(rootJson));
        builder.AddJsonStream(stream);
        return builder.Build().GetSection("Root");
    }
}

public sealed class ConfigurationBinderTests_Reflection() : ConfigurationBinderTests(RefectionProviderUnderTest.NoEmit);
public sealed class ConfigurationBinderTests_ReflectionEmit() : ConfigurationBinderTests(RefectionProviderUnderTest.Emit);
public sealed class ConfigurationBinderTests_SourceGen() : ConfigurationBinderTests(SourceGenProviderUnderTest.Default);