using TypeShape;
using TypeShape.Applications.CborSerializer;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.Validation;

string json = """
{
    "Id" : null,
    "Components" : ["1"],
    "Sample" : 1.15,
    "PhoneNumber" : "NaN"
}
""";

BindingModel? model = TypeShapeJsonSerializer.Deserialize<BindingModel>(json);

Console.WriteLine("Deserialized value:");
Console.WriteLine(PrettyPrinter.Print(model));
Console.WriteLine();

Console.WriteLine($"CBOR encoding: {CborSerializer.EncodeToHex(model)}");

Validator.Validate(model);

[GenerateShape]
public partial class BindingModel
{
    [Required]
    public string? Id { get; set; }

    [Length(Min = 2, Max = 5)]
    public List<string>? Components { get; set; }

    [Range<double>(Min = 0, Max = 1)]
    public double Sample { get; set; }

    [RegularExpression(Pattern = @"^\+?[0-9]{7,14}$")]
    public string? PhoneNumber { get; set; }
}