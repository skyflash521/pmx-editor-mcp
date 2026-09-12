using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 行キーからツールの名前を引く表を、型役割表と公開API列挙から導く。名前は担当群と動作の語と
    /// 要素名詞で決まるので、行は名前を書かない。
    /// </summary>
    public static class ToolNameEvidence
    {
        /// <summary>生成のツールの動作の語の頭。要素名詞を続けて動作の語にする。</summary>
        private const string CreatePrefix = "create_";

        /// <summary>
        /// 独立したツールを持つのは直接ディスパッチの行だけなので、その行キーだけを持つ表を返す。
        /// 導けないものがあれば <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<string, string> Resolve(
            ToolMap map,
            TypeRoleTable roles,
            CommonAssignmentTable assignments,
            IDictionary<string, SignatureRecord> signatures)
        {
            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            IDictionary<string, ToolMapRowKind> kinds = RowKindRule.Resolve(
                map,
                signatures,
                ToolMapEvidence.EmbeddedTypeNames(roles),
                new HashSet<string>(
                    assignments.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal),
                ToolMapEvidence.IndependentToolTypeNames(roles),
                HandleRouteEvidence.Reached(signatures, roles));
            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IList<string> dispatched = kinds
                .Where(k => k.Value == ToolMapRowKind.DirectDispatch && signatures.ContainsKey(k.Key))
                .Select(k => k.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
            IDictionary<string, ISet<string>> colliding = Colliding(dispatched, byType, signatures);

            return new ReadOnlyDictionary<string, string>(dispatched.ToDictionary(
                key => key, key => Named(signatures[key], byType, colliding), StringComparer.Ordinal));
        }

        /// <summary>そのシグネチャのツールの名前。</summary>
        private static string Named(
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
            IEnumerable<string> dispatched,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, SignatureRecord> signatures)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<KeyValuePair<string, string>> words = new List<KeyValuePair<string, string>>();
            foreach (string key in dispatched)
            {
                SignatureRecord signature = signatures[key];
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
