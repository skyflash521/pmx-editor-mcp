using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

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

        /// <summary>入力が上限を超えた(本文のバイト数、または解析前に数える構造トークン)。</summary>
        public const int RequestTooLarge = -32004;

        /// <summary>応答のメッセージが上限のバイト数を超えた。</summary>
        public const int ResponseTooLarge = -32005;
    }

    /// <summary>解析できた要求。</summary>
    public sealed class JsonRpcRequest
    {
        private readonly object _parameters;
        private readonly bool _hasParameters;

        internal JsonRpcRequest(object id, string method, object parameters, bool hasParameters)
        {
            Id = id;
            Method = method;
            _parameters = parameters;
            _hasParameters = hasParameters;
        }

        /// <summary>要求の識別子。数値または文字列。</summary>
        public object Id { get; }

        /// <summary>呼ぶ処理の名前。</summary>
        public string Method { get; }

        /// <summary>
        /// 引数をオブジェクトとして取り出す。省略されていたときは空を渡して真を返し、
        /// オブジェクト以外が与えられていたときは <paramref name="parameters"/> を null にして
        /// 偽を返す。引数の検査は解析でなくディスパッチの段で行うため(それより前に
        /// ハンドシェイクとメソッドの検索がある)、解析はここまでを担う。省略と明示的な null は
        /// 別に扱う(前者は引数なし、後者はオブジェクト以外)ので、生の値は公開しない。
        /// </summary>
        public bool TryGetParams(out IDictionary<string, object> parameters)
        {
            if (!_hasParameters)
            {
                parameters = new Dictionary<string, object>();
                return true;
            }

            IDictionary<string, object> given = _parameters as IDictionary<string, object>;
            if (given == null)
            {
                parameters = null;
                return false;
            }

            parameters = given;
            return true;
        }
    }

    /// <summary>要求を1件解析した結果。</summary>
    public sealed class JsonRpcParseResult
    {
        private JsonRpcParseResult(JsonRpcRequest request, object id, int errorCode, string errorMessage)
        {
            Request = request;
            Id = id;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>解析できたかどうか。</summary>
        public bool IsValid => Request != null;

        /// <summary>解析できた要求。<see cref="IsValid"/> が真のときだけ意味を持つ。</summary>
        public JsonRpcRequest Request { get; }

        /// <summary>応答に載せる識別子。判別できなかったときは null。</summary>
        public object Id { get; }

        /// <summary>エラーコード。<see cref="IsValid"/> が偽のときだけ意味を持つ。</summary>
        public int ErrorCode { get; }

        /// <summary>エラーの説明。<see cref="IsValid"/> が偽のときだけ意味を持つ。</summary>
        public string ErrorMessage { get; }

        internal static JsonRpcParseResult Parsed(JsonRpcRequest request)
        {
            return new JsonRpcParseResult(request, request.Id, 0, null);
        }

        internal static JsonRpcParseResult Rejected(object id, int errorCode, string errorMessage)
        {
            return new JsonRpcParseResult(null, id, errorCode, errorMessage);
        }
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

        /// <summary>
        /// シリアライザに許す入れ子の深さ。解析と組み立ての両方に効く。深さは値の再帰の段数で数え、
        /// オブジェクト・配列だけでなくその中の文字列や数値も1段として数える(トップレベルの値が
        /// 1段目)。フレームワークの暗黙の既定に依らず、契約としてここで固定する。
        /// </summary>
        public const int JsonRecursionLimit = 100;

        /// <summary>
        /// 解析の前に数える構造トークン(オブジェクトと配列の開き括弧、要素とメンバーの区切りの
        /// コンマ。文字列の中にあるものは数えない)の上限。本文のバイト数の上限だけでは、要素の
        /// 数に歯止めが無い——上限いっぱいの本文へ小さなメンバーを並べれば数百万のオブジェクトを
        /// 作らせられ、エディタと同じプロセスのメモリを圧迫できる。値は解析で作るオブジェクトの
        /// 量から決める。1トークンあたりの取り分が最も大きいのはメンバーを1つだけ持つ辞書の鎖で、
        /// この形で上限いっぱいまで詰めて実測すると、解析後に保持されるのが約56MB、解析中に確保する
        /// 総量が約95MBだった。上限が無いと、同じ形で本文のバイト数の上限いっぱいまで詰めたときに
        /// 保持が約725MB・確保総量が約1,325MBになる。ツール契約が定める要素数の上限は、この上限の
        /// 内側で定める。
        /// </summary>
        public const int ParseStructureTokenLimit = 200000;

        /// <summary>要求と応答の jsonrpc に固定で置く値。</summary>
        private const string ProtocolVersion = "2.0";

        /// <summary>要求を1件解析する。</summary>
        public static JsonRpcParseResult ParseRequest(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            // 解析より前に構造の量を見る。解析してから数えると、数えるために作ったオブジェクトで
            // すでにメモリを使ってしまう。
            if (ExceedsStructureTokenLimit(line))
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.RequestTooLarge, "要求の構造の量が上限を超えている。");
            }

            // 空(空白のみのものを含む)の本文は、シリアライザからは null リテラルと同じ結果に
            // 見えるため、解析へ渡す前に構文不正として分ける。
            if (line.Trim().Length == 0)
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.ParseError, "本文が空でJSONとして解釈できない。");
            }

            object parsed;
            try
            {
                parsed = CreateSerializer(ParseMaxJsonLength).DeserializeObject(line);
            }
            catch (Exception exception) when (IsDeserializeFailure(exception))
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.ParseError, "本文をJSONとして解釈できない。");
            }

            IDictionary<string, object> request = parsed as IDictionary<string, object>;
            if (request == null)
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.InvalidRequest, "要求はJSONオブジェクトでなければならない。");
            }

            // 識別子を先に判定する。応答に要求の識別子を載せられるかがこれで決まる。
            object id;
            if (!request.TryGetValue("id", out id) || !IsAllowedId(id))
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.InvalidRequest, "id は数値または文字列で、省略できない。");
            }

            object version;
            if (!request.TryGetValue("jsonrpc", out version)
                || !ProtocolVersion.Equals(version as string, StringComparison.Ordinal))
            {
                return JsonRpcParseResult.Rejected(
                    id, JsonRpcErrorCodes.InvalidRequest, "jsonrpc は 2.0 でなければならない。");
            }

            object methodValue;
            if (!request.TryGetValue("method", out methodValue))
            {
                return JsonRpcParseResult.Rejected(
                    id, JsonRpcErrorCodes.InvalidRequest, "method は省略できない。");
            }

            string method = methodValue as string;
            if (method == null)
            {
                return JsonRpcParseResult.Rejected(
                    id, JsonRpcErrorCodes.InvalidRequest, "method は文字列でなければならない。");
            }

            object parameters;
            bool hasParameters = request.TryGetValue("params", out parameters);
            return JsonRpcParseResult.Parsed(new JsonRpcRequest(id, method, parameters, hasParameters));
        }

        /// <summary>
        /// 成功の応答を組み立てる。文字数の上限は課さない——応答の大きさは組み立てた本文の
        /// バイト数で判定するため、シリアライザの上限で先に失敗させると、上限超過と
        /// シリアライズできない値の区別が付かなくなる。後者は例外として呼び出し側へ伝える。
        /// </summary>
        public static string SerializeResult(object id, object result)
        {
            Dictionary<string, object> response = new Dictionary<string, object>
            {
                { "jsonrpc", ProtocolVersion },
                { "id", id },
                { "result", result },
            };

            return CreateSerializer(int.MaxValue).Serialize(response);
        }

        /// <summary>エラーの応答を組み立てる。</summary>
        public static string SerializeError(object id, int code, string message)
        {
            Dictionary<string, object> error = new Dictionary<string, object>
            {
                { "code", code },
                { "message", message },
            };
            Dictionary<string, object> response = new Dictionary<string, object>
            {
                { "jsonrpc", ProtocolVersion },
                { "id", id },
                { "error", error },
            };

            return CreateSerializer(int.MaxValue).Serialize(response);
        }

        /// <summary>
        /// 識別子として受理する型かどうかを判定する。数値の側の並びは、シリアライザがJSONの数値を
        /// 実体化するときに使う型(Int32 から Int64・Decimal・Double へ落ちる順)をすべて挙げた
        /// もので、1つでも欠けると妥当な識別子を構造不正として弾いてしまう。
        /// </summary>
        private static bool IsAllowedId(object id)
        {
            return id is string || id is int || id is long || id is decimal || id is double;
        }

        /// <summary>
        /// 本文をJSONとして解釈できなかったことを表す例外かどうかを判定する。要求の本文は接続の
        /// 相手が自由に作れるため、シリアライザが投げる例外の型を1つに賭けず、解釈の失敗として
        /// ありうる型をまとめて構文不正へ寄せる(メモリ不足などの続行できない失敗は通す)。
        /// </summary>
        private static bool IsDeserializeFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is FormatException
                || exception is OverflowException
                || exception is InvalidOperationException;
        }

        /// <summary>
        /// 構造トークンの数が上限を超えるかを、解析せずに1回の走査で判定する。文字列の中の記号は
        /// 数えない——値として置かれた括弧やコンマはオブジェクトを作らないため。
        /// </summary>
        private static bool ExceedsStructureTokenLimit(string line)
        {
            int tokens = 0;
            bool inString = false;
            bool escaped = false;

            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                }
                else if (current == '{' || current == '[' || current == ',')
                {
                    tokens++;
                    if (tokens > ParseStructureTokenLimit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static JavaScriptSerializer CreateSerializer(int maxJsonLength)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = maxJsonLength;
            serializer.RecursionLimit = JsonRecursionLimit;
            return serializer;
        }
    }
}
