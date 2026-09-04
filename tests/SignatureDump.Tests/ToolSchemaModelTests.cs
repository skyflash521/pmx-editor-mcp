using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaModelTests
    {
        [Theory]
        [InlineData(0, 200, "limitDefault")]
        [InlineData(-1, 200, "limitDefault")]
        [InlineData(50, 0, "limitMaximum")]
        public void ListingLimitsAreOneOrMore(
            int limitDefault, int limitMaximum, string parameter)
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => new ListingLimits(limitDefault, limitMaximum));

            Assert.Equal(parameter, error.ParamName);
        }

        [Fact]
        public void ListingLimitsKeepTheDefaultWithinTheMaximum()
        {
            Assert.Equal(
                "limitDefault",
                Assert.Throws<ArgumentException>(() => new ListingLimits(300, 200)).ParamName);

            ListingLimits limits = new ListingLimits(200, 200);

            Assert.Equal(200, limits.LimitDefault);
            Assert.Equal(200, limits.LimitMaximum);
        }

        [Fact]
        public void BoundsHaveAtLeastOneEnd()
        {
            Assert.Equal(
                "minimum",
                Assert.Throws<ArgumentException>(() => new ValueBounds(null, null)).ParamName);
        }

        [Fact]
        public void BoundsKeepTheMinimumWithinTheMaximum()
        {
            Assert.Equal(
                "maximum",
                Assert.Throws<ArgumentException>(() => new ValueBounds(5, 1)).ParamName);

            ValueBounds bounds = new ValueBounds(1, 1);

            Assert.Equal(1, bounds.Minimum);
            Assert.Equal(1, bounds.Maximum);
        }

        [Fact]
        public void ARequiredPartOfAToolIsNotOptional()
        {
            SchemaItem output = Item("number");

            Assert.Throws<ArgumentNullException>(
                () => new ToolSchema("t", new SchemaBranch[0], null, null, null));
            Assert.Throws<ArgumentNullException>(() => new ToolSchema("t", null, output, null, null));
            Assert.Throws<ArgumentNullException>(() => new ToolSchemaTable(null));
            Assert.Throws<ArgumentNullException>(() => new SchemaChoice(null, true));
            Assert.Throws<ArgumentNullException>(
                () => new SchemaBranch("b", null, null, null, new SchemaChoice[0]));
            Assert.Throws<ArgumentNullException>(
                () => new SchemaBranch("b", null, null, new SchemaItem[0], null));
            Assert.Throws<ArgumentNullException>(() => new SchemaPayload("t", null));
        }

        [Fact]
        public void EveryNestedItemIsListedOnceFromTheOutside()
        {
            SchemaItem inner = Item("number", "index");
            SchemaItem element = new SchemaItem(
                null, new[] { inner }, null, null, ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null, null);
            SchemaItem array = new SchemaItem(
                null, null, element, "targets", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, 2, null);

            Assert.Equal(new[] { array, element, inner }, array.WithNested);
        }

        private static SchemaItem Item(string shape, string name = null)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null, null);
        }
    }
}
