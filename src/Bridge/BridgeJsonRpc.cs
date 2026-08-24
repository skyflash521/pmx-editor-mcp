using System;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ホストから受け取った応答1件。</summary>
    public sealed class HostResponse
    {
        private HostResponse(JsonNode result, bool isError, int errorCode, string errorMessage)
        {
            throw new NotImplementedException();
        }

        /// <summary>ホストがエラーを返したかどうか。</summary>
        public bool IsError => throw new NotImplementedException();

        /// <summary>成功応答の結果。<see cref="IsError"/> が偽のときだけ意味を持つ。</summary>
        public JsonNode Result => throw new NotImplementedException();

        /// <summary>ホストのエラーコード。<see cref="IsError"/> が真のときだけ意味を持つ。</summary>
        public int ErrorCode => throw new NotImplementedException();

        /// <summary>ホストのエラーの説明。<see cref="IsError"/> が真のときだけ意味を持つ。</summary>
        public string ErrorMessage => throw new NotImplementedException();
    }

    /// <summary>応答を1件解析した結果。</summary>
    public sealed class HostResponseParseResult
    {
        private HostResponseParseResult(HostResponse response, string invalidReason)
        {
            throw new NotImplementedException();
        }

        /// <summary>応答が契約に沿っているかどうか。</summary>
        public bool IsValid => throw new NotImplementedException();

        /// <summary>解析できた応答。<see cref="IsValid"/> が真のときだけ意味を持つ。</summary>
        public HostResponse Response => throw new NotImplementedException();

        /// <summary>
        /// 契約に沿わない理由。<see cref="IsValid"/> が偽のときだけ意味を持つ。呼び出し側は
        /// これをエラー本文へ載せ、handshake が成立する前か後かでエラーコードを選ぶ。
        /// </summary>
        public string InvalidReason => throw new NotImplementedException();
    }

    /// <summary>ホストとやり取りするJSON-RPC 2.0のサブセットの組み立てと解析。</summary>
    public static class BridgeJsonRpc
    {
        /// <summary>
        /// 解析で許す入れ子の深さ。ホストが組み立てうる深さの応答を受け取れるようにするための
        /// 値で、これを下回るとホストが正しく返した深い応答まで不正と判定してしまう。
        /// </summary>
        public const int MaxDepth = 100;

        /// <summary>
        /// 要求を1行の本文へ組み立てる。<paramref name="parameters"/> が null のときは
        /// 引数の項目自体を置かない。
        /// </summary>
        public static string SerializeRequest(int id, string method, JsonObject parameters)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 応答の本文を解析し、契約に沿っているかを判定する。成功応答の識別子は
        /// <paramref name="expectedId"/> と一致していなければならない。エラー応答は識別子が
        /// null であることも許す——ホストは要求の識別子を判別できないときや応答が上限を
        /// 超えたときに null を載せる契約であり、ここで弾くとホストが返した理由が失われる。
        /// </summary>
        public static HostResponseParseResult ParseResponse(string message, int expectedId)
        {
            throw new NotImplementedException();
        }
    }
}
