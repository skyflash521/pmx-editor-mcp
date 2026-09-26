using System;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ToolEnvelopeResultTests
    {
        private const string Notice = "接続先: PmxEditor(12345)";

        private const int Budget = 100000;

        /// <summary>PNGを詰めた文字列の代わり。中身は読まれないので、短い綴りで足りる。</summary>
        private const string Png = "iVBORw0KGgo=";

        [Fact]
        public void ASuccessBecomesTheValueAsJson()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":{\"total\":2}}"), Notice, Budget, false);

            Assert.False(result.IsError ?? false);
            Assert.Equal(Notice + "\n{\"total\":2}", Text(result));
        }

        [Fact]
        public void TextOutsideAsciiInTheValueIsKeptAsIs()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":{\"name\":\"右足首D\"}}"), Notice, Budget, false);

            Assert.Equal(Notice + "\n{\"name\":\"右足首D\"}", Text(result));
        }

        [Fact]
        public void OnlyWhatJsonRequiresIsEscapedInTheValue()
        {
            JsonObject value = new JsonObject
            {
                ["comment"] = "全　角\U0001F600\"引\\用\n改\u0001",
                ["items"] = new JsonArray(1, 2.5, true, null),
            };
            JsonObject envelope = new JsonObject { ["ok"] = true, ["value"] = value };

            CallToolResult result = ToolEnvelopeResult.From(envelope, Notice, Budget, false);

            Assert.Equal(
                Notice + "\n{\"comment\":\"全　角\U0001F600\\\"引\\\\用\\n改\\u0001\",\"items\":[1,2.5,true,null]}",
                Text(result));
        }

        [Fact]
        public void AHalfOfASurrogatePairStandingAloneIsEscaped()
        {
            JsonObject envelope = new JsonObject { ["ok"] = true, ["value"] = "前\uD800後\uDC00" };

            CallToolResult result = ToolEnvelopeResult.From(envelope, Notice, Budget, false);

            Assert.Equal(Notice + "\n\"前\\ud800後\\udc00\"", Text(result));
        }

        [Fact]
        public void ASuccessWithoutAValueBecomesNull()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":null}"), Notice, Budget, false);

            Assert.Equal(Notice + "\nnull", Text(result));
        }

        [Fact]
        public void AFailureBecomesTheCodeAndTheMessage()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_INVALID_ARGUMENT\""
                        + ",\"message\":\"値が範囲の外にある。\"}}"),
                Notice,
                Budget,
                false);

            Assert.True(result.IsError);
            Assert.Equal(Notice + "\nTOOL_INVALID_ARGUMENT: 値が範囲の外にある。", Text(result));
        }

        [Fact]
        public void WarningsAreAddedAsLines()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":true,\"value\":1,\"warnings\":[\"表示の更新に失敗した。\",\"二つ目。\"]}"),
                Notice,
                Budget,
                false);

            Assert.Equal(Notice + "\n1\n警告: 表示の更新に失敗した。\n警告: 二つ目。", Text(result));
        }

        [Fact]
        public void WarningsAreAddedToAFailureToo()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_OPERATION_FAILED\",\"message\":\"失敗。\"}"
                        + ",\"warnings\":[\"未変更。\"]}"),
                Notice,
                Budget,
                false);

            Assert.True(result.IsError);
            Assert.Equal(Notice + "\nTOOL_OPERATION_FAILED: 失敗。\n警告: 未変更。", Text(result));
        }

        [Fact]
        public void TheResultIsOneTextContent()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":1}"), Notice, Budget, false);

            Assert.Single(result.Content);
            Assert.IsType<TextContentBlock>(result.Content[0]);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("1")]
        [InlineData("{\"value\":1}")]
        [InlineData("{\"ok\":\"true\",\"value\":1}")]
        [InlineData("{\"ok\":true}")]
        [InlineData("{\"ok\":false}")]
        [InlineData("{\"ok\":false,\"error\":{\"message\":\"説明。\"}}")]
        [InlineData("{\"ok\":false,\"error\":{\"code\":\"TOOL_NOT_APPLICABLE\"}}")]
        [InlineData("{\"ok\":true,\"value\":1,\"warnings\":\"一つ\"}")]
        [InlineData("{\"ok\":true,\"value\":1,\"warnings\":[1]}")]
        [InlineData("{\"ok\":true,\"value\":1,\"warnings\":[\"  \"]}")]
        [InlineData("{\"ok\":false,\"error\":{\"code\":\"TOOL_NOT_APPLICABLE\",\"message\":\"  \"}}")]
        public void AnEnvelopeThatBreaksTheContractStops(string json)
        {
            Assert.Throws<FormatException>(
                () => ToolEnvelopeResult.From(JsonNode.Parse(json), Notice, Budget, false));
        }

        [Fact]
        public void TheTargetNoticeIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ToolEnvelopeResult.From(
                    JsonNode.Parse("{\"ok\":true,\"value\":1}"), null, Budget, false));
        }

        [Fact]
        public void ABodyOverTheBudgetCarriesTheWayToNarrowTheAnswer()
        {
            string value = new string('a', BridgeBudget.MinimumChars + 1);

            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + value + "\"}"),
                Notice,
                BridgeBudget.MinimumChars,
                false,
                "fields で項目を絞る");

            Assert.True(result.IsError);
            Assert.Contains("fields", Text(result), StringComparison.Ordinal);
        }

        [Fact]
        public void ABodyOverTheBudgetBecomesTheTooLargeError()
        {
            string value = new string('a', BridgeBudget.MinimumChars + 1);

            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + value + "\"}"),
                Notice,
                BridgeBudget.MinimumChars,
                false);

            Assert.True(result.IsError);
            Assert.Contains("TOOL_RESPONSE_TOO_LARGE", Text(result), StringComparison.Ordinal);
            Assert.DoesNotContain(value, Text(result), StringComparison.Ordinal);
        }

        [Fact]
        public void ABodyThatJustFitsIsKept()
        {
            string value = new string('a', BridgeBudget.MinimumChars - 2);

            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + value + "\"}"),
                Notice,
                BridgeBudget.MinimumChars,
                false);

            Assert.False(result.IsError ?? false);
            Assert.Equal(Notice + "\n\"" + value + "\"", Text(result));
        }

        [Fact]
        public void TheTargetNoticeIsNotCountedInTheBudget()
        {
            string value = new string('a', BridgeBudget.MinimumChars - 2);

            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + value + "\"}"),
                new string('n', 100),
                BridgeBudget.MinimumChars,
                false);

            Assert.False(result.IsError ?? false);
        }

        [Fact]
        public void ABudgetUnderTheLowerBoundStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ToolEnvelopeResult.From(
                    JsonNode.Parse("{\"ok\":true,\"value\":1}"),
                    Notice,
                    BridgeBudget.MinimumChars - 1,
                    false));
        }

        [Fact]
        public void AnImageBecomesAnImageContentAndLeavesTheBodyOut()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + Png + "\"}"), Notice, Budget, true);

            Assert.False(result.IsError ?? false);
            Assert.Equal(Notice, Text(result));
            ImageContentBlock drawn = result.Content.OfType<ImageContentBlock>().Single();
            Assert.Equal("image/png", drawn.MimeType);
            Assert.Equal(Png, Encoding.UTF8.GetString(drawn.Data.ToArray()));
        }

        [Fact]
        public void AnImageIsNotCountedInTheBudget()
        {
            string packed = new string('a', BridgeBudget.MinimumChars + 1);

            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":\"" + packed + "\"}"),
                Notice,
                BridgeBudget.MinimumChars,
                true);

            Assert.False(result.IsError ?? false);
            Assert.Equal(
                packed,
                Encoding.UTF8.GetString(
                    result.Content.OfType<ImageContentBlock>().Single().Data.ToArray()));
        }

        [Fact]
        public void TheWarningsOfAnImageStillComeAsText()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":true,\"value\":\"" + Png + "\",\"warnings\":[\"画像を縮めた。\"]}"),
                Notice,
                Budget,
                true);

            Assert.Equal(Notice + "\n警告: 画像を縮めた。", Text(result));
            Assert.Single(result.Content.OfType<ImageContentBlock>());
        }

        [Fact]
        public void AFailureOfAnImageToolCarriesNoImage()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_NOT_APPLICABLE\""
                        + ",\"message\":\"ビューが無い。\"}}"),
                Notice,
                Budget,
                true);

            Assert.True(result.IsError);
            Assert.Empty(result.Content.OfType<ImageContentBlock>());
        }

        [Theory]
        [InlineData("{\"ok\":true,\"value\":null}")]
        [InlineData("{\"ok\":true,\"value\":\"\"}")]
        [InlineData("{\"ok\":true,\"value\":1}")]
        [InlineData("{\"ok\":true,\"value\":{\"data\":\"iVBORw0KGgo=\"}}")]
        public void AnImageThatIsNotAStringBreaksTheContract(string json)
        {
            Assert.Throws<FormatException>(
                () => ToolEnvelopeResult.From(JsonNode.Parse(json), Notice, Budget, true));
        }

        private static string Text(CallToolResult result)
        {
            return result.Content.OfType<TextContentBlock>().Single().Text;
        }
    }
}
