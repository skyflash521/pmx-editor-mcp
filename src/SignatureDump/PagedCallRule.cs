using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 応答を位置と件数で切り出す行を決める。一列の並びを丸ごと返す行がそれで、そのツールは offset と
    /// limit を受け取り、総数と切り出した並びと続きの位置を返す。返したものをハンドルとして預ける行と、
    /// 1つの文字列へ詰めて返すバイトの並びは当たらない。
    /// </summary>
    public static class PagedCallRule
    {
        private const string ArraySuffix = "[]";

        private const string ByteTypeName = "System.Byte";

        /// <summary>その行の応答を切り出すか。</summary>
        public static bool Pages(ToolMapRow row, SignatureRecord signature)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            string valueType = signature.ValueType;
            if (!valueType.EndsWith(ArraySuffix, StringComparison.Ordinal)
                || HandleIssuanceEvidence.Issues(row, signature))
            {
                return false;
            }

            string element = valueType.Substring(0, valueType.Length - ArraySuffix.Length);

            return !element.EndsWith(ArraySuffix, StringComparison.Ordinal)
                && !string.Equals(element, ByteTypeName, StringComparison.Ordinal);
        }
    }
}
