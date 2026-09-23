using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 値を返さずに位置の並びを1つ書き込むメンバーの、書いた並びを読み返すメンバーを引く。同じ型に、名前の
    /// Set を Get に替えた、引数を取らず位置の並びを返すメンバーがあればそれを採る。
    /// </summary>
    public static class ReadBackRule
    {
        private const string VoidTypeName = "System.Void";

        private const string PositionsTypeName = "System.Int32[]";

        private const string SetPrefix = "Set";

        private const string GetPrefix = "Get";

        /// <summary>読み返すメンバーの行キー。無ければ null。</summary>
        public static string Of(SignatureRecord signature, IDictionary<string, SignatureRecord> signatures)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (signature.MemberKind != MemberKind.Method
                || !string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                || !signature.MemberName.StartsWith(SetPrefix, StringComparison.Ordinal)
                || signature.Parameters.Count != 1
                || signature.Parameters[0].Direction != ParameterDirection.In
                || !string.Equals(signature.Parameters[0].TypeName, PositionsTypeName, StringComparison.Ordinal))
            {
                return null;
            }

            string getter = GetPrefix + signature.MemberName.Substring(SetPrefix.Length);
            SignatureRecord found = signatures.Values.FirstOrDefault(
                s => s.MemberKind == MemberKind.Method
                    && string.Equals(s.DeclaringType, signature.DeclaringType, StringComparison.Ordinal)
                    && string.Equals(s.MemberName, getter, StringComparison.Ordinal)
                    && s.Parameters.Count == 0
                    && string.Equals(s.ValueType, signature.Parameters[0].TypeName, StringComparison.Ordinal));

            return found == null ? null : found.Key;
        }
    }
}
