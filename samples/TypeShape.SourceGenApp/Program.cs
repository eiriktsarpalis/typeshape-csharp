using TypeShape;
using TypeShape.Applications.CborSerializer;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.RandomGenerator;
using TypeShape.Applications.StructuralEquality;
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

Console.WriteLine($"Deserialized value: {PrettyPrinter.Print(model)}");
Console.WriteLine($"CBOR encoding: {CborSerializer.EncodeToHex(model)}");

BindingModel randomValue = RandomGenerator.GenerateValue<BindingModel>(size: 32, seed: 42);

Console.WriteLine($"Generated random value: {PrettyPrinter.Print(randomValue)}");
Console.WriteLine($"Equals random value: {StructuralEqualityComparer.Equals(model, randomValue)}");
Console.WriteLine($"Equals itself: {StructuralEqualityComparer.Equals(model, model)}");

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