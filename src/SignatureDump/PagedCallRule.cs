using System;

namespace PmxEditorMcp.SignatureDump
{
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
