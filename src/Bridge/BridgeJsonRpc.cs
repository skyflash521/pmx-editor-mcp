using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ホストから受け取った応答1件。</summary>
    public sealed class HostResponse
    {
        private HostResponse(JsonNode result, bool isError, int errorCode, string errorMessage)
        {
            Result = result;
            IsError = isError;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>ホストがエラーを返したかどうか。</summary>
        public bool IsError { get; }

        /// <summary>成功応答の結果。<see cref="IsError"/> が偽のときだけ意味を持つ。</summary>
        public JsonNode Result { get; }

        /// <summary>ホストのエラーコード。<see cref="IsError"/> が真のときだけ意味を持つ。</summary>
        public int ErrorCode { get; }

        /// <summary>ホストのエラーの説明。<see cref="IsError"/> が真のときだけ意味を持つ。</summary>
        public string ErrorMessage { get; }

        internal static HostResponse ForResult(JsonNode result)
        {
            return new HostResponse(result, false, 0, null);
        }

        internal static HostResponse ForError(int errorCode, string errorMessage)
        {
            return new HostResponse(null, true, errorCode, errorMessage);
        }
    }

    /// <summary>応答を1件解析した結果。</summary>
    public sealed class HostResponseParseResult
    {
        private HostResponseParseResult(HostResponse response, string invalidReason)
        {
            Response = response;
            InvalidReason = invalidReason;
        }

        /// <summary>応答が契約に沿っているかどうか。</summary>
        public bool IsValid => InvalidReason == null;

        /// <summary>解析できた応答。<see cref="IsValid"/> が真のときだけ意味を持つ。</summary>
        public HostResponse Response { get; }

        /// <summary>
        /// 契約に沿わない理由。<see cref="IsValid"/> が偽のときだけ意味を持つ。呼び出し側は
        /// これをエラー本文へ載せ、handshake が成立する前か後かでエラーコードを選ぶ。
        /// </summary>
        public string InvalidReason { get; }

        internal static HostResponseParseResult Valid(HostResponse response)
        {
            return new HostResponseParseResult(response, null);
        }

        internal static HostResponseParseResult Invalid(string invalidReason)
        {
            return new HostResponseParseResult(null, invalidReason);
        }
    }

    /// <summary>ホストとやり取りするJSON-RPC 2.0のサブセットの組み立てと解析。</summary>
    public static class BridgeJsonRpc
    {
        /// <summary>
        /// 解析で許す入れ子の深さ。ホストが組み立てうる深さの応答を受け取れるようにするための
        /// 値で、これを下回るとホストが正しく返した深い応答まで不正と判定してしまう。
        /// </summary>
        public const int MaxDepth = 100;

        private const string ProtocolVersion = "2.0";

        /// <summary>
        /// 要求を1行の本文へ組み立てる。<paramref name="parameters"/> が null のときは
        /// 引数の項目自体を置かない。
        /// </summary>
        public static string SerializeRequest(int id, string method, JsonObject parameters)
        {
            JsonObject request = new JsonObject
            {
                ["jsonrpc"] = ProtocolVersion,
                ["id"] = id,
                ["method"] = method,
            };

            if (parameters != null)
            {
                // 呼び出し側が渡した木をそのまま繋ぐと親が付け替わるので、複製を置く。
                request["params"] = parameters.DeepClone();
            }

            return request.ToJsonString();
        }

        /// <summary>
        /// 応答の本文を解析し、契約に沿っているかを判定する。成功応答の識別子は
        /// <paramref name="expectedId"/> と一致していなければならない。エラー応答は識別子が
        /// null であることも許す——ホストは要求の識別子を判別できないときや応答が上限を
        /// 超えたときに null を載せる契約であり、ここで弾くとホストが返した理由が失われる。
        /// </summary>
        public static HostResponseParseResult ParseResponse(string message, int expectedId)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(message, null, new JsonDocumentOptions { MaxDepth = MaxDepth });
            }
            catch (JsonException error)
            {
                return HostResponseParseResult.Invalid("応答をJSONとして解析できない: " + error.Message);
            }

            JsonObject response = root as JsonObject;
            if (response == null)
            {
                return HostResponseParseResult.Invalid("応答のトップレベルがJSONオブジェクトでない。");
            }

            JsonNode versionNode;
            string version;
            if (!response.TryGetPropertyValue("jsonrpc", out versionNode)
                || !TryGetString(versionNode, out version)
                || version != ProtocolVersion)
            {
                return HostResponseParseResult.Invalid("応答の jsonrpc が " + ProtocolVersion + " でない。");
            }

            JsonNode idNode;
            if (!response.TryGetPropertyValue("id", out idNode))
            {
                return HostResponseParseResult.Invalid("応答に id が無い。");
            }

            JsonNode resultNode;
            JsonNode errorNode;
            bool hasResult = response.TryGetPropertyValue("result", out resultNode);
            bool hasError = response.TryGetPropertyValue("error", out errorNode);
            if (hasResult == hasError)
            {
                return HostResponseParseResult.Invalid("応答は result と error のどちらか一方だけを持つ。");
            }

            if (hasError)
            {
                if (idNode != null && !IsExpectedId(idNode, expectedId))
                {
                    return HostResponseParseResult.Invalid("応答の id が要求と一致しない。");
                }

                return ParseError(errorNode);
            }

            if (idNode == null || !IsExpectedId(idNode, expectedId))
            {
                return HostResponseParseResult.Invalid("応答の id が要求と一致しない。");
            }

            return HostResponseParseResult.Valid(HostResponse.ForResult(resultNode));
        }

        private static HostResponseParseResult ParseError(JsonNode errorNode)
        {
            JsonObject error = errorNode as JsonObject;
            if (error == null)
            {
                return HostResponseParseResult.Invalid("応答の error がオブジェクトでない。");
            }

            JsonNode codeNode;
            int code;
            if (!error.TryGetPropertyValue("code", out codeNode) || !TryGetInt32(codeNode, out code))
            {
                return HostResponseParseResult.Invalid("応答の error.code が整数でない。");
            }

            JsonNode messageNode;
            string errorMessage;
            if (!error.TryGetPropertyValue("message", out messageNode)
                || !TryGetString(messageNode, out errorMessage))
            {
                return HostResponseParseResult.Invalid("応答の error.message が文字列でない。");
            }

            return HostResponseParseResult.Valid(HostResponse.ForError(code, errorMessage));
        }

        private static bool IsExpectedId(JsonNode idNode, int expectedId)
        {
            int id;
            return TryGetInt32(idNode, out id) && id == expectedId;
        }

        internal static bool TryGetInt32(JsonNode node, out int value)
        {
            value = 0;

            JsonValue jsonValue = node as JsonValue;
            return jsonValue != null && jsonValue.TryGetValue(out value);
        }

        internal static bool TryGetString(JsonNode node, out string value)
        {
            value = null;

            JsonValue jsonValue = node as JsonValue;
            return jsonValue != null && jsonValue.TryGetValue(out value);
        }
    }
}
