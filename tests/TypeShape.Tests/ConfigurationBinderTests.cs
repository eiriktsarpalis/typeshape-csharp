using System.Text;
using Microsoft.Extensions.Configuration;
using TypeShape.Abstractions;
using TypeShape.Examples.ConfigurationBinder;
using TypeShape.Examples.JsonSerializer;
using TypeShape.Examples.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract class ConfigurationBinderTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void BoundResultEqualsOriginalValue<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = testCase.GetShape(providerUnderTest);
        if (!testCase.HasConstructors(providerUnderTest))
        {
            
            Assert.Throws<NotSupportedException>(() => TypeShapeConfigurationBinder.Create(shape));
            return;
        }

        Func<IConfiguration, T?> binder = TypeShapeConfigurationBinder.Create(shape);
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
        TypeShapeJsonConverter<T> converter = TypeShapeJsonSerializer.CreateConverter(shape);
        string json = converter.Serialize(testCase.Value);
        if (testCase.IsStack)
        {
            T? value = converter.Deserialize(json);
            json = converter.Serialize(value);
        }
        
        string rootJson = $$"""{ "Root" : {{json}} }""";
        var builder = new ConfigurationBuilder();
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(rootJson));
        builder.AddJsonStream(stream);
        return builder.Build().GetSection("Root");
    }
}

public sealed class ConfigurationBinderTests_Reflection() : ConfigurationBinderTests(RefectionProviderUnderTest.Default);
public sealed class ConfigurationBinderTests_ReflectionEmit() : ConfigurationBinderTests(RefectionProviderUnderTest.NoEmit);
public sealed class ConfigurationBinderTests_SourceGen() : ConfigurationBinderTests(SourceGenProviderUnderTest.Default);