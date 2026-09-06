using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class NonEmptyArrayRuleTests
    {
        [Theory]
        [InlineData("indices")]
        [InlineData("handles")]
        [InlineData("parentIndices")]
        [InlineData("parentHandles")]
        [InlineData("refIndices")]
        [InlineData("targets")]
        [InlineData("assignments")]
        public void AnArrayThatDecidesWhatTheCallActsOnCannotBeEmpty(string name)
        {
            Assert.True(NonEmptyArrayRule.NonEmpty(Array(name)));
        }

        [Theory]
        [InlineData("argsList")]
        [InlineData("values")]
        [InlineData("Handles")]
        public void AnyOtherArrayCanBeEmpty(string name)
        {
            Assert.False(NonEmptyArrayRule.NonEmpty(Array(name)));
        }

        [Fact]
        public void AnItemThatIsNotAnArrayIsNotAnArrayThatCannotBeEmpty()
        {
            Assert.False(NonEmptyArrayRule.NonEmpty(Value("handles")));
        }

        [Fact]
        public void TheArgumentIsChecked()
        {
            Assert.Throws<ArgumentNullException>(() => NonEmptyArrayRule.NonEmpty(null));
        }

        private static SchemaItem Array(string name)
        {
            return new SchemaItem(
                null, null, Value(null), name, ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Value(string name)
        {
            return new SchemaItem(
                "number", null, null, name, ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
        }
    }
}
