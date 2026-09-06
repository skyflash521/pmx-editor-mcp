using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表とスキーマ正本が、ツール仕様書の写像の規則に合うことを確かめる。名前も埋め込み先も
    /// 呼び分けの見分けも機械で決まるので、書き手が別のものを書けばここで落ちる。
    /// </summary>
    public static class ToolMappingGate
    {
        /// <summary>生成のツールの動作の語の頭。要素名詞を続けて動作の語にする。</summary>
        private const string CreatePrefix = "create_";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures,
            ToolSchemaTable schemas)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            RequireTellableBranches(schemas);

            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, ISet<string>> colliding = Colliding(map, byType, signatures);

            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                if (row.Tool == null && row.EmbeddedIn == null)
                {
                    continue;
                }

                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    throw new InvalidOperationException(
                        "行キーのシグネチャが公開APIの列挙に無い: " + row.SignatureKey);
                }

                if (row.Tool != null)
                {
                    RequireSame(row.Tool, Expected(signature, byType, colliding));
                }

                foreach (string embedded in row.EmbeddedIn ?? new string[0])
                {
                    RequireEmbedded(embedded, signature, byType, map);
                }
            }
        }

        /// <summary>
        /// 同じツールへ集めた呼び分けが、相互に見分けられることを求める。見分けられない呼び分けを
        /// 残すと、どちらの呼び出しなのかがホストの側で決まらない。
        /// </summary>
        private static void RequireTellableBranches(ToolSchemaTable schemas)
        {
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                for (int first = 0; first < schema.Branches.Count; first++)
                {
                    for (int second = first + 1; second < schema.Branches.Count; second++)
                    {
                        if (Tellable(schema.Branches[first], schema.Branches[second]))
                        {
                            continue;
                        }

                        throw new InvalidOperationException(
                            "入力で判別できない呼び分けがある: " + schema.Tool
                                + "(" + schema.Branches[first].Branch + " と "
                                + schema.Branches[second].Branch + ")");
                    }
                }
            }
        }

        /// <summary>
        /// 2つの呼び分けを見分けられるか。見分けられるのは、分岐を選ぶ項目が同じ名前で違う値を選ぶ
        /// とき、片方が必ず渡す名前をもう片方がどの入力にも持たないとき、必ず1つを渡すまとまりが
        /// 共通の名前を持たないときのいずれかで、入れ子の組の中にも同じ規則を当てる。
        /// </summary>
        private static bool Tellable(SchemaBranch first, SchemaBranch second)
        {
            if (first.SelectorName != null
                && string.Equals(first.SelectorName, second.SelectorName, StringComparison.Ordinal)
                && !string.Equals(Written(first.SelectorValue), Written(second.SelectorValue),
                    StringComparison.Ordinal))
            {
                return true;
            }

            return Missing(first.Inputs, second.Inputs)
                || Missing(second.Inputs, first.Inputs)
                || Apart(first, second)
                || Inside(first.Inputs, second.Inputs);
        }

        /// <summary>必ず渡す名前のうち、相手がどの入力にも持たないものがあるか。</summary>
        private static bool Missing(IEnumerable<SchemaItem> from, IEnumerable<SchemaItem> other)
        {
            return from.Where(i => i.Required.HasValue && i.Required.Value)
                .Any(i => !other.Any(
                    o => string.Equals(o.Name, i.Name, StringComparison.Ordinal)));
        }

        /// <summary>必ず1つを渡すまとまりで、共通の名前を持たない組があるか。</summary>
        private static bool Apart(SchemaBranch first, SchemaBranch second)
        {
            return first.Choices.Where(c => c.Required).Any(
                one => second.Choices.Where(c => c.Required).Any(
                    other => !one.Names.Intersect(other.Names, StringComparer.Ordinal).Any()));
        }

        /// <summary>両方が持つ同じ名前の組の中で、必ず渡す名前が食い違うか。</summary>
        private static bool Inside(IList<SchemaItem> first, IList<SchemaItem> second)
        {
            foreach (SchemaItem item in first.Where(i => Grouped(i) != null))
            {
                SchemaItem twin = second.FirstOrDefault(
                    o => string.Equals(o.Name, item.Name, StringComparison.Ordinal)
                        && Grouped(o) != null);
                if (twin == null)
                {
                    continue;
                }

                if (Missing(Grouped(item), Grouped(twin)) || Missing(Grouped(twin), Grouped(item)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// その項目が持つ組の中身。組と、空にできない組の配列が持つ。空にできる配列は、空の要求が
        /// どちらの呼び分けにも当てはまるので見分けに使えない。
        /// </summary>
        private static IList<SchemaItem> Grouped(SchemaItem item)
        {
            if (item.Members != null)
            {
                return item.Members;
            }

            return item.Element == null || !item.MinItems.HasValue ? null : item.Element.Members;
        }

        /// <summary>分岐を選ぶ値を、JSONの形と型を保った文字列にしたもの。</summary>
        private static string Written(object value)
        {
            if (value == null)
            {
                return "null";
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            if (members != null)
            {
                return "{" + string.Join(
                    ",",
                    members.OrderBy(m => m.Key, StringComparer.Ordinal)
                        .Select(m => Written(m.Key) + ":" + Written(m.Value))
                        .ToArray()) + "}";
            }

            IEnumerable<object> items = value as IEnumerable<object>;
            if (items != null)
            {
                return "[" + string.Join(",", items.Select(Written).ToArray()) + "]";
            }

            return value.GetType().Name + ":"
                + Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void RequireSame(string written, string expected)
        {
            if (!string.Equals(expected, written, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "ツールの名前が規則から導いた名前と合わない: " + written
                        + "(導いた名前: " + expected + ")");
            }
        }

        /// <summary>
        /// 埋め込み先が、宣言型の役割に応じた先であることを求める。埋め込み先は名前でしか指せない
        /// ので、綴りの取り違えはここでしか出ない。
        /// </summary>
        private static void RequireEmbedded(
            string embedded,
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            ToolMap map)
        {
            TypeRoleRecord owner;
            if (!byType.TryGetValue(
                    TypeDefinitionName.OfElement(signature.DeclaringType), out owner))
            {
                throw new InvalidOperationException(
                    "ツールの名前を導く型が型役割表に無い: " + signature.DeclaringType);
            }

            bool branch = map.Rows.Any(
                r => string.Equals(r.EventType, embedded, StringComparison.Ordinal));
            if (owner.Role == TypeRole.EventArgs)
            {
                if (!branch)
                {
                    throw new InvalidOperationException(
                        "イベント引数型の埋め込み先がイベントの分岐に無い: " + embedded);
                }

                return;
            }

            if (owner.Role == TypeRole.Dto)
            {
                if (!branch
                    && !map.Rows.Any(
                        r => string.Equals(r.Tool, embedded, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "DTO型の埋め込み先が表のツールにもイベントの分岐にも無い: " + embedded);
                }

                return;
            }

            if (!Aggregated(owner).Contains(embedded, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "埋め込み先が宣言型の取得と更新のツールに無い: " + embedded);
            }
        }

        /// <summary>プロパティを集める先。取得と更新の2つで、追加と削除は集める先にならない。</summary>
        private static IEnumerable<string> Aggregated(TypeRoleRecord owner)
        {
            ToolVerb[] verbs = owner.Role == TypeRole.Connector
                ? new[] { ToolVerb.Get, ToolVerb.Update }
                : new[] { ToolVerb.List, ToolVerb.Update };

            return verbs.Select(v => ToolNameRule.OfRole(owner, v));
        }

        /// <summary>そのシグネチャのツールに期待する名前。</summary>
        private static string Expected(
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, ISet<string>> colliding)
        {
            TypeRoleRecord owner = Role(byType, signature.DeclaringType);
            string group = ToolGroups.TokenOf(owner.Group);
            if (signature.MemberKind == MemberKind.Constructor)
            {
                return ToolNameRule.Compose(group, CreatePrefix + owner.ElementNoun, null);
            }

            string actionWord = ToolNameRule.ActionWord(signature.MemberName);
            bool qualify = owner.Role != TypeRole.Connector || Collides(colliding, group, actionWord);

            return ToolNameRule.Compose(
                group, actionWord, qualify ? owner.ElementNoun : null);
        }

        /// <summary>
        /// 同じ担当群で2つ以上のツールに現れる動作の語。コネクタ型の出所修飾の要否を決める。同名の
        /// オーバーロードは1つのツールへ集まるので、宣言型と動作の語の組を1件として数える。
        /// </summary>
        private static IDictionary<string, ISet<string>> Colliding(
            ToolMap map,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, SignatureRecord> signatures)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<KeyValuePair<string, string>> words = new List<KeyValuePair<string, string>>();
            foreach (ToolMapRow row in map.Rows.Where(r => r.Tool != null))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                TypeRoleRecord owner;
                if (!byType.TryGetValue(
                        TypeDefinitionName.OfElement(signature.DeclaringType), out owner)
                    || owner.Group == CapabilityOwner.None)
                {
                    continue;
                }

                string actionWord = ToolNameRule.ActionWord(signature.MemberName);
                if (!seen.Add(signature.DeclaringType + " " + actionWord))
                {
                    continue;
                }

                words.Add(new KeyValuePair<string, string>(
                    ToolGroups.TokenOf(owner.Group), actionWord));
            }

            return ToolNameRule.Colliding(words);
        }

        private static bool Collides(
            IDictionary<string, ISet<string>> colliding, string group, string actionWord)
        {
            ISet<string> inGroup;
            return colliding.TryGetValue(group, out inGroup) && inGroup.Contains(actionWord);
        }

        /// <summary>その型の役割。表に無いか担当群を持たなければ例外。</summary>
        private static TypeRoleRecord Role(
            IDictionary<string, TypeRoleRecord> byType, string typeName)
        {
            TypeRoleRecord role;
            if (typeName == null
                || !byType.TryGetValue(TypeDefinitionName.OfElement(typeName), out role))
            {
                throw new InvalidOperationException(
                    "ツールの名前を導く型が型役割表に無い: " + (typeName ?? "型名無し"));
            }

            if (role.Group == CapabilityOwner.None)
            {
                throw new InvalidOperationException(
                    "担当群を持たない型のツールがある: " + typeName);
            }

            return role;
        }
    }
}
