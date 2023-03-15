using TypeShape;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.RandomGenerator;
using TypeShape.Applications.StructuralEquality;
using TypeShape.ReflectionProvider;

IType<MyPoco> shape = ReflectionTypeShapeProvider.Default.GetShape<MyPoco>();

RandomGenerator<MyPoco> generator = RandomGenerator.Create(shape);
PrettyPrinter<MyPoco> printer = PrettyPrinter.Create(shape);
TypeShapeJsonSerializer<MyPoco> jsonSerializer = TypeShapeJsonSerializer.Create(shape);
IEqualityComparer<MyPoco> equalityComparer = StructuralEqualityComparer.Create(shape);

MyPoco randomValue = generator.GenerateValue(size: 64, seed: 12);
Console.WriteLine("Generated pseudo-random value:");
Console.WriteLine(printer.PrettyPrint(randomValue));

string json = jsonSerializer.Serialize(randomValue);
Console.WriteLine($"Serialized to JSON: {json}");

MyPoco? deserializedValue = jsonSerializer.Deserialize(json);
Console.WriteLine($"Deserialized value equals original: {equalityComparer.Equals(randomValue, deserializedValue)}");

public class MyPoco
{
    public MyPoco(bool @bool = true, string @string = "str")
    {
        Bool = @bool;
        String = @string;
    }

    public bool Bool { get; }
    public string String { get; }
    public int[]? Array { get; set; }
    public Dictionary<string, int>? Dict { get; set; }
}