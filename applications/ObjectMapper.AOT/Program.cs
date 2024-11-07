using PolyType;
using PolyType.Examples.ObjectMapper;
using PolyType.Examples.PrettyPrinter;

var weatherForecastDto = new WeatherForecastDTO
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

Console.WriteLine($"Mapping value:\n{PrettyPrinter.Print(weatherForecastDto)}");
var weatherForecast = Mapper.MapValue<WeatherForecastDTO, WeatherForecast>(weatherForecastDto);
Console.WriteLine($"To value:\n{PrettyPrinter.Print(weatherForecast)}");

[GenerateShape]
public partial class WeatherForecastDTO
{
    public required string Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public int TemperatureCelsius { get; set; }
    public string? Summary { get; set; }
    public string? SummaryField;
    public List<DateTimeOffset>? DatesAvailable { get; set; }
    public Dictionary<string, HighLowTempsDTO>? TemperatureRanges { get; set; }
    public string[]? SummaryWords { get; set; }
}

public class HighLowTempsDTO
{
    public int High { get; set; }
    public int Low { get; set; }
}

[GenerateShape]
public partial record WeatherForecast
{
    public DateTimeOffset Date { get; init; }
    public int TemperatureCelsius { get; init; }
    public IReadOnlyList<DateTimeOffset>? DatesAvailable { get; init; }
    public IReadOnlyDictionary<string, HighLowTemps>? TemperatureRanges { get; init; }
    public IReadOnlyList<string>? SummaryWords { get; init; }
    public string? UnmatchedProperty { get; init; }
}

public record HighLowTemps
{
    public int High { get; init; }
}