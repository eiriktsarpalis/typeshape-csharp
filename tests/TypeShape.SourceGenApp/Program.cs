using TypeShape;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.Applications.Validation;

IType<BindingModel> shape = SourceGenTypeShapeProvider.Default.BindingModel;

TypeShapeJsonSerializer<BindingModel> jsonSerializer = TypeShapeJsonSerializer.Create(shape);
Validator<BindingModel> validator = Validator.Create(shape);
PrettyPrinter<BindingModel> printer = PrettyPrinter.Create(shape);

string json = """
    {
        "Id" : null,
        "Components" : ["1"],
        "Sample" : 1.15,
        "PhoneNumber" : "NaN"
    }
    """;

BindingModel? model = jsonSerializer.Deserialize(json);

Console.WriteLine("Deserialized value:");
Console.WriteLine(printer.PrettyPrint(model));
Console.WriteLine();

if (validator.TryValidate(model, out List<string>? errors))
{
    Console.WriteLine("No validation errors found");
}
else
{
    Console.WriteLine("Found validation errors: ");
    foreach (string error in errors)
    {
        Console.WriteLine(error);
    }
    Console.WriteLine();
}

public record BindingModel
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

[GenerateShape(typeof(BindingModel))]
public partial class SourceGenTypeShapeProvider
{
}