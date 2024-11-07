using System.Text.Json.Serialization;
using PolyType;
using PolyType.Examples.CborSerializer;
using PolyType.Examples.JsonSchema;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.PrettyPrinter;
using PolyType.Examples.StructuralEquality;
using PolyType.Examples.XmlSerializer;
using PolyType.ReflectionProvider;

// Use reflection to derive the shape for BindingModel and use it to fold
// serialization, pretty printing, CBOR encoding and validation programs.
ITypeShapeProvider shapeProvider = ReflectionTypeShapeProvider.Default;

PrettyPrinter<Todos> printer = PrettyPrinter.Create<Todos>(shapeProvider);
JsonConverter<Todos> jsonConverter = JsonSerializerTS.CreateConverter<Todos>(shapeProvider);
XmlConverter<Todos> xmlConverter = XmlSerializer.CreateConverter<Todos>(shapeProvider);
CborConverter<Todos> cborConverter = CborSerializer.CreateConverter<Todos>(shapeProvider);
IEqualityComparer<Todos> structuralEqualityComparer = StructuralEqualityComparer.Create<Todos>(shapeProvider);

DateOnly today = DateOnly.FromDateTime(DateTime.Now);
Todos originalValue = new(
    [ new (Id: 0, "Wash the dishes.", today, Status.Done),
      new (Id: 1, "Dry the dishes.", today, Status.Done),
      new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
      new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
      new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]);

Todos? value = originalValue;
Console.WriteLine($"Using values:\n{printer.Print(value)}");

string json = jsonConverter.Serialize(value, options: new() { Indented = true });
Console.WriteLine($"JSON encoding:\n{json}");
value = jsonConverter.Deserialize(json);

var jsonSchema = JsonSchemaGenerator.Generate<Todos>(shapeProvider);
Console.WriteLine($"JSON schema:\n{jsonSchema.ToJsonString()}");

string xml = xmlConverter.Serialize(value);
Console.WriteLine($"XML encoding:\n{xml}");
value = xmlConverter.Deserialize(xml);

string cborHex = cborConverter.EncodeToHex(value);
Console.WriteLine($"CBOR encoding:\n{cborHex}");
value = cborConverter.DecodeFromHex(cborHex);

bool areEqual = structuralEqualityComparer.Equals(originalValue, value);
Console.WriteLine($"Result equals the original value: {areEqual}");

public record Todos(Todo[] Items);

public record Todo(int Id, string? Title, DateOnly? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }