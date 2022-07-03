using TypeShape;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.ReflectionProvider;

IType<MyPoco> shape = ReflectionTypeShapeProvider.Default.GetShape<MyPoco>();
TypeShapeJsonSerializer<MyPoco> jsonSerializer = TypeShapeJsonSerializer.Create(shape);
PrettyPrinter<MyPoco> pp = PrettyPrinter.CreatePrinter(shape);

var value = new MyPoco(@string: "myString")
{
    List = new() { 1, 2, 3 },
    Dict = new() { ["key1"] = 42, ["key2"] = -1 },
};

string json = jsonSerializer.Serialize(value);
Console.WriteLine(json);
value = jsonSerializer.Deserialize(json)!;

Console.WriteLine(pp.PrettyPrint(value));

public class MyPoco
{
    public MyPoco(bool @bool = true, string @string = "str")
    {
        Bool = @bool;
        String = @string;
    }

    public bool Bool { get; }
    public string String { get; }
    public List<int>? List { get; set; }
    public Dictionary<string, int>? Dict { get; set; }
}