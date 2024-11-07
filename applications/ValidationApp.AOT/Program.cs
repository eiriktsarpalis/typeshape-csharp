using PolyType;
using PolyType.Examples.Validation;

var validInstance = new BindingModel
{
    Id = "1",
    Components = ["1", "2", "3"],
    Sample = 0.5,
    PhoneNumber = "+1234567890",
};

Validator.Validate(validInstance);
Console.WriteLine($"Success: {nameof(validInstance)}");

var invalidInstance = new BindingModel
{
    Id = null,
    Components = ["1"],
    Sample = 1.15,
    PhoneNumber = "NaN",
};

Validator.Validate(invalidInstance);
Console.WriteLine($"Success: {nameof(invalidInstance)}");

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