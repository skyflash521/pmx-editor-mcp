using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 役割対象の型の実体を並べるリストのプロパティを列挙から導く。そのリストが要素を所有するのか、
    /// 他所が所有する要素を指すだけなのかは列挙からは決まらないので、ここでは決めない。
    /// </summary>
    public static class ElementCollectionEvidence
    {
        private const string ListHead = "System.Collections.Generic.IList<";

        /// <summary>
        /// 提供対象のうち、役割対象の型が宣言する引数の無い取得プロパティで、値の型が役割対象の型の
        /// リストであるものと、その要素の型。
        /// </summary>
        public static IDictionary<string, string> Candidates(
            InventoryRecord inventory,
            IDictionary<string, TypeRole> roles,
            ISet<string> provided)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            Dictionary<string, string> candidates =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures
                .Where(s => provided.Contains(s.Key)))
            {
                string element = ElementTypeName(signature);
                if (element != null
                    && roles.ContainsKey(TypeDefinitionName.Of(signature.DeclaringType))
                    && roles.ContainsKey(element))
                {
                    candidates.Add(signature.Key, element);
                }
            }

            return new ReadOnlyDictionary<string, string>(candidates);
        }

        /// <summary>リストの要素の型。リストを返す取得プロパティでなければ null。</summary>
        private static string ElementTypeName(SignatureRecord signature)
        {
            if (signature.MemberKind != MemberKind.Property
                || !signature.CanRead
                || signature.Parameters.Count != 0
                || !signature.ValueType.StartsWith(ListHead, StringComparison.Ordinal)
                || !signature.ValueType.EndsWith(">", StringComparison.Ordinal))
            {
                return null;
            }

            return TypeDefinitionName.Of(signature.ValueType.Substring(
                ListHead.Length, signature.ValueType.Length - ListHead.Length - 1));
        }
    }
}
