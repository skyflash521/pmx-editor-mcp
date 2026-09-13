using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>PMXから受け手へ至る道の種別。</summary>
    public enum AccessPathKind
    {
        /// <summary>受け手そのもの。</summary>
        Whole,

        /// <summary>PMXから単数の段だけを辿った先。相手は1つに決まる。</summary>
        Child,

        /// <summary>途中にリストの段を挟んだ先。相手は集合になる。</summary>
        Element,
    }

    /// <summary>PMXから受け手へ至る道。</summary>
    public sealed class AccessPath
    {
        public AccessPath(
            AccessPathKind kind,
            string rowKey,
            IList<string> parents,
            bool listed,
            string elementType,
            string ownerType = null)
        {
            Kind = kind;
            RowKey = rowKey;
            Parents = new ReadOnlyCollection<string>(parents ?? new string[0]);
            Listed = listed;
            ElementType = elementType;
            OwnerType = ownerType;
        }

        public AccessPathKind Kind { get; }

        /// <summary>最後の一歩の行。受け手そのものでは null。</summary>
        public string RowKey { get; }

        /// <summary>最後の一歩までに辿る行。</summary>
        public IList<string> Parents { get; }

        /// <summary>最後の一歩がリストの段か。偽ならそのプロパティを1つ辿る段。</summary>
        public bool Listed { get; }

        /// <summary>相手にする型の名前。受け手そのものでは null。</summary>
        public string ElementType { get; }

        /// <summary>
        /// 最後の一歩を直に持つ型の名前。その型へハンドルが発行されうる道だけが持ち、ほかは null。
        /// </summary>
        public string OwnerType { get; }
    }

    /// <summary>
    /// 操作対象型の受け手がPMXのどこに居るかを、型役割表と公開API列挙から導く。道は導けるので
    /// 正本へ書かない。
    /// </summary>
    public static class ElementPathEvidence
    {
        /// <summary>道が始まる型。</summary>
        public const string PmxTypeName = "PEPlugin.Pmx.IPXPmx";

        /// <summary>型の名前から、その型の受け手へ至る道。辿り着けない型は持たない。</summary>
        public static IDictionary<string, AccessPath> Resolve(
            InventoryRecord inventory, TypeRoleTable roles)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            IDictionary<string, TypeRole> roleOf = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t.Role, StringComparer.Ordinal);
            IDictionary<string, IList<string>> concrete =
                ElementCollectionEvidence.ConcreteTypes(inventory, roleOf);
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures.ToDictionary(
                s => s.Key, s => s, StringComparer.Ordinal);
            ISet<string> issued = Issued(inventory, roles);

            Dictionary<string, AccessPath> paths =
                new Dictionary<string, AccessPath>(StringComparer.Ordinal)
                {
                    { PmxTypeName, new AccessPath(AccessPathKind.Whole, null, null, false, null) },
                };

            foreach (ElementCollectionRecord collection in roles.Collections
                .Where(c => c.Owns)
                .OrderBy(c => c.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(collection.SignatureKey, out signature)
                    || collection.OwnerPath.Count == 0)
                {
                    continue;
                }

                string element = TypeDefinitionName.OfElement(
                    ValueTypeName.Contained(signature.ValueType));
                IList<string> parents = collection.OwnerPath
                    .Take(collection.OwnerPath.Count - 1)
                    .ToList();
                string owner = Owner(signatures, issued, collection.SignatureKey);
                Reach(paths, element, collection.SignatureKey, parents, owner);
                IList<string> leaves;
                if (concrete.TryGetValue(element, out leaves))
                {
                    foreach (string leaf in leaves)
                    {
                        Reach(paths, leaf, collection.SignatureKey, parents, owner);
                    }
                }
            }

            Walk(inventory, roleOf, signatures, issued, paths);

            return new ReadOnlyDictionary<string, AccessPath>(paths);
        }

        /// <summary>
        /// 受け手へ至る道が辿る行のキー。ここに在る行は、その先の型のツールが相手を得るのに通る
        /// 経路そのもので、値として写す相手ではない。
        /// </summary>
        public static ISet<string> Traversed(InventoryRecord inventory, TypeRoleTable roles)
        {
            HashSet<string> traversed = new HashSet<string>(StringComparer.Ordinal);
            foreach (AccessPath path in Resolve(inventory, roles).Values)
            {
                if (path.RowKey != null)
                {
                    traversed.Add(path.RowKey);
                }

                foreach (string parent in path.Parents)
                {
                    traversed.Add(parent);
                }
            }

            return traversed;
        }

        /// <summary>その道で辿る一歩がリストの段かどうか。</summary>
        public static bool Listed(IDictionary<string, SignatureRecord> signatures, string rowKey)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            SignatureRecord signature;
            string element;

            return signatures.TryGetValue(rowKey, out signature)
                && ValueTypeName.TryElement(signature.ValueType, out element);
        }

        /// <summary>
        /// ハンドルを発行されうる型。所有するリストの要素になる型がこれに当たる——そこへ加える
        /// ために作られた生成物は、加わるまで台帳が保つ。
        /// </summary>
        public static ISet<string> Issued(InventoryRecord inventory, TypeRoleTable roles)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            IDictionary<string, TypeRole> roleOf = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t.Role, StringComparer.Ordinal);
            IDictionary<string, IList<string>> concrete =
                ElementCollectionEvidence.ConcreteTypes(inventory, roleOf);
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures.ToDictionary(
                s => s.Key, s => s, StringComparer.Ordinal);
            HashSet<string> issued = new HashSet<string>(StringComparer.Ordinal);
            foreach (ElementCollectionRecord collection in roles.Collections.Where(c => c.Owns))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(collection.SignatureKey, out signature))
                {
                    continue;
                }

                string element = TypeDefinitionName.OfElement(
                    ValueTypeName.Contained(signature.ValueType));
                issued.Add(element);
                IList<string> leaves;
                if (concrete.TryGetValue(element, out leaves))
                {
                    foreach (string leaf in leaves)
                    {
                        issued.Add(leaf);
                    }
                }
            }

            foreach (HandleIssuanceRecord issuance in roles.Issuances.Where(i => i.Issues))
            {
                SignatureRecord signature;
                if (signatures.TryGetValue(issuance.SignatureKey, out signature))
                {
                    issued.Add(TypeDefinitionName.OfElement(
                        ValueTypeName.Contained(signature.ValueType)));
                }
            }

            return issued;
        }

        /// <summary>
        /// その一歩を直に持つ型。親をハンドルで指せない道では null——親へハンドルが発行されない
        /// 道である。
        /// </summary>
        public static string Owner(
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> issued,
            string rowKey)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (issued == null)
            {
                throw new ArgumentNullException(nameof(issued));
            }

            SignatureRecord signature;
            if (rowKey == null || !signatures.TryGetValue(rowKey, out signature))
            {
                return null;
            }

            string owner = TypeDefinitionName.OfElement(signature.DeclaringType);

            return issued.Contains(owner) ? owner : null;
        }

        /// <summary>
        /// まだ道の無い型へ届く道を覚える。同じ型へ届く道が2つあれば、一歩の少ない方を採る。
        /// </summary>
        private static void Walk(
            InventoryRecord inventory,
            IDictionary<string, TypeRole> roleOf,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> issued,
            IDictionary<string, AccessPath> paths)
        {
            IDictionary<string, IList<SignatureRecord>> members = inventory.Signatures
                .Where(s => s.MemberKind == MemberKind.Property
                    && s.CanRead
                    && s.Parameters.Count == 0)
                .GroupBy(s => TypeDefinitionName.OfElement(s.DeclaringType), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => (IList<SignatureRecord>)g.OrderBy(s => s.Key, StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);

            for (int steps = 0; steps <= paths.Values.Max(Steps); steps++)
            {
                foreach (string from in paths
                    .Where(p => Steps(p.Value) == steps)
                    .Select(p => p.Key)
                    .OrderBy(t => t, StringComparer.Ordinal)
                    .ToList())
                {
                    IList<SignatureRecord> listed;
                    if (!members.TryGetValue(from, out listed))
                    {
                        continue;
                    }

                    foreach (SignatureRecord signature in listed)
                    {
                        string value = TypeDefinitionName.OfElement(signature.ValueType);
                        string element;
                        if (ValueTypeName.TryElement(signature.ValueType, out element)
                            || !roleOf.ContainsKey(value)
                            || paths.ContainsKey(value))
                        {
                            continue;
                        }

                        paths.Add(
                            value, Extend(paths[from], signatures, issued, signature.Key, value));
                    }
                }
            }
        }

        /// <summary>その道が辿る一歩の数。</summary>
        private static int Steps(AccessPath path)
        {
            return path.Parents.Count + (path.RowKey == null ? 0 : 1);
        }

        /// <summary>その道の先へ、単数の段を1つ延ばした道。</summary>
        private static AccessPath Extend(
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> issued,
            string rowKey,
            string type)
        {
            List<string> parents = new List<string>(path.Parents);
            if (path.RowKey != null)
            {
                parents.Add(path.RowKey);
            }

            return new AccessPath(
                path.Kind == AccessPathKind.Element ? AccessPathKind.Element : AccessPathKind.Child,
                rowKey,
                parents,
                false,
                type,
                Owner(signatures, issued, rowKey));
        }

        /// <summary>その型へ至る道をまだ持っていなければ覚える。</summary>
        private static void Reach(
            IDictionary<string, AccessPath> paths,
            string type,
            string rowKey,
            IList<string> parents,
            string owner)
        {
            if (paths.ContainsKey(type))
            {
                return;
            }

            paths.Add(
                type, new AccessPath(AccessPathKind.Element, rowKey, parents, true, type, owner));
        }
    }
}
