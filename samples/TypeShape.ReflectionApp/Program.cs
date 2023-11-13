using TypeShape;
using TypeShape.Applications.CborSerializer;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.RandomGenerator;
using TypeShape.Applications.StructuralEquality;
using TypeShape.Applications.Validation;
using TypeShape.ReflectionProvider;

// Use reflection to derive the shape for BindingModel and use it to derive
// serialization, pretty printing, CBOR encoding and validation programs.
ITypeShape<BindingModel> shape = ReflectionTypeShapeProvider.Default.GetShape<BindingModel>();
TypeShapeJsonSerializer<BindingModel> jsonSerializer = TypeShapeJsonSerializer.Create(shape);
PrettyPrinter<BindingModel> printer = PrettyPrinter.Create(shape);
CborConverter<BindingModel> cborConverter = CborSerializer.CreateConverter(shape);
RandomGenerator<BindingModel> randomGenerator = RandomGenerator.Create(shape);
IEqualityComparer<BindingModel> structuralEqualityComparer = StructuralEqualityComparer.Create(shape);
Validator<BindingModel> validator = Validator.Create(shape);

string json = """
{
    "Id" : null,
    "Components" : ["1"],
    "Sample" : 1.15,
    "PhoneNumber" : "NaN"
}
""";

BindingModel? model = jsonSerializer.Deserialize(json);

Console.WriteLine($"Deserialized value: {printer.Print(model)}");
Console.WriteLine($"CBOR encoding: {cborConverter.EncodeToHex(model)}");

BindingModel randomValue = randomGenerator.GenerateValue(size: 32, seed: 42);

Console.WriteLine($"Generated random value: {printer.Print(randomValue)}");
Console.WriteLine($"Equals random value: {structuralEqualityComparer.Equals(model, randomValue)}");
Console.WriteLine($"Equals itself: {structuralEqualityComparer.Equals(model, model)}");

validator.Validate(model);

public class BindingModel
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