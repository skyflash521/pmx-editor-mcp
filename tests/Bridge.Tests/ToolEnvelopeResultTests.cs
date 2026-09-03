using System;
using System.Linq;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ToolEnvelopeResultTests
    {
        private const string Notice = "接続先: PmxEditor(12345)";

        [Fact]
        public void ASuccessBecomesTheValueAsJson()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":{\"total\":2}}"), Notice);

            Assert.False(result.IsError ?? false);
            Assert.Equal(Notice + "\n{\"total\":2}", Text(result));
        }

        [Fact]
        public void ASuccessWithoutAValueBecomesNull()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":null}"), Notice);

            Assert.Equal(Notice + "\nnull", Text(result));
        }

        [Fact]
        public void AFailureBecomesTheCodeAndTheMessage()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_INVALID_ARGUMENT\""
                        + ",\"message\":\"値が範囲の外にある。\"}}"),
                Notice);

            Assert.True(result.IsError);
            Assert.Equal(Notice + "\nTOOL_INVALID_ARGUMENT: 値が範囲の外にある。", Text(result));
        }

        [Fact]
        public void WarningsAreAddedAsLines()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":true,\"value\":1,\"warnings\":[\"表示の更新に失敗した。\",\"二つ目。\"]}"),
                Notice);

            Assert.Equal(Notice + "\n1\n警告: 表示の更新に失敗した。\n警告: 二つ目。", Text(result));
        }

        [Fact]
        public void WarningsAreAddedToAFailureToo()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse(
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_OPERATION_FAILED\",\"message\":\"失敗。\"}"
                        + ",\"warnings\":[\"未変更。\"]}"),
                Notice);

            Assert.True(result.IsError);
            Assert.Equal(Notice + "\nTOOL_OPERATION_FAILED: 失敗。\n警告: 未変更。", Text(result));
        }

        [Fact]
        public void TheResultIsOneTextContent()
        {
            CallToolResult result = ToolEnvelopeResult.From(
                JsonNode.Parse("{\"ok\":true,\"value\":1}"), Notice);

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
                () => ToolEnvelopeResult.From(JsonNode.Parse(json), Notice));
        }

        [Fact]
        public void TheTargetNoticeIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ToolEnvelopeResult.From(JsonNode.Parse("{\"ok\":true,\"value\":1}"), null));
        }

        private static string Text(CallToolResult result)
        {
            return result.Content.OfType<TextContentBlock>().Single().Text;
        }
    }
}
