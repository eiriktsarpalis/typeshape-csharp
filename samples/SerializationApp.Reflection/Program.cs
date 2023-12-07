using TypeShape;
using TypeShape.Applications.CborSerializer;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.StructuralEquality;
using TypeShape.Applications.XmlSerializer;
using TypeShape.ReflectionProvider;

// Use reflection to derive the shape for BindingModel and use it to fold
// serialization, pretty printing, CBOR encoding and validation programs.
ITypeShape<Todos> shape = ReflectionTypeShapeProvider.Default.GetShape<Todos>();

TypeShapeJsonSerializer<Todos> jsonSerializer = TypeShapeJsonSerializer.Create(shape);
PrettyPrinter<Todos> printer = PrettyPrinter.Create(shape);
XmlConverter<Todos> xmlConverter = XmlSerializer.CreateConverter(shape);
CborConverter<Todos> cborConverter = CborSerializer.CreateConverter(shape);
IEqualityComparer<Todos> structuralEqualityComparer = StructuralEqualityComparer.Create(shape);

DateOnly today = DateOnly.FromDateTime(DateTime.Now);
Todos originalValue = new(
    [ new (Id: 0, "Wash the dishes.", today, Status.Done),
      new (Id: 1, "Dry the dishes.", today, Status.Done),
      new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
      new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
      new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]);

Todos? value = originalValue;
Console.WriteLine($"Using values:\n{printer.Print(value)}");

string json = jsonSerializer.Serialize(value);
Console.WriteLine($"JSON encoding:\n{json}");
value = jsonSerializer.Deserialize(json);

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