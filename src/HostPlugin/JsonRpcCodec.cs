using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>ホストが応答に載せるエラーコード。</summary>
    public static class JsonRpcErrorCodes
    {
        /// <summary>本文がJSONとして解釈できない。</summary>
        public const int ParseError = -32700;

        /// <summary>要求の構造が契約に合わない。</summary>
        public const int InvalidRequest = -32600;

        /// <summary>method に対応する処理が無い。</summary>
        public const int MethodNotFound = -32601;

        /// <summary>params が契約に合わない。</summary>
        public const int InvalidParams = -32602;

        /// <summary>要求処理で予期しない例外が起きた。</summary>
        public const int InternalError = -32603;

        /// <summary>プロトコル番号が合わない。</summary>
        public const int ProtocolMismatch = -32001;

        /// <summary>要求処理が上限の時間を超えた。</summary>
        public const int RequestTimeout = -32002;

        /// <summary>ハンドシェイクの前に他の要求が来た。</summary>
        public const int HandshakeRequired = -32003;

        /// <summary>入力のメッセージが上限のバイト数を超えた。</summary>
        public const int RequestTooLarge = -32004;

        /// <summary>応答のメッセージが上限のバイト数を超えた。</summary>
        public const int ResponseTooLarge = -32005;
    }

    /// <summary>解析できた要求。</summary>
    public sealed class JsonRpcRequest
    {
        /// <summary>要求の識別子。数値または文字列。</summary>
        public object Id => throw new NotImplementedException();

        /// <summary>呼ぶ処理の名前。</summary>
        public string Method => throw new NotImplementedException();

        /// <summary>
        /// 引数をオブジェクトとして取り出す。省略されていたときは空を渡して真を返し、
        /// オブジェクト以外が与えられていたときは <paramref name="parameters"/> を null にして
        /// 偽を返す。引数の検査は解析でなくディスパッチの段で行うため(それより前に
        /// ハンドシェイクとメソッドの検索がある)、解析はここまでを担う。省略と明示的な null は
        /// 別に扱う(前者は引数なし、後者はオブジェクト以外)ので、生の値は公開しない。
        /// </summary>
        public bool TryGetParams(out IDictionary<string, object> parameters)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>要求を1件解析した結果。</summary>
    public sealed class JsonRpcParseResult
    {
        /// <summary>解析できたかどうか。</summary>
        public bool IsValid => throw new NotImplementedException();

        /// <summary>解析できた要求。<see cref="IsValid"/> が真のときだけ意味を持つ。</summary>
        public JsonRpcRequest Request => throw new NotImplementedException();

        /// <summary>応答に載せる識別子。判別できなかったときは null。</summary>
        public object Id => throw new NotImplementedException();

        /// <summary>エラーコード。<see cref="IsValid"/> が偽のときだけ意味を持つ。</summary>
        public int ErrorCode => throw new NotImplementedException();

        /// <summary>エラーの説明。<see cref="IsValid"/> が偽のときだけ意味を持つ。</summary>
        public string ErrorMessage => throw new NotImplementedException();
    }

    /// <summary>
    /// JSON-RPC 2.0 のサブセットの解析と組み立て。同梱依存を持たないシリアライザを用いる。
    /// </summary>
    public static class JsonRpcCodec
    {
        /// <summary>
        /// 解析でシリアライザに許す本文の文字数。メッセージのバイト数の上限が16MiBで、その本文が
        /// すべて1バイト文字でも 16,777,216 文字にしかならないため、その2倍を採る。
        /// </summary>
        public const int ParseMaxJsonLength = 32 * 1024 * 1024;

        /// <summary>要求を1件解析する。</summary>
        public static JsonRpcParseResult ParseRequest(string line)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 成功の応答を組み立てる。文字数の上限は課さない——応答の大きさは組み立てた本文の
        /// バイト数で判定するため、シリアライザの上限で先に失敗させると、上限超過と
        /// シリアライズできない値の区別が付かなくなる。後者は例外として呼び出し側へ伝える。
        /// </summary>
        public static string SerializeResult(object id, object result)
        {
            throw new NotImplementedException();
        }

        /// <summary>エラーの応答を組み立てる。</summary>
        public static string SerializeError(object id, int code, string message)
        {
            throw new NotImplementedException();
        }
    }
}
