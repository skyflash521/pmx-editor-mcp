using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ListingLimitRuleTests
    {
        /// <summary>一覧を返す題材。要素の項目を差し替えられる。</summary>
        private static ToolSchema Listing(params SchemaItem[] members)
        {
            return Tool(Group(null, Value("total", "number"), Items(Group(null, members))));
        }

        /// <summary>切り出した並びを持つ項目。</summary>
        private static SchemaItem Items(SchemaItem element)
        {
            return new SchemaItem(
                null, null, element, "items", ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Group(string name, params SchemaItem[] members)
        {
            return new SchemaItem(
                null, members, null, name, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }

        /// <summary>ホストが載せる項目。選び方の外に置かれる。</summary>
        private static SchemaItem Value(string name, string shape)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }

        /// <summary>SDKに由来する項目。出所を持たず、選べる項目になる。</summary>
        private static SchemaItem Chosen(string name, string shape)
        {
            return new SchemaItem(
                shape, null, null, name, null, null, null, false,
                null, null, null, false, null);
        }

        private static ToolSchema Tool(SchemaItem output)
        {
            return new ToolSchema(
                "model_list_vertices",
                new[] { new SchemaBranch("only", null, null, new SchemaItem[0], new SchemaChoice[0]) },
                output,
                null);
        }

        /// <summary>題材の表。実物の値は共通契約の正本が持つ。</summary>
        private static readonly AssumedLength Lengths = new AssumedLength(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "number", 11 },
                { "text", 256 },
                { "boolean", 5 },
            });

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
        public void TheMaximumTakesTheSmallestChosenItemAndTheDefaultTakesThemAll()
        {
            ToolSchema schema = Listing(
                Value("index", "number"), Chosen("name", "text"), Chosen("flag", "boolean"));

            ListingLimits limits = ListingLimitRule.Derive(schema, Lengths, 98000);

            // 選び方の外は 11+8、選べるのは 256+8 と 5+8。
            Assert.Equal((98000 - 1000) / (19 + 264 + 13) / 2, limits.LimitDefault);
            Assert.Equal((98000 - 1000) / (19 + 13), limits.LimitMaximum);
        }

        [Fact]
        public void BothCountsStayAtOneWhenTheRoomIsSmallerThanOneItem()
        {
            ToolSchema schema = Listing(Chosen("name", "text"));

            ListingLimits limits = ListingLimitRule.Derive(schema, Lengths, 1001);

            Assert.Equal(1, limits.LimitDefault);
            Assert.Equal(1, limits.LimitMaximum);
        }

        [Fact]
        public void RoomThatDoesNotCoverTheListingFrameStops()
        {
            ToolSchema schema = Listing(Chosen("name", "text"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(schema, Lengths, 1000));

            Assert.Contains("一覧応答の枠に足りない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnElementWithoutAChosenItemStops()
        {
            ToolSchema schema = Listing(Value("index", "number"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(schema, Lengths, 98000));

            Assert.Contains("選べる項目が無い", error.Message, StringComparison.Ordinal);
        }

        public static IEnumerable<object[]> OutputsWithoutTheSlicedArray()
        {
            yield return new object[] { Value(null, "number") };
            yield return new object[] { Group(null, Value("total", "number")) };
            yield return new object[] { Group(null, Value("items", "number")) };
            yield return new object[]
            {
                Group(null, Items(Value(null, "number"))),
            };
        }

        [Theory]
        [MemberData(nameof(OutputsWithoutTheSlicedArray))]
        public void AnOutputWithoutTheSlicedArrayStops(SchemaItem output)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(Tool(output), Lengths, 98000));

            Assert.Contains("切り出した並びを持たない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSchemaAndTheTableAreRequired()
        {
            ToolSchema schema = Listing(Chosen("name", "text"));

            Assert.Throws<ArgumentNullException>(() => ListingLimitRule.Derive(null, Lengths, 98000));
            Assert.Throws<ArgumentNullException>(() => ListingLimitRule.Derive(schema, null, 98000));
        }
    }
}
