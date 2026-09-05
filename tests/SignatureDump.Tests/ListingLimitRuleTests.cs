using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ListingLimitRuleTests
    {
        /// <summary>
        /// 一覧を返す題材。選べる項目2つと、選び方の外に置かれる項目1つを持つ。
        /// </summary>
        private static string Listing(string members)
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                ""output"": { ""origin"": ""hostOutput"", ""members"": [
                  { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
                  { ""name"": ""items"", ""origin"": ""hostOutput"", ""maxItems"": 100,
                    ""element"": { ""origin"": ""hostOutput"", ""members"": " + members + @" } }] },
                ""listing"": { ""limitDefault"": 1, ""limitMaximum"": 1 } }] }";
        }

        private static ToolSchema Read(string table)
        {
            return Assert.Single(ToolSchemaJsonReader.Read(table).Tools);
        }

        /// <summary>題材の表。実物の値は仕様書が持つ。</summary>
        private static readonly AssumedLength Lengths = new AssumedLength(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "number", 11 },
                { "text", 256 },
                { "boolean", 5 },
            });

        [Fact]
        public void TheMaximumTakesTheSmallestChosenItemAndTheDefaultTakesThemAll()
        {
            ToolSchema schema = Read(Listing(
                @"[{ ""name"": ""index"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
                   { ""name"": ""name"", ""origin"": ""sdkReturn"", ""shape"": ""text"" },
                   { ""name"": ""flag"", ""origin"": ""sdkReturn"", ""shape"": ""boolean"" }]"));

            ListingLimits limits = ListingLimitRule.Derive(schema, Lengths, 98000);

            // 選び方の外は 11+8、選べるのは 256+8 と 5+8。
            Assert.Equal((98000 - 1000) / (19 + 264 + 13) / 2, limits.LimitDefault);
            Assert.Equal((98000 - 1000) / (19 + 13), limits.LimitMaximum);
        }

        [Fact]
        public void BothCountsStayAtOneWhenTheRoomIsSmallerThanOneItem()
        {
            ToolSchema schema = Read(Listing(
                @"[{ ""name"": ""name"", ""origin"": ""sdkReturn"", ""shape"": ""text"" }]"));

            ListingLimits limits = ListingLimitRule.Derive(schema, Lengths, 1001);

            Assert.Equal(1, limits.LimitDefault);
            Assert.Equal(1, limits.LimitMaximum);
        }

        [Fact]
        public void RoomThatDoesNotCoverTheListingFrameStops()
        {
            ToolSchema schema = Read(Listing(
                @"[{ ""name"": ""name"", ""origin"": ""sdkReturn"", ""shape"": ""text"" }]"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(schema, Lengths, 1000));

            Assert.Contains("一覧応答の枠に足りない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnElementWithoutAChosenItemStops()
        {
            ToolSchema schema = Read(Listing(
                @"[{ ""name"": ""index"", ""origin"": ""hostOutput"", ""shape"": ""number"" }]"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(schema, Lengths, 98000));

            Assert.Contains("選べる項目が無い", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(@"""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" }")]
        [InlineData(@"""output"": { ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" }] }")]
        [InlineData(@"""output"": { ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""items"", ""origin"": ""hostOutput"", ""shape"": ""number"" }] }")]
        [InlineData(@"""output"": { ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""items"", ""origin"": ""hostOutput"", ""maxItems"": 100,
              ""element"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }")]
        public void AnOutputWithoutTheSlicedArrayStops(string output)
        {
            ToolSchema schema = Read(
                @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                    ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }], " + output + @",
                    ""listing"": { ""limitDefault"": 1, ""limitMaximum"": 1 } }] }");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ListingLimitRule.Derive(schema, Lengths, 98000));

            Assert.Contains("切り出した並びを持たない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSchemaAndTheTableAreRequired()
        {
            ToolSchema schema = Read(Listing(
                @"[{ ""name"": ""name"", ""origin"": ""sdkReturn"", ""shape"": ""text"" }]"));

            Assert.Throws<ArgumentNullException>(() => ListingLimitRule.Derive(null, Lengths, 98000));
            Assert.Throws<ArgumentNullException>(() => ListingLimitRule.Derive(schema, null, 98000));
        }
    }
}
