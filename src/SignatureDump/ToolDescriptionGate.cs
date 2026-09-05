using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 組み立てた説明文が、クライアントの切り詰めに掛からない大きさに収まり、群のなかで衝突する
    /// 動作の語を出所で見分けられることを確かめる。
    /// </summary>
    public static class ToolDescriptionGate
    {
        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            IList<ToolDescriptionMaterial> materials,
            IDictionary<string, ToolDescription> descriptions,
            ICollection<string> composedTools)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            if (descriptions == null)
            {
                throw new ArgumentNullException(nameof(descriptions));
            }

            if (composedTools == null)
            {
                throw new ArgumentNullException(nameof(composedTools));
            }

            RequireNoComposedMaterial(materials, composedTools);
            RequireSameTools(materials, descriptions, composedTools);
            RequireLimit(descriptions);
            RequireSourceQualifier(materials, descriptions);
        }

        /// <summary>
        /// 合成ツールの名前を持つ材料が無いことを求める。合成ツールは行を持たないので、行から材料が
        /// 出れば同じ名前の説明文が2つの出所から現れ、どちらを載せるかが決まらない。
        /// </summary>
        private static void RequireNoComposedMaterial(
            IList<ToolDescriptionMaterial> materials, ICollection<string> composedTools)
        {
            string collided = materials.Select(m => m.Tool).Where(composedTools.Contains)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (collided != null)
            {
                throw new InvalidOperationException(
                    "合成ツールの名前を割り当てた行がある: " + collided);
            }
        }

        /// <summary>
        /// 説明文が、材料を持つツールと合成ツールを合わせたものを覆うことを求める。合成ツールは行を
        /// 持たないので材料からは現れない。
        /// </summary>
        private static void RequireSameTools(
            IList<ToolDescriptionMaterial> materials,
            IDictionary<string, ToolDescription> descriptions,
            ICollection<string> composedTools)
        {
            HashSet<string> known = new HashSet<string>(
                materials.Select(m => m.Tool), StringComparer.Ordinal);
            known.UnionWith(composedTools);

            string missing = known.Where(t => !descriptions.ContainsKey(t))
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("説明文が無いツールがある: " + missing);
            }

            string extra = descriptions.Keys.Where(t => !known.Contains(t))
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("材料の無い説明文がある: " + extra);
            }
        }

        /// <summary>説明文がUTF-8で上限のバイト数に収まることを求める。</summary>
        private static void RequireLimit(IDictionary<string, ToolDescription> descriptions)
        {
            foreach (KeyValuePair<string, ToolDescription> description in descriptions
                .OrderBy(d => d.Key, StringComparer.Ordinal))
            {
                int bytes = Encoding.UTF8.GetByteCount(description.Value.Text);
                if (bytes > ToolDescriptionRule.LimitBytes)
                {
                    throw new InvalidOperationException(
                        "説明文が上限のバイト数を超える: " + description.Key
                            + "(" + bytes + "バイト)");
                }
            }
        }

        /// <summary>
        /// 群のなかで2件以上に現れる動作の語を持つツールが、出所修飾を後置していること、および
        /// その説明文の先頭に対象と出所の語が現れることを求める。
        /// </summary>
        private static void RequireSourceQualifier(
            IList<ToolDescriptionMaterial> materials, IDictionary<string, ToolDescription> descriptions)
        {
            IDictionary<string, ISet<string>> colliding = ToolNameRule.Colliding(
                materials.Select(m => new KeyValuePair<string, string>(m.Group, m.ActionWord)));

            foreach (ToolDescriptionMaterial material in materials.OrderBy(m => m.Tool, StringComparer.Ordinal))
            {
                ISet<string> inGroup;
                if (!colliding.TryGetValue(material.Group, out inGroup)
                    || !inGroup.Contains(material.ActionWord))
                {
                    continue;
                }

                if (material.Qualifier == null || material.Qualifier.Length == 0)
                {
                    throw new InvalidOperationException(
                        "群のなかで衝突する動作の語を持つのに出所修飾が無い: " + material.Tool);
                }

                if (!material.Tool.EndsWith("_" + material.Qualifier, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "出所修飾が後置されていない: " + material.Tool);
                }

                string head = Head(descriptions[material.Tool].Text);
                if (head.IndexOf(material.ElementNoun, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "説明文の先頭に対象の語が無い: " + material.Tool);
                }

                if (head.IndexOf(material.TypeName, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "説明文の先頭に出所の語が無い: " + material.Tool);
                }
            }
        }

        private static string Head(string text)
        {
            int end = text.IndexOf('\n');
            return end < 0 ? text : text.Substring(0, end);
        }
    }
}
