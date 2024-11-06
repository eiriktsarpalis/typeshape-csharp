using TypeShape.Abstractions;
using TypeShape.Examples.ObjectMapper;
using TypeShape.Examples.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract class ObjectMapperTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void MapToTheSameType_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        if (!providerUnderTest.HasConstructor(testCase))
        {
            return;
        }

        (Mapper<T, T> mapper, IEqualityComparer<T> comparer, ITypeShape<T> shape) = GetMapperAndEqualityComparer<T>(testCase);

        T? mappedValue = mapper(testCase.Value);

        if (!typeof(T).IsValueType && testCase.Value != null)
        {
            if (shape is IObjectTypeShape { HasConstructor: false, HasProperties: false })
            {
                // Trivial objects without ctors or properties are not copied.
                Assert.Same((object?)mappedValue, (object?)testCase.Value);
            }
            else
            {
                Assert.NotSame((object?)mappedValue, (object?)testCase.Value);
            }
        }

        if (testCase.IsStack)
        {
            mappedValue = mapper(mappedValue);
        }

        Assert.Equal(testCase.Value, mappedValue, comparer!);
    }

    [Fact]
    public void MapsToMatchingType()
    {
        Assert.Throws<InvalidOperationException>(() => GetMapper<WeatherForecast, WeatherForecastDTO>());
        Mapper<WeatherForecastDTO, WeatherForecast> mapper = GetMapper<WeatherForecastDTO, WeatherForecast>();

        var weatherForecastDTO = new WeatherForecastDTO
        {
            Id = "id",
            Date = DateTime.Parse("1975-01-01"),
            DatesAvailable = [DateTime.Parse("1975-01-01"), DateTime.Parse("1976-01-01")],
            Summary = "Summary",
            SummaryField = "SummaryField",
            TemperatureCelsius = 42,
            SummaryWords = ["Summary", "Words"],
            TemperatureRanges = new()
            {
                ["Range1"] = new() { Low = 1, High = 2 },
                ["Range2"] = new() { Low = 3, High = 4 },
            }
        };

        var weatherForecast = mapper(weatherForecastDTO);

        Assert.Equal(weatherForecastDTO.Date, weatherForecast.Date);
        Assert.Equal(weatherForecastDTO.DatesAvailable, weatherForecast.DatesAvailable!);
        Assert.Equal(weatherForecastDTO.TemperatureCelsius, weatherForecast.TemperatureCelsius);
        Assert.Equal(weatherForecastDTO.SummaryWords, weatherForecast.SummaryWords!);
        Assert.Equal(new Dictionary<string, HighLowTemps> { ["Range1"] = new() { High = 2 }, ["Range2"] = new() { High = 4 }, }, weatherForecast.TemperatureRanges!);
        Assert.Null(weatherForecast.UnmatchedProperty);
    }

    private Mapper<TFrom, TTo> GetMapper<TFrom, TTo>() 
        where TFrom : IShapeable<TFrom>
        where TTo : IShapeable<TTo> => 
        Mapper.Create(providerUnderTest.ResolveShape<TFrom>(), providerUnderTest.ResolveShape<TTo>());

    private (Mapper<T, T>, IEqualityComparer<T>, ITypeShape<T>) GetMapperAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (Mapper.Create(shape, shape), StructuralEqualityComparer.Create(shape), shape);
    }
}

public sealed class MapperTests_Reflection() : ObjectMapperTests(RefectionProviderUnderTest.Default);
public sealed class MapperTests_ReflectionEmit() : ObjectMapperTests(RefectionProviderUnderTest.NoEmit);
public sealed class MapperTests_SourceGen() : ObjectMapperTests(SourceGenProviderUnderTest.Default);