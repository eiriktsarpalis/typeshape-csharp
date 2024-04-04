using TypeShape;
using TypeShape.Applications.CborSerializer;
using TypeShape.Applications.JsonSchema;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.StructuralEquality;
using TypeShape.Applications.XmlSerializer;

DateOnly today = DateOnly.FromDateTime(DateTime.Now);
Todos originalValue = new(
    [ new (Id: 0, "Wash the dishes.", today, Status.Done),
      new (Id: 1, "Dry the dishes.", today, Status.Done),
      new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
      new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
      new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]);

Todos? value = originalValue;
Console.WriteLine($"Using values:\n{PrettyPrinter.Print(value)}");

string json = TypeShapeJsonSerializer.Serialize(value);
Console.WriteLine($"JSON encoding:\n{json}");
value = TypeShapeJsonSerializer.Deserialize<Todos>(json);

var schema = JsonSchemaGenerator.Generate<Todos>();
Console.WriteLine($"JSON schema:\n{schema.ToJsonString()}");

string xml = XmlSerializer.Serialize(value);
Console.WriteLine($"XML encoding:\n{xml}");
value = XmlSerializer.Deserialize<Todos>(xml);

string cborHex = CborSerializer.EncodeToHex(value);
Console.WriteLine($"CBOR encoding:\n{cborHex}");
value = CborSerializer.DecodeFromHex<Todos>(cborHex);

bool areEqual = StructuralEqualityComparer.Equals(originalValue, value);
Console.WriteLine($"Result equals the original value: {areEqual}");

[GenerateShape]
public partial record Todos(Todo[] Items);

public record Todo(int Id, string? Title, DateOnly? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }