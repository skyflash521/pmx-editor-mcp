using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>一列に並ぶ並びの印を外して、その先の型を取る。</summary>
    public sealed class ValueTypeNameTests
    {
        [Theory]
        [InlineData("System.Int32[]", "System.Int32")]
        [InlineData("System.Collections.Generic.IList<Sdk.Item>", "Sdk.Item")]
        public void AnArrayOrAListGivesItsElement(string typeName, string expected)
        {
            string element;

            Assert.True(ValueTypeName.TryElement(typeName, out element));
            Assert.Equal(expected, element);
        }

        [Theory]
        [InlineData("System.Int32")]
        [InlineData("System.Nullable<System.Int32>")]
        [InlineData("System.Collections.Generic.IDictionary<System.String,System.Int32>")]
        public void ATypeThatIsNotASequenceGivesNothing(string typeName)
        {
            string element;

            Assert.False(ValueTypeName.TryElement(typeName, out element));
            Assert.Null(element);
        }

        [Fact]
        public void TheContainedTypeStripsEveryMarkInTurn()
        {
            Assert.Equal(
                "Sdk.Item",
                ValueTypeName.Contained("System.Collections.Generic.IList<Sdk.Item[]>"));
        }

        [Fact]
        public void ATypeWithoutAMarkIsItsOwnContainedType()
        {
            Assert.Equal("Sdk.Item", ValueTypeName.Contained("Sdk.Item"));
        }

        [Fact]
        public void ANameThatIsNotGivenStops()
        {
            string element;

            Assert.Throws<ArgumentNullException>(() => ValueTypeName.TryElement(null, out element));
        }
    }
}
