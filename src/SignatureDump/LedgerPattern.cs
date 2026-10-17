using System;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力台帳のまとめて指す書き方の行が指す公開シグネチャの決め方。</summary>
    public static class LedgerPattern
    {
        public const string BuilderType = "PEPlugin.IPEBuilder";

        private static readonly Regex Spelled = new Regex(
            "(?<![A-Za-z0-9_.])((?:[A-Za-z_][A-Za-z0-9_]*\\.)+)\\*", RegexOptions.CultureInvariant);

        /// <summary>
        /// 対象の欄が `*` で指す名前空間の、末尾に点を付けた名前。1つに決まらなければ
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static string Prefix(CapabilityRecord row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            MatchCollection found = Spelled.Matches(row.Target);
            if (found.Count != 1)
            {
                throw new InvalidOperationException(
                    row.Id + " の対象が `*` で指す名前空間を1つに決められない: " + row.Target);
            }

            return found[0].Groups[1].Value;
        }

        /// <summary>対象の欄がビルダを挙げていれば、その名前空間の型を作るビルダのメンバーも指す。</summary>
        public static bool CoversCreation(CapabilityRecord row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            return row.Target.IndexOf(
                BuilderType.Substring(BuilderType.LastIndexOf('.') + 1),
                StringComparison.Ordinal) >= 0;
        }

        public static bool Covers(CapabilityRecord row, SignatureRecord signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            string prefix = Prefix(row);
            if (signature.DeclaringType.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }

            return CoversCreation(row)
                && string.Equals(signature.DeclaringType, BuilderType, StringComparison.Ordinal)
                && signature.ValueType != null
                && signature.ValueType.StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}
