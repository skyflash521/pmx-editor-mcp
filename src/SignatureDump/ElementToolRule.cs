using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 所有するリストの要素の型が持つ、追加と削除と、在る要素をハンドルで指すツールの名前を決める。
    /// </summary>
    public static class ElementToolRule
    {
        /// <summary>その要素の型の追加と削除のツールの名前。</summary>
        public static IEnumerable<string> Of(TypeRoleRecord element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return new[] { ToolVerb.Add, ToolVerb.Remove }
                .Select(v => ToolNameRule.OfRole(element, v));
        }

        /// <summary>
        /// その要素の型を、親のハンドルと位置で指すツールの名前。並びに在る要素をハンドルの台帳へ
        /// 預けて番号を返す。
        /// </summary>
        public static string Holding(TypeRoleRecord element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return ToolNameRule.OfRole(element, ToolVerb.Hold);
        }

        /// <summary>
        /// 能力対応表の行が持ち込む、要素をハンドルで指すツールの名前。
        /// <see cref="Holdings"/> が選んだ行の分だけ立つ。
        /// </summary>
        public static ISet<string> HoldingNames(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            TypeRoleTable roles,
            ISet<string> made,
            ISet<string> issued)
        {
            return new HashSet<string>(
                Holdings(map, signatures, roles, made, issued).Values.Select(Holding),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 行キーから、その要素をハンドルで指すツールが相手にする要素の型の役割を引く表。名前を
        /// 持ち込むリストの行だけを持つ。
        /// <paramref name="made"/> はどれかの行が作ると述べる型の名前、
        /// <paramref name="issued"/> はハンドルが出る型の名前である。
        /// </summary>
        public static IDictionary<string, TypeRoleRecord> Holdings(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            TypeRoleTable roles,
            ISet<string> made,
            ISet<string> issued)
        {
            if (made == null)
            {
                throw new ArgumentNullException(nameof(made));
            }

            if (issued == null)
            {
                throw new ArgumentNullException(nameof(issued));
            }

            Dictionary<string, TypeRoleRecord> holdings =
                new Dictionary<string, TypeRoleRecord>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TypeRoleRecord> owned in Elements(map, signatures, roles))
            {
                if ((made.Contains(owned.Value.TypeName)
                        && RootedInPmx(signatures, roles, owned.Key))
                    || ElementPathEvidence.Owner(signatures, issued, owned.Key) == null)
                {
                    continue;
                }

                holdings.Add(owned.Key, owned.Value);
            }

            return holdings;
        }

        private static bool RootedInPmx(
            IDictionary<string, SignatureRecord> signatures, TypeRoleTable roles, string rowKey)
        {
            ElementCollectionRecord collection = roles.Collections
                .First(c => string.Equals(c.SignatureKey, rowKey, StringComparison.Ordinal));
            SignatureRecord root;

            return !signatures.TryGetValue(collection.OwnerPath[0], out root)
                || string.Equals(
                    TypeDefinitionName.OfElement(root.DeclaringType),
                    ElementPathEvidence.PmxTypeName,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// 能力対応表の行が持ち込む追加と削除のツールの名前。所有するリストの行だけが持ち込み、
        /// 要素の型が独立したツールを持たない役割なら持ち込まない。
        /// </summary>
        public static ISet<string> Names(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            TypeRoleTable roles)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TypeRoleRecord> owned in Elements(map, signatures, roles))
            {
                named.UnionWith(Of(owned.Value));
            }

            return named;
        }

        /// <summary>
        /// 行キーから、そのリストが並べる要素の型の役割を引く表。所有するリストの行だけを持つ。
        /// </summary>
        public static IDictionary<string, TypeRoleRecord> Elements(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            TypeRoleTable roles)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            ISet<string> owning = new HashSet<string>(
                roles.Collections.Where(c => c.Owns).Select(c => c.SignatureKey),
                StringComparer.Ordinal);
            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);

            Dictionary<string, TypeRoleRecord> elements =
                new Dictionary<string, TypeRoleRecord>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows.Where(r => owning.Contains(r.SignatureKey))
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                TypeRoleRecord element;
                if (!signatures.TryGetValue(row.SignatureKey, out signature)
                    || !byType.TryGetValue(
                        TypeDefinitionName.OfElement(
                            ValueTypeName.Contained(signature.ValueType)),
                        out element)
                    || !TypeRoleRecord.HasIndependentTool(element.Role))
                {
                    continue;
                }

                elements.Add(row.SignatureKey, element);
            }

            return elements;
        }
    }
}
