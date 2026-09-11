using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 所有するリストの要素の型が持つ、追加と削除のツールの名前を決める。これらのツールは行を
    /// 持たず、そのリストの行が能力対応表へ載ることで現れるので、名前の決め方をここ1つに置く。
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
