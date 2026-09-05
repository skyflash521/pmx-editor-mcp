using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class JsonRpcCodecTests
    {
        private const string DoubleQuote = "\"";
        private const string EscapedBackslash = "\\\\";
        private const string EscapedQuote = "\\\"";

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
        public void ParsesValidRequest()
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
        public void AcceptsStringId()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest("{\"jsonrpc\":\"2.0\",\"id\":\"a-1\",\"method\":\"ping\"}");

            Assert.True(result.IsValid);
            Assert.Equal("a-1", result.Request.Id);
        }

        [Fact]
        public void ParsesArguments()
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
        public void BodyThatIsNotJsonIsSyntaxError(string line)
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
        public void RequestWhoseTopLevelIsNotObjectIsStructureError(string line)
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
        public void StructureErrorWithReadableIdReturnsThatId(string line)
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
        public void MissingOrUnsupportedIdTypeYieldsStructureErrorWithNullId(string line)
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
        public void NonObjectArgumentsStillParseAndOnlyExtractionFails(string line)
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
        public void EmptyMethodNameIsNotStructureError()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(EmptyMethodRequest);

            // 対応する処理が無いことはディスパッチの段で決まる。
            Assert.True(result.IsValid);
            Assert.Equal(string.Empty, result.Request.Method);
        }

        [Fact]
        public void StructureErrorIsDecidedBeforeInvalidArguments()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(NullIdWithArrayParamsRequest);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, result.ErrorCode);
            Assert.Null(result.Id);
        }

        [Fact]
        public void RequestAtDepthLimitIsParsed()
        {
            // 要求のオブジェクトと params のオブジェクトで2段、残りを空配列で埋めて上限ちょうどにする。
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                BuildNested(JsonRpcCodec.JsonRecursionLimit - 2));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void RequestDeeperThanLimitIsSyntaxError()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                BuildNested(JsonRpcCodec.JsonRecursionLimit - 1));

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.ParseError, result.ErrorCode);
        }

        private static string BuildNested(int arrayDepth)
        {
            return BuildNested(arrayDepth, null);
        }

        private static string BuildNested(int arrayDepth, string innermost)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":{\"a\":");
            builder.Append('[', arrayDepth);
            if (innermost != null)
            {
                builder.Append(innermost);
            }

            builder.Append(']', arrayDepth);
            builder.Append("}}");
            return builder.ToString();
        }

        [Fact]
        public void BodyLargerThanSerializerDefaultIsParsed()
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
        public void BuildsSuccessResponse()
        {
            string line = JsonRpcCodec.SerializeResult(7, "pong");

            Assert.Contains("\"jsonrpc\":\"2.0\"", line);
            Assert.Contains("\"id\":7", line);
            Assert.Contains("\"result\":\"pong\"", line);
            Assert.DoesNotContain("\"error\"", line);
        }

        [Fact]
        public void BuildsResponseWithStringId()
        {
            string line = JsonRpcCodec.SerializeResult("a-1", "pong");

            Assert.Contains("\"id\":\"a-1\"", line);
        }

        [Fact]
        public void BuildsObjectResult()
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
        public void BuildsErrorResponse()
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
        public void ErrorResponseBodyContainsNoNewline()
        {
            string line = JsonRpcCodec.SerializeError(1, JsonRpcErrorCodes.InternalError, "改行\r\nを含む説明");

            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }

        [Fact]
        public void SuccessResponseBodyContainsNoNewline()
        {
            string line = JsonRpcCodec.SerializeResult(1, "改行\r\nを含む結果");

            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }

        [Fact]
        public void BuildsResultLargerThanSerializerDefault()
        {
            // シリアライザの既定の上限は約200万文字。組み立てには上限を課さない。
            string large = new string('a', 3000000);

            string line = JsonRpcCodec.SerializeResult(1, large);

            Assert.Contains(large, line, StringComparison.Ordinal);
        }

        [Fact]
        public void UnserializableResultThrows()
        {
            Dictionary<string, object> looped = new Dictionary<string, object>();
            looped["self"] = looped;

            Assert.ThrowsAny<Exception>(() => JsonRpcCodec.SerializeResult(1, looped));
        }

        [Fact]
        public void StructureTokenLimitIsTwoHundredThousand()
        {
            Assert.Equal(200000, JsonRpcCodec.ParseStructureTokenLimit);
        }

        [Fact]
        public void RequestAtStructureTokenLimitIsParsed()
        {
            Assert.True(JsonRpcCodec.ParseRequest(BuildFlatArray(ElementsForLimit)).IsValid);
        }

        /// <summary>
        /// 上限内の本文でも、空のオブジェクトを並べれば解析で大量のオブジェクトが作られる。
        /// 上限ちょうどの要求へ開き括弧を1つ足しただけの本文で、判定の向きを固定する。
        /// </summary>
        [Fact]
        public void RequestOneTokenOverLimitIsInputLimitExceeded()
        {
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(BuildFlatArray(ElementsForLimit, "[]"));

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.RequestTooLarge, result.ErrorCode);
            Assert.Null(result.Id);
        }

        /// <summary>
        /// 値として置かれた括弧やコンマはオブジェクトを作らない。
        /// </summary>
        [Fact]
        public void SymbolsInsideStringsAreNotCountedAsStructureTokens()
        {
            string commas = new string(',', JsonRpcCodec.ParseStructureTokenLimit + 100);
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                BuildRequest(Quoted(commas)));

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// 末尾の逆斜線がエスケープ済みなら文字列はそこで終わる。取り違えると、あとに続く
        /// トークンを数え落として上限超過を見逃す。
        /// </summary>
        [Fact]
        public void StructureTokensAfterStringEndingWithBackslashAreCounted()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            builder.Append(Quoted(EscapedBackslash));
            for (int index = 0; index <= JsonRpcCodec.ParseStructureTokenLimit / 2; index++)
            {
                builder.Append(",{}");
            }

            builder.Append(']');
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(BuildRequest(builder.ToString()));

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCodes.RequestTooLarge, result.ErrorCode);
        }

        /// <summary>
        /// 閉じたと取り違えると、文字列の中のコンマを構造トークンに数えてしまう。
        /// </summary>
        [Fact]
        public void EscapedQuoteDoesNotCloseString()
        {
            string commas = new string(',', JsonRpcCodec.ParseStructureTokenLimit + 100);
            JsonRpcParseResult result = JsonRpcCodec.ParseRequest(
                BuildRequest(Quoted(EscapedQuote + commas)));

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// 構造トークンがちょうど上限になる要素の数。要求を包む部分が6トークン(要求のオブジェクトの
        /// 開き・項目の区切り3つ・params のオブジェクトの開き・params の2つ目の項目の区切り)、
        /// 配列の開きが1トークン、要素が1つ増えるごとに2トークン(オブジェクトの開きと区切り)増える。
        /// 区切りは要素の数より1つ少ないので、合計は要素の数の2倍に6を足した数になる。
        /// </summary>
        private static int ElementsForLimit
        {
            get { return (JsonRpcCodec.ParseStructureTokenLimit - 6) / 2; }
        }

        /// <summary>params に空のオブジェクトを並べた配列を置いた要求を作る。</summary>
        private static string BuildFlatArray(int elements)
        {
            return BuildFlatArray(elements, "0");
        }

        /// <summary>
        /// params に空のオブジェクトを並べた配列を置いた要求を作る。2つ目の項目の値でトークン数を
        /// 微調整する(値が `0` なら1トークン、`[]` なら開き括弧の分で2トークン増える)。
        /// </summary>
        private static string BuildFlatArray(int elements, string extraMemberValue)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[{}");
            for (int index = 1; index < elements; index++)
            {
                builder.Append(",{}");
            }

            builder.Append(']');
            return BuildRequest(builder.ToString(), extraMemberValue);
        }

        private static string BuildRequest(string paramsValue)
        {
            return BuildRequest(paramsValue, null);
        }

        /// <summary>
        /// params に値を1つ置いた要求を作る。2つ目の項目の値を与えると、区切りの1トークンと
        /// その値が持つトークンが増える。
        /// </summary>
        private static string BuildRequest(string paramsValue, string extraMemberValue)
        {
            string extra = extraMemberValue == null
                ? string.Empty
                : "," + Quoted("b") + ":" + extraMemberValue;
            return "{" + Quoted("jsonrpc") + ":" + Quoted("2.0") + "," + Quoted("id") + ":1,"
                + Quoted("method") + ":" + Quoted("ping") + "," + Quoted("params") + ":{"
                + Quoted("a") + ":" + paramsValue + extra + "}}";
        }

        private static string Quoted(string value)
        {
            return DoubleQuote + value + DoubleQuote;
        }

        [Fact]
        public void NestingDepthLimitIsOneHundred()
        {
            Assert.Equal(100, JsonRpcCodec.JsonRecursionLimit);
        }

        [Fact]
        public void LeafValueCountsAsOneLevel()
        {
            // 配列97段の内側に数値を置くと、要求と params の2段と合わせて上限ちょうどになる。
            Assert.True(JsonRpcCodec.ParseRequest(BuildNested(97, "1")).IsValid);
            Assert.False(JsonRpcCodec.ParseRequest(BuildNested(98, "1")).IsValid);
        }

        [Fact]
        public void ParseCharacterLimitMatchesContract()
        {
            Assert.Equal(33554432, JsonRpcCodec.ParseMaxJsonLength);
        }

        [Fact]
        public void ErrorCodesMatchContract()
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
