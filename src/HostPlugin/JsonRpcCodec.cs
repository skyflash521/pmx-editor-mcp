using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
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

        public string Method { get; }

        /// <summary>
        /// 引数をオブジェクトとして取り出す。省略されていたときは空を渡して真を返し、
        /// オブジェクト以外が与えられていたときは <paramref name="parameters"/> を null にして
        /// 偽を返す。
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

    public sealed class JsonRpcParseResult
    {
        private JsonRpcParseResult(JsonRpcRequest request, object id, int errorCode, string errorMessage)
        {
            Request = request;
            Id = id;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

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
        public const int ParseMaxJsonLength = 32 * 1024 * 1024;

        /// <summary>
        /// シリアライザに許す入れ子の深さ。解析と組み立ての両方に効く。深さは値の再帰の段数で数え、
        /// オブジェクト・配列だけでなくその中の文字列や数値も1段として数える(トップレベルの値が
        /// 1段目)。
        /// </summary>
        public const int JsonRecursionLimit = 100;

        /// <summary>
        /// 解析の前に数える構造トークンの上限。ツール契約が定める要素数の上限は、この上限の
        /// 内側で定める。
        /// </summary>
        public const int ParseStructureTokenLimit = 200000;

        private const string ProtocolVersion = "2.0";

        public static JsonRpcParseResult ParseRequest(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (ExceedsStructureTokenLimit(line))
            {
                return JsonRpcParseResult.Rejected(
                    null, JsonRpcErrorCodes.RequestTooLarge, "要求の構造の量が上限を超えている。");
            }

            // 空白だけの本文は、シリアライザでは null リテラルと同じ結果になる。
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
        /// 成功の応答を組み立てる。シリアライズできない値は例外として呼び出し側へ伝える。
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
        /// 識別子として受理する型かどうかを判定する。数値の側は、シリアライザがJSONの数値を
        /// 実体化するときに使う型をすべて挙げたもの。
        /// </summary>
        private static bool IsAllowedId(object id)
        {
            return id is string || id is int || id is long || id is decimal || id is double;
        }

        private static bool IsDeserializeFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is FormatException
                || exception is OverflowException
                || exception is InvalidOperationException;
        }

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
