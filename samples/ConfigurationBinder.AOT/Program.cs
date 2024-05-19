using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using TypeShape;
using TypeShape.Applications.ConfigurationBinder;
using TypeShape.Applications.PrettyPrinter;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

ApplicationSettings? settings = TypeShapeConfigurationBinder.Get<ApplicationSettings>(configuration);
Console.WriteLine(PrettyPrinter.Print(settings));

[GenerateShape]
public partial class ApplicationSettings
{
    public required string ApplicationName { get; init; }
    public required int Version { get; init; }
    public required DatabaseSettings Database { get; init; }
    public required ImmutableList<FeatureToggle> Features { get; init; }
    public required ImmutableDictionary<string, ApiEndpoint> ApiEndpoints { get; set; }
}

public class DatabaseSettings
{
    public required string ConnectionString { get; init; }
    public required string Provider { get; init; }
}

public class FeatureToggle
{
    public required string Name { get; init; }
    public required bool IsEnabled { get; init; }
}

public class ApiEndpoint
{
    public required string Url { get; init; }
    public required string Key { get; init; }
}