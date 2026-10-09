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
    public static partial class ToolEnvelope
    {
        /// <summary>その包みが成功かどうか。</summary>
        public static bool Succeeded(IDictionary<string, object> envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            object ok;

            return envelope.TryGetValue(OkName, out ok) && Equals(ok, true);
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
