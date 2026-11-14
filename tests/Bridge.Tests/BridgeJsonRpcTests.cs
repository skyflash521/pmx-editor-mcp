using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeJsonRpcTests
    {
        private const int HostRecursionLimit = 100;

        [Fact]
        public void BuildsRequestWithoutArguments()
        {
            string request = BridgeJsonRpc.SerializeRequest(1, "ping", null);

            JsonObject parsed = JsonNode.Parse(request).AsObject();
            Assert.Equal("2.0", (string)parsed["jsonrpc"]);
            Assert.Equal(1, (int)parsed["id"]);
            Assert.Equal("ping", (string)parsed["method"]);

            // 引数が無いときは項目自体を置かない(明示的なnullと区別できなくなるため)。
            Assert.False(parsed.ContainsKey("params"));
        }

        [Fact]
        public void BuildsRequestWithArguments()
        {
            JsonObject parameters = new JsonObject { ["protocol"] = 1 };

            string request = BridgeJsonRpc.SerializeRequest(7, "handshake", parameters);

            JsonObject parsed = JsonNode.Parse(request).AsObject();
            Assert.Equal(7, (int)parsed["id"]);
            Assert.Equal("handshake", (string)parsed["method"]);
            Assert.Equal(1, (int)parsed["params"]["protocol"]);
        }

        [Fact]
        public void BuiltRequestContainsNoNewline()
        {
            JsonObject parameters = new JsonObject { ["name"] = "1行目\n2行目\r" };

            string request = BridgeJsonRpc.SerializeRequest(1, "m", parameters);

            Assert.DoesNotContain("\n", request);
            Assert.DoesNotContain("\r", request);
        }

        [Fact]
        public void ParsesSuccessResponse()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":\"pong\"}", 3);

            Assert.True(result.IsValid);
            Assert.False(result.Response.IsError);
            Assert.Equal("pong", (string)result.Response.Result);
        }

        [Fact]
        public void AcceptsSuccessResponseWithNullResult()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":null}", 3);

            Assert.True(result.IsValid);
            Assert.False(result.Response.IsError);
            Assert.Null(result.Response.Result);
        }

        [Fact]
        public void ParsesErrorResponse()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":-32601,\"message\":\"未知のメソッド\"}}", 3);

            Assert.True(result.IsValid);
            Assert.True(result.Response.IsError);
            Assert.Equal(-32601, result.Response.ErrorCode);
            Assert.Equal("未知のメソッド", result.Response.ErrorMessage);
        }

        [Fact]
        public void AcceptsErrorResponseWithNullId()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32700,\"message\":\"\"}}", 3);

            Assert.True(result.IsValid);
            Assert.True(result.Response.IsError);
            Assert.Equal(-32700, result.Response.ErrorCode);
            Assert.Equal(string.Empty, result.Response.ErrorMessage);
        }

        [Fact]
        public void RejectsErrorResponseMissingIdMember()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32601,\"message\":\"x\"}}", 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Fact]
        public void RejectsErrorResponseWhoseIdBelongsToAnotherRequest()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":9,\"error\":{\"code\":-32601,\"message\":\"x\"}}", 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Theory]
        // 解析できない本文。
        [InlineData("")]
        [InlineData("{")]
        // トップレベルがオブジェクトでない。
        [InlineData("[1,2]")]
        [InlineData("null")]
        // バージョンの欠落と不一致。
        [InlineData("{\"id\":3,\"result\":1}")]
        [InlineData("{\"jsonrpc\":\"1.0\",\"id\":3,\"result\":1}")]
        [InlineData("{\"jsonrpc\":2,\"id\":3,\"result\":1}")]
        // 識別子の欠落・型違い・不一致。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":\"3\",\"result\":1}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":4,\"result\":1}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":null,\"result\":1}")]
        // 結果とエラーの過不足。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":1,\"error\":{\"code\":-1,\"message\":\"x\"}}")]
        // エラーの構造不正。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":\"x\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"message\":\"x\"}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":\"-1\",\"message\":\"x\"}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":-1.5,\"message\":\"x\"}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":-1}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":-1,\"message\":5}}")]
        public void ResponseOutsideContractIsInvalidWithReason(string message)
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(message, 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Fact]
        public void ParseDepthLimitMatchesContract()
        {
            Assert.Equal(100, BridgeJsonRpc.MaxDepth);
        }

        [Fact]
        public void AcceptsHostDepthLimitWithEmptyInnermostArray()
        {
            // 末端が数値などのスカラー値でなく空の配列のときは、その配列自体が末端の1段になるので、包絡の1段と
            // 合わせて配列を上限より1段少なくしたところがホストの数え方で上限ちょうどになる。
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                NestedResponse(HostRecursionLimit - 1, string.Empty), 3);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void RejectsResponseDeeperThanLimit()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                NestedResponse(BridgeJsonRpc.MaxDepth + 1), 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        private static string NestedResponse(int arrayDepth)
        {
            return NestedResponse(arrayDepth, "1");
        }

        /// <summary>最も内側の記述を指定した段数だけ配列で包んだ応答を作る。</summary>
        private static string NestedResponse(int arrayDepth, string innermost)
        {
            StringBuilder built = new StringBuilder();
            built.Append("{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":");
            built.Append('[', arrayDepth);
            built.Append(innermost);
            built.Append(']', arrayDepth);
            built.Append('}');
            return built.ToString();
        }
    }
}
