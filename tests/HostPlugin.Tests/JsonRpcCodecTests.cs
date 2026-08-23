using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class JsonRpcCodecTests
    {
        private const string EmptyMethodRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"\"}";

        private const string NullIdWithArrayParamsRequest =
            "{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"ping\",\"params\":[1,2]}";

        private static IDictionary<string, object> ParseObject(string json)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"m\",\"params\":" + json + "}");
            Assert.True(result.IsValid);

            IDictionary<string, object> parameters;
            Assert.True(result.Request.TryGetParams(out parameters));
            return parameters;
        }

        [Fact]
        public void 妥当な要求を解析する()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"ping\"}");

            Assert.True(result.IsValid);
            Assert.Equal("ping", result.Request.Method);
            Assert.Equal(7, Convert.ToInt32(result.Request.Id));
            Assert.Equal(7, Convert.ToInt32(result.Id));

            // 引数を省略した要求からも、空の引数として取り出せる。
            IDictionary<string, object> parameters;
            Assert.True(result.Request.TryGetParams(out parameters));
            Assert.Empty(parameters);
        }

        [Fact]
        public void 文字列の識別子を受理する()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest("{\"jsonrpc\":\"2.0\",\"id\":\"a-1\",\"method\":\"ping\"}");

            Assert.True(result.IsValid);
            Assert.Equal("a-1", result.Request.Id);
        }

        [Fact]
        public void 引数を解析する()
        {
            IDictionary<string, object> parameters = ParseObject("{\"protocol\":1,\"name\":\"あ\"}");

            Assert.Equal(1, Convert.ToInt32(parameters["protocol"]));
            Assert.Equal("あ", parameters["name"]);
        }

        // 空(空白のみのものを含む)の本文は、シリアライザからは null リテラルと同じ結果に
        // 見えるため、解析の前に構文不正として分ける必要がある。
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{")]
        [InlineData("{\"jsonrpc\":}")]
        [InlineData("これはJSONではない")]
        // 数値の桁あふれと壊れた数値も、シリアライザからは解釈の失敗として返る。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1e999999,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"m\",\"params\":{\"x\":1.2.3}}")]
        public void JSONとして解釈できない本文は構文不正になる(string line)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(line);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.ParseError, result.ErrorCode);
            Assert.Null(result.Id);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Theory]
        [InlineData("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}]")]
        [InlineData("\"文字列\"")]
        [InlineData("5")]
        [InlineData("null")]
        public void トップレベルがオブジェクトでない要求は構造不正になる(string line)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(line);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, result.ErrorCode);
            Assert.Null(result.Id);
        }

        [Theory]
        [InlineData("{\"id\":1,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":2.0,\"id\":1,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":5}")]
        public void 判別できる識別子を持つ構造不正は識別子を返す(string line)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(line);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, result.ErrorCode);
            Assert.Equal(1, Convert.ToInt32(result.Id));
        }

        [Theory]
        [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":true,\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":[1],\"method\":\"ping\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":{\"v\":1},\"method\":\"ping\"}")]
        public void 識別子が欠けるか許容外の型なら構造不正で識別子はnullになる(string line)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(line);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, result.ErrorCode);
            Assert.Null(result.Id);
        }

        [Theory]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\",\"params\":[1,2]}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\",\"params\":\"文字列\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\",\"params\":5}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\",\"params\":null}")]
        public void 引数がオブジェクト以外でも解析は通り取り出しだけが失敗する(string line)
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(line);

            // 引数の検査より前にハンドシェイクとメソッドの検索があるため、解析では弾かない。
            Assert.True(result.IsValid);
            Assert.Equal(3, Convert.ToInt32(result.Request.Id));

            IDictionary<string, object> parameters;
            Assert.False(result.Request.TryGetParams(out parameters));
            Assert.Null(parameters);
        }

        [Fact]
        public void 空のメソッド名は構造不正にしない()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(EmptyMethodRequest);

            // 対応する処理が無いことはディスパッチの段で決まる。
            Assert.True(result.IsValid);
            Assert.Equal(string.Empty, result.Request.Method);
        }

        [Fact]
        public void 識別子と引数がともに不正なら構造不正を先に判定する()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(NullIdWithArrayParamsRequest);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, result.ErrorCode);
            Assert.Null(result.Id);
        }

        [Fact]
        public void シリアライザの既定より大きい本文を解析できる()
        {
            // 既定の上限は約200万文字。それを超えても16MiB以下なら受理する。
            string large = new string('a', 3000000);
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":{\"text\":\"" + large + "\"}}");

            Assert.True(result.IsValid);
            IDictionary<string, object> parameters;
            Assert.True(result.Request.TryGetParams(out parameters));
            Assert.Equal(large, parameters["text"]);
        }

        [Fact]
        public void 成功の応答を組み立てる()
        {
            string line = JsonRpcCodec.SerializeResult(7, "pong");

            Assert.Contains("\"jsonrpc\":\"2.0\"", line);
            Assert.Contains("\"id\":7", line);
            Assert.Contains("\"result\":\"pong\"", line);
            Assert.DoesNotContain("\"error\"", line);
        }

        [Fact]
        public void 文字列の識別子を持つ応答を組み立てる()
        {
            string line = JsonRpcCodec.SerializeResult("a-1", "pong");

            Assert.Contains("\"id\":\"a-1\"", line);
        }

        [Fact]
        public void オブジェクトの結果を組み立てる()
        {
            Dictionary<string, object> result = new Dictionary<string, object>
            {
                { "protocol", 1 },
                { "hostVersion", "1.2.3.4" },
                { "budgetChars", 100000 },
            };

            string line = JsonRpcCodec.SerializeResult(1, result);

            Assert.Contains("\"result\":{", line);
            Assert.Contains("\"protocol\":1", line);
            Assert.Contains("\"hostVersion\":\"1.2.3.4\"", line);
            Assert.Contains("\"budgetChars\":100000", line);
        }

        [Fact]
        public void エラーの応答を組み立てる()
        {
            string line = JsonRpcCodec.SerializeError(null, JsonRpcErrorCodes.ParseError, "解釈できない");

            Assert.Contains("\"jsonrpc\":\"2.0\"", line);
            Assert.Contains("\"id\":null", line);
            Assert.Contains("\"error\":{", line);
            Assert.Contains("\"code\":-32700", line);
            Assert.Contains("\"message\":\"解釈できない\"", line);
            Assert.DoesNotContain("\"result\"", line);
        }

        [Fact]
        public void エラーの応答の本文に改行を含めない()
        {
            string line = JsonRpcCodec.SerializeError(1, JsonRpcErrorCodes.InternalError, "改行\r\nを含む説明");

            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }

        [Fact]
        public void 成功の応答の本文に改行を含めない()
        {
            string line = JsonRpcCodec.SerializeResult(1, "改行\r\nを含む結果");

            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }

        [Fact]
        public void シリアライザの既定より大きい結果も組み立てる()
        {
            // シリアライザの既定の上限は約200万文字。組み立てには上限を課さない。
            string large = new string('a', 3000000);

            string line = JsonRpcCodec.SerializeResult(1, large);

            Assert.Contains(large, line, StringComparison.Ordinal);
        }

        [Fact]
        public void シリアライズできない結果は例外になる()
        {
            Dictionary<string, object> looped = new Dictionary<string, object>();
            looped["self"] = looped;

            Assert.ThrowsAny<Exception>(() => JsonRpcCodec.SerializeResult(1, looped));
        }

        [Fact]
        public void 解析の文字数の上限は3355万文字である()
        {
            Assert.Equal(33554432, JsonRpcCodec.ParseMaxJsonLength);
        }

        [Fact]
        public void エラーコードは契約で定めた値である()
        {
            Assert.Equal(-32700, JsonRpcErrorCodes.ParseError);
            Assert.Equal(-32600, JsonRpcErrorCodes.InvalidRequest);
            Assert.Equal(-32601, JsonRpcErrorCodes.MethodNotFound);
            Assert.Equal(-32602, JsonRpcErrorCodes.InvalidParams);
            Assert.Equal(-32603, JsonRpcErrorCodes.InternalError);
            Assert.Equal(-32001, JsonRpcErrorCodes.ProtocolMismatch);
            Assert.Equal(-32002, JsonRpcErrorCodes.RequestTimeout);
            Assert.Equal(-32003, JsonRpcErrorCodes.HandshakeRequired);
            Assert.Equal(-32004, JsonRpcErrorCodes.RequestTooLarge);
            Assert.Equal(-32005, JsonRpcErrorCodes.ResponseTooLarge);
        }
    }
}
