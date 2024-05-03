using TypeShape.Applications.RandomGenerator;
using Xunit;

namespace TypeShape.Tests
{
    public partial class BugTests
    {
        [Fact]
        public void RandomGenerator_Array_ChildSize()
        {
            var poco = RandomGenerator.GenerateValue<FirstPoco>(10, 42);
            Assert.NotNull(poco);
        }

        [GenerateShape]
        public abstract partial class BasePoco
        {
            public long Id { get; set; }
            public FirstPoco First { get; set; } = default!;
            public FirstPoco[] FirstArray { get; set; } = default!;
        }

        [GenerateShape]
        public sealed partial class FirstPoco : BasePoco;
    }
}
