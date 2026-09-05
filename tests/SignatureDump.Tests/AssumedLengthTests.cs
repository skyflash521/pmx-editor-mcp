using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class AssumedLengthTests
    {
        /// <summary>題材の表。実物の値は仕様書が持つので、ここでは読み方だけを見る。</summary>
        private static readonly AssumedLength Lengths = new AssumedLength(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "boolean", 5 },
                { "text", 256 },
            });

        [Theory]
        [InlineData("boolean", 5)]
        [InlineData("text", 256)]
        public void EachSpellingTakesTheLengthTheTableWrites(string shape, int expected)
        {
            Assert.Equal(expected, Lengths.Of(Scalar(shape)));
        }

        [Theory]
        [InlineData(1000.0, 4)]
        [InlineData(1.0, 1)]
        [InlineData(0.5, 3)]
        [InlineData(-1.0, 2)]
        [InlineData(1e30, 5)]
        public void AnUpperBoundIsTakenAsTheLengthOfItsWritingInJson(double maximum, int expected)
        {
            Assert.Equal(expected, Lengths.Of(Bounded(maximum)));
        }

        [Fact]
        public void AGroupIsTheSumOfItsMembersAndTheirNames()
        {
            SchemaItem group = Group(Scalar("boolean", "a"), Scalar("boolean", "b"));

            Assert.Equal((5 + 8) * 2, Lengths.Of(group));
        }

        [Fact]
        public void AnArrayIsItsElementTimesEightWhenNoSourceFixesTheCount()
        {
            Assert.Equal(5 * 8, Lengths.Of(Array(Scalar("boolean"), null)));
        }

        [Fact]
        public void AnArrayTakesTheCountThePrimarySourceFixes()
        {
            Assert.Equal(5 * 3, Lengths.Of(Array(Scalar("boolean"), 3)));
        }

        [Fact]
        public void AnItemWithoutAKnownSpellingStops()
        {
            Assert.Throws<InvalidOperationException>(() => Lengths.Of(Scalar("date")));
        }

        [Fact]
        public void TheItemAndTheTableAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => Lengths.Of(null));
            Assert.Throws<ArgumentNullException>(() => new AssumedLength(null));
        }

        private static SchemaItem Scalar(string shape, string name = null)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null, null);
        }

        private static SchemaItem Bounded(double maximum)
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.SdkReturn, null, null, false,
                new ValueBounds(null, maximum), null, "配布文書の該当節", false, null, null);
        }

        private static SchemaItem Group(params SchemaItem[] members)
        {
            return new SchemaItem(
                null, members, null, null, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null, null);
        }

        /// <summary>要素数を一次資料が定めた並びは、転記元を伴う上限を持つ。</summary>
        private static SchemaItem Array(SchemaItem element, int? fixedCount)
        {
            return new SchemaItem(
                null, null, element, null, ItemOrigin.SdkReturn, null, null, false,
                null, null, fixedCount.HasValue ? "配布文書の該当節" : null, false,
                fixedCount ?? 100, null);
        }
    }
}
