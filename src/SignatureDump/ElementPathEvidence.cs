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

        /// <summary>PMXが1つだけ持つ子。</summary>
        Child,

        /// <summary>リストが並べる要素。</summary>
        Element,
    }

    /// <summary>PMXから受け手へ至る道。</summary>
    public sealed class AccessPath
    {
        public AccessPath(
            AccessPathKind kind, string rowKey, IList<string> parents, string elementType)
        {
            Kind = kind;
            RowKey = rowKey;
            Parents = new ReadOnlyCollection<string>(parents ?? new string[0]);
            ElementType = elementType;
        }

        public AccessPathKind Kind { get; }

        /// <summary>子を得る行、または要素を並べるリストの行。受け手そのものでは null。</summary>
        public string RowKey { get; }

        /// <summary>要素までに辿る親の行。PMXが直に持つリストでは空。</summary>
        public IList<string> Parents { get; }

        /// <summary>要素として扱う型の名前。要素を相手にしない道では null。</summary>
        public string ElementType { get; }
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

            Dictionary<string, AccessPath> paths =
                new Dictionary<string, AccessPath>(StringComparer.Ordinal)
                {
                    { PmxTypeName, new AccessPath(AccessPathKind.Whole, null, null, null) },
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
                Reach(paths, element, collection.SignatureKey, parents);
                IList<string> leaves;
                if (concrete.TryGetValue(element, out leaves))
                {
                    foreach (string leaf in leaves)
                    {
                        Reach(paths, leaf, collection.SignatureKey, parents);
                    }
                }
            }

            foreach (SignatureRecord signature in inventory.Signatures
                .Where(s => string.Equals(
                    TypeDefinitionName.OfElement(s.DeclaringType),
                    PmxTypeName,
                    StringComparison.Ordinal))
                .Where(s => s.MemberKind == MemberKind.Property && s.CanRead && s.Parameters.Count == 0)
                .OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                string value = TypeDefinitionName.OfElement(signature.ValueType);
                string contained;
                if (ValueTypeName.TryElement(signature.ValueType, out contained)
                    || !roleOf.ContainsKey(value)
                    || paths.ContainsKey(value))
                {
                    continue;
                }

                paths.Add(value, new AccessPath(AccessPathKind.Child, signature.Key, null, null));
            }

            return new ReadOnlyDictionary<string, AccessPath>(paths);
        }

        /// <summary>その道で辿り着く一歩がリストの段かどうか。</summary>
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

        /// <summary>その型へ至る道をまだ持っていなければ覚える。</summary>
        private static void Reach(
            IDictionary<string, AccessPath> paths,
            string type,
            string rowKey,
            IList<string> parents)
        {
            if (paths.ContainsKey(type))
            {
                return;
            }

            paths.Add(type, new AccessPath(AccessPathKind.Element, rowKey, parents, type));
        }
    }
}
