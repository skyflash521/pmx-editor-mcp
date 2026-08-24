using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeJsonRpcTests
    {
        private const string Pending = "impl pending: 要求を1行へ組み立て、ホストの応答が契約に沿っているかを判定する";

        /// <summary>
        /// IPC仕様書がホストへ課している入れ子の深さの上限。ホストは値の再帰の段数で数え、
        /// 末端の値も1段に数える。応答は包絡のオブジェクトで1段、末端の値で1段使うので、
        /// 結果を包む配列がこれより2段少ないところが、ホストが返しうる最も深い応答になる。
        /// </summary>
        private const int HostRecursionLimit = 100;

        [Fact(Skip = Pending)]
        public void 引数のない要求を組み立てる()
        {
            string request = BridgeJsonRpc.SerializeRequest(1, "ping", null);

            JsonObject parsed = JsonNode.Parse(request).AsObject();
            Assert.Equal("2.0", (string)parsed["jsonrpc"]);
            Assert.Equal(1, (int)parsed["id"]);
            Assert.Equal("ping", (string)parsed["method"]);

            // 引数が無いときは項目自体を置かない(明示的なnullと区別できなくなるため)。
            Assert.False(parsed.ContainsKey("params"));
        }

        [Fact(Skip = Pending)]
        public void 引数のある要求を組み立てる()
        {
            JsonObject parameters = new JsonObject { ["protocol"] = 1 };

            string request = BridgeJsonRpc.SerializeRequest(7, "handshake", parameters);

            JsonObject parsed = JsonNode.Parse(request).AsObject();
            Assert.Equal(7, (int)parsed["id"]);
            Assert.Equal("handshake", (string)parsed["method"]);
            Assert.Equal(1, (int)parsed["params"]["protocol"]);
        }

        [Fact(Skip = Pending)]
        public void 組み立てた要求は改行を含まない()
        {
            // 本文は1行として送るので、区切りと紛れる文字がそのまま入ってはならない。
            JsonObject parameters = new JsonObject { ["name"] = "1行目\n2行目\r" };

            string request = BridgeJsonRpc.SerializeRequest(1, "m", parameters);

            Assert.DoesNotContain("\n", request);
            Assert.DoesNotContain("\r", request);
        }

        [Fact(Skip = Pending)]
        public void 成功応答を解析する()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":\"pong\"}", 3);

            Assert.True(result.IsValid);
            Assert.False(result.Response.IsError);
            Assert.Equal("pong", (string)result.Response.Result);
        }

        [Fact(Skip = Pending)]
        public void 結果がnullの成功応答も受理する()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":null}", 3);

            Assert.True(result.IsValid);
            Assert.False(result.Response.IsError);
            Assert.Null(result.Response.Result);
        }

        [Fact(Skip = Pending)]
        public void エラー応答を解析する()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"error\":{\"code\":-32601,\"message\":\"未知のメソッド\"}}", 3);

            Assert.True(result.IsValid);
            Assert.True(result.Response.IsError);
            Assert.Equal(-32601, result.Response.ErrorCode);
            Assert.Equal("未知のメソッド", result.Response.ErrorMessage);
        }

        [Fact(Skip = Pending)]
        public void エラー応答は識別子がnullでも受理する()
        {
            // ホストは要求の識別子を判別できないときや応答が上限を超えたときにnullを載せる。
            // ここで弾くと、ホストが返した理由がブリッジ側の不正として塗り潰される。
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32700,\"message\":\"\"}}", 3);

            Assert.True(result.IsValid);
            Assert.True(result.Response.IsError);
            Assert.Equal(-32700, result.Response.ErrorCode);
            Assert.Equal(string.Empty, result.Response.ErrorMessage);
        }

        [Theory(Skip = Pending)]
        [InlineData(-32700)]
        [InlineData(-32004)]
        [InlineData(-32601)]
        public void 識別子がnullのエラー応答はコードを問わず受理する(int hostErrorCode)
        {
            // 応答が上限を超えたときは、どのコードでもホストが識別子をnullへ落とす契約である。
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":" + hostErrorCode + ",\"message\":\"\"}}", 3);

            Assert.True(result.IsValid);
            Assert.Equal(hostErrorCode, result.Response.ErrorCode);
        }

        [Fact(Skip = Pending)]
        public void エラー応答でも識別子の項目が無ければ不正とする()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32601,\"message\":\"x\"}}", 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Fact(Skip = Pending)]
        public void エラー応答の識別子が別の要求のものなら不正とする()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                "{\"jsonrpc\":\"2.0\",\"id\":9,\"error\":{\"code\":-32601,\"message\":\"x\"}}", 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Theory(Skip = Pending)]
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
        public void 契約に沿わない応答は理由を持って不正になる(string message)
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(message, 3);

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.InvalidReason));
        }

        [Fact(Skip = Pending)]
        public void 解析で許す深さの上限は契約で定めた値である()
        {
            Assert.Equal(100, BridgeJsonRpc.MaxDepth);
        }

        [Fact(Skip = Pending)]
        public void 最も内側が空の配列でもホストの上限までの深さを受理する()
        {
            // 末端が数値などのスカラー値でなく空の配列のときは、その配列自体が末端の1段になるので、包絡の1段と
            // 合わせて配列を上限より1段少なくしたところがホストの数え方で上限ちょうどになる。
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                NestedResponse(HostRecursionLimit - 1, string.Empty), 3);

            Assert.True(result.IsValid);
        }

        [Fact(Skip = Pending)]
        public void ホストが返しうる最も深い応答を受理する()
        {
            HostResponseParseResult result = BridgeJsonRpc.ParseResponse(
                NestedResponse(HostRecursionLimit - 2), 3);

            Assert.True(result.IsValid);
        }

        [Fact(Skip = Pending)]
        public void 深さの上限を超える応答は不正とする()
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
