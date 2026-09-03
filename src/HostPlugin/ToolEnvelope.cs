using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// ツールの結果を包む形。ドメインの失敗はJSON-RPCの error ではなくこの包みで返す——error は
    /// 要求の解釈・ディスパッチ・応答生成といったホスト基盤の異常のために空けておく。
    /// </summary>
    public static class ToolEnvelope
    {
        /// <summary>範囲外の位置を指した。</summary>
        public const string IndexOutOfRange = "TOOL_INDEX_OUT_OF_RANGE";

        /// <summary>引数の値が不正。</summary>
        public const string InvalidArgument = "TOOL_INVALID_ARGUMENT";

        /// <summary>ハンドルが不正。</summary>
        public const string InvalidHandle = "TOOL_INVALID_HANDLE";

        /// <summary>危険操作の確認が無い。</summary>
        public const string ConfirmRequired = "TOOL_CONFIRM_REQUIRED";

        /// <summary>現在の状態・提供範囲で適用できない。</summary>
        public const string NotApplicable = "TOOL_NOT_APPLICABLE";

        /// <summary>実行に失敗した。</summary>
        public const string OperationFailed = "TOOL_OPERATION_FAILED";

        /// <summary>応答が応答サイズ予算に収まらない。</summary>
        public const string ResponseTooLarge = "TOOL_RESPONSE_TOO_LARGE";

        /// <summary>要求が要求サイズ予算に収まらない。</summary>
        public const string RequestTooLarge = "TOOL_REQUEST_TOO_LARGE";

        private const string OkName = "ok";

        private const string ValueName = "value";

        private const string ErrorName = "error";

        private const string CodeName = "code";

        private const string MessageName = "message";

        private const string WarningsName = "warnings";

        private static readonly ReadOnlyCollection<string> Codes = new ReadOnlyCollection<string>(
            new[]
            {
                IndexOutOfRange, InvalidArgument, InvalidHandle, ConfirmRequired, NotApplicable,
                OperationFailed, ResponseTooLarge, RequestTooLarge,
            });

        /// <summary>ツールが返しうるエラーコード。閉じた集合とする。</summary>
        public static IList<string> ErrorCodes
        {
            get { return Codes; }
        }

        /// <summary>成功の包み。値が無いツールは null を渡す。</summary>
        public static IDictionary<string, object> Success(
            object value, IEnumerable<string> warnings = null)
        {
            Dictionary<string, object> envelope = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { OkName, true },
                { ValueName, value },
            };
            AddWarnings(envelope, warnings);

            return envelope;
        }

        /// <summary>失敗の包み。コードは閉じた集合のいずれかでなければならない。</summary>
        public static IDictionary<string, object> Failure(
            string code, string message, IEnumerable<string> warnings = null)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!Codes.Contains(code))
            {
                throw new ArgumentException("知らないエラーコード: " + code, nameof(code));
            }

            if (message.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(message));
            }

            Dictionary<string, object> envelope = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { OkName, false },
                {
                    ErrorName,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { CodeName, code },
                        { MessageName, message },
                    }
                },
            };
            AddWarnings(envelope, warnings);

            return envelope;
        }

        /// <summary>
        /// 警告は在るときだけ載せる。空の配列を載せると、警告が無いことと区別できない形が2つになる。
        /// </summary>
        private static void AddWarnings(
            IDictionary<string, object> envelope, IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                return;
            }

            string[] listed = warnings.ToArray();
            if (listed.Length == 0)
            {
                return;
            }

            if (listed.Any(w => string.IsNullOrEmpty(w) || w.Trim().Length == 0))
            {
                throw new ArgumentException("空の警告は載せられない。", nameof(warnings));
            }

            envelope.Add(WarningsName, listed);
        }
    }
}
