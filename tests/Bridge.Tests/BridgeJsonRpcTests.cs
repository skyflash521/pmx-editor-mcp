using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeJsonRpcTests
    {
        /// <summary>
        /// IPC仕様書がホストへ課している入れ子の深さの上限。ホストは値の再帰の段数で数え、
        /// 末端の値も1段に数える。応答は包絡のオブジェクトで1段、末端の値で1段使うので、
        /// 結果を包む配列がこれより2段少ないところが、ホストが返しうる最も深い応答になる。
        /// </summary>
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

        /// <summary>
        /// 本文は1行として送るので、区切りと紛れる文字がそのまま入ってはならない。
        /// </summary>
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

        /// <summary>
        /// ホストは要求の識別子を判別できないときや応答が上限を超えたときにnullを載せる。
        /// ここで弾くと、ホストが返した理由がブリッジ側の不正として塗り潰される。
        /// </summary>
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

        /// <summary>
        /// 応答が上限を超えたときは、どのコードでもホストが識別子をnullへ落とす契約である。
        /// </summary>
        [Theory]
        [InlineData(-32700)]
        [InlineData(-32004)]
        [InlineData(-32601)]
        public void AcceptsErrorResponseWithNullIdForAnyCode(int hostErrorCode)
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":" + hostErrorCode + ",\"message\":\"\"}}", 3);

            Assert.True(result.IsValid);
            Assert.Equal(hostErrorCode, result.Response.ErrorCode);
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
        [InlineData("not json")]
        // トップレベルがオブジェクトでない。
        [InlineData("[1,2]")]
        [InlineData("\"pong\"")]
        [InlineData("null")]
        // 版の欠落と不一致。
        [InlineData("{\"id\":3,\"result\":1}")]
        [InlineData("{\"jsonrpc\":\"1.0\",\"id\":3,\"result\":1}")]
        [InlineData("{\"jsonrpc\":2,\"id\":3,\"result\":1}")]
        // 識別子の欠落・型違い・不一致。
        [InlineData("{\"jsonrpc\":\"2.0\",\"result\":1}")]
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
        public void AcceptsDeepestResponseHostCanReturn()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                NestedResponse(HostRecursionLimit - 2), 3);

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
