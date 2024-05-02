using System.Text.Json;
using TypeShape.Applications.JsonSerializer;
using Xunit;

namespace TypeShape.Tests;

public class AdditionalMarkerAttributeTests
{

    [Fact]
    public void HappyCase()
    {
        var value = new AdditionalMarkerData {Value = "Hello World"};
        var expectedResult = JsonSerializer.Serialize(value);
        var actualResult = TypeShapeJsonSerializer.Serialize(value);
        Assert.Equal(expectedResult, actualResult);
    }
}

[AdditionalMarker]
public partial class AdditionalMarkerData
{
    public string Value { get; set; } = default!;
}

public sealed class AdditionalMarkerAttribute : Attribute;