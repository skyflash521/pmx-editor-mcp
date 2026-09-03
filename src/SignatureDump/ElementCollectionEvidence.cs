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
        /// <summary>所有の経路が始まる型。</summary>
        public static readonly IList<string> OwnershipRoots = new ReadOnlyCollection<string>(
            new[] { "PEPlugin.Pmx.IPXPmx", "PEPlugin.Vmd.IPEVmd", "PEPlugin.Vme.IPEVmeObject" });

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

        /// <summary>
        /// 役割対象の型の名前から、それを継承する葉のインターフェースの名前。葉は、それを継承する
        /// 役割対象のインターフェースが一つも無いものをいう。継承するインターフェースが一つも無い型は
        /// 現れない。実体を持ちうるクラスは、継承されていても具象型の選択肢から外れないので数えない。
        /// </summary>
        public static IDictionary<string, IList<string>> ConcreteTypes(
            InventoryRecord inventory, IDictionary<string, TypeRole> roles)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            Dictionary<string, ISet<string>> children =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (TypeRecord type in inventory.Types.Concat(inventory.ReferencedTypes))
            {
                string name = TypeDefinitionName.Of(type.Name);
                if (type.Kind != TypeKind.Interface || !roles.ContainsKey(name))
                {
                    continue;
                }

                foreach (string baseType in type.BaseTypes.Select(TypeDefinitionName.Of))
                {
                    if (!roles.ContainsKey(baseType) || string.Equals(baseType, name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ISet<string> kinds;
                    if (!children.TryGetValue(baseType, out kinds))
                    {
                        kinds = new HashSet<string>(StringComparer.Ordinal);
                        children[baseType] = kinds;
                    }

                    kinds.Add(name);
                }
            }

            Dictionary<string, IList<string>> leaves =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (string baseType in children.Keys)
            {
                leaves.Add(
                    baseType,
                    new ReadOnlyCollection<string>(Descendants(children, baseType)
                        .Where(d => !children.ContainsKey(d))
                        .OrderBy(d => d, StringComparer.Ordinal)
                        .ToList()));
            }

            return new ReadOnlyDictionary<string, IList<string>>(leaves);
        }

        /// <summary>その型を直に、または間に型を挟んで継承する型。</summary>
        private static ISet<string> Descendants(
            IDictionary<string, ISet<string>> children, string baseType)
        {
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> pending = new Queue<string>(children[baseType]);
            while (pending.Count != 0)
            {
                string name = pending.Dequeue();
                if (!found.Add(name))
                {
                    continue;
                }

                ISet<string> kinds;
                if (children.TryGetValue(name, out kinds))
                {
                    foreach (string kind in kinds)
                    {
                        pending.Enqueue(kind);
                    }
                }
            }

            return found;
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
