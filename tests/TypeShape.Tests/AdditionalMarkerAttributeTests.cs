using System;
using TypeShape.Applications.JsonSerializer;
using Xunit;

namespace TypeShape.Tests
{
    public class AdditionalMarkerAttributeTests
    {

        [Fact]
        public void HappyCase()
        {
           var provider = SourceGenProvider.Default;
           var value = new AdditionalMarkerData {Value = "Hello World"};
           string expectedResult = TypeShapeJsonSerializer.Serialize(value);
        }
    }

    [AdditionalMarker]
    public partial class AdditionalMarkerData
    {
        public string Value { get; set; } = default!;
    }

    public sealed class AdditionalMarkerAttribute : Attribute
    {

    }
}
