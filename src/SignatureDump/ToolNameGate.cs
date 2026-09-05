using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表が割り当てたツールの名前が、規則から導いた名前と一致することを確かめる。名前は
    /// 機械で決まるので、書き手が別の名前を書けばここで落ちる。
    /// </summary>
    public static class ToolNameGate
    {
        /// <summary>生成のツールの動作の語の頭。要素名詞を続けて動作の語にする。</summary>
        private const string CreatePrefix = "create_";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map, TypeRoleTable roles, IDictionary<string, SignatureRecord> signatures)
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
            foreach (ToolVerb verb in new[] { ToolVerb.Get, ToolVerb.List, ToolVerb.Update })
            {
                string named;
                if (owner.Tools.TryGetValue(verb, out named))
                {
                    yield return named;
                }
            }
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
