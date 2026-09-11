using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 説明文の材料を、能力対応表・型役割表・日本語名の正本・配布物のドキュメントXMLから集める。
    /// 説明文そのものは <see cref="ToolDescriptionRule"/> が組み立てる。
    /// </summary>
    public static class ToolDescriptionEvidence
    {
        /// <summary>ツールごとの材料を、ツール名の順に返す。</summary>
        public static IList<ToolDescriptionMaterial> Collect(
            ToolMap map,
            TypeRoleTable roles,
            IList<PropertyNameRecord> names,
            InventoryRecord inventory,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> contractNotes,
            IDictionary<string, string> methodNotes,
            IDictionary<string, string> propertyNotes)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (contractNotes == null)
            {
                throw new ArgumentNullException(nameof(contractNotes));
            }

            if (methodNotes == null)
            {
                throw new ArgumentNullException(nameof(methodNotes));
            }

            if (propertyNotes == null)
            {
                throw new ArgumentNullException(nameof(propertyNotes));
            }

            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, StringComparer.Ordinal);
            IDictionary<string, TypeRoleRecord> byType = roles.Types
                .ToDictionary(t => TypeDefinitionName.OfElement(t.TypeName), StringComparer.Ordinal);
            IDictionary<string, string> japanese = JapaneseNames(names, signatures, propertyNotes);

            List<ToolDescriptionMaterial> materials = new List<ToolDescriptionMaterial>();
            foreach (IGrouping<string, ToolMapRow> tool in map.Rows
                .Where(r => toolNames.ContainsKey(r.SignatureKey))
                .GroupBy(r => toolNames[r.SignatureKey], StringComparer.Ordinal)
                .Concat(Aggregations(map, byType, toolNames))
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                materials.Add(Material(
                    tool.Key,
                    tool.ToList(),
                    map,
                    byType,
                    japanese,
                    signatures,
                    contractNotes,
                    methodNotes,
                    propertyNotes));
            }

            return new ReadOnlyCollection<ToolDescriptionMaterial>(materials);
        }

        /// <summary>
        /// 項目を集める取得と更新のツールごとの行。これらのツールは行を持たず、埋め込み先として
        /// 名指しされることで現れるので、名指しの側から集める。埋め込み先がイベントの分岐や
        /// ほかのツールであるものは、そのツールの材料が別に在るのでここには入らない。
        /// </summary>
        private static IEnumerable<IGrouping<string, ToolMapRow>> Aggregations(
            ToolMap map,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, string> toolNames)
        {
            HashSet<string> dispatched = new HashSet<string>(
                toolNames.Values, StringComparer.Ordinal);
            List<KeyValuePair<string, ToolMapRow>> named =
                new List<KeyValuePair<string, ToolMapRow>>();
            foreach (ToolMapRow row in map.Rows.Where(r => r.EmbeddedIn != null))
            {
                TypeRoleRecord owner;
                if (!byType.TryGetValue(DeclaringTypeOf(row.SignatureKey), out owner))
                {
                    continue;
                }

                ISet<string> aggregations = AggregationToolRule.Names(new[] { owner });
                foreach (string embedded in row.EmbeddedIn
                    .Where(e => aggregations.Contains(e) && !dispatched.Contains(e)))
                {
                    named.Add(new KeyValuePair<string, ToolMapRow>(embedded, row));
                }
            }

            return named.GroupBy(e => e.Key, e => e.Value, StringComparer.Ordinal);
        }

        /// <summary>行キーが指すメンバーの宣言型。型役割表を引く鍵の形にそろえる。</summary>
        private static string DeclaringTypeOf(string signatureKey)
        {
            int open = signatureKey.IndexOf('(');
            string head = open < 0 ? signatureKey : signatureKey.Substring(0, open);
            int dot = head.LastIndexOf('.');

            return TypeDefinitionName.OfElement(dot < 0 ? head : head.Substring(0, dot));
        }

        private static ToolDescriptionMaterial Material(
            string tool,
            IList<ToolMapRow> rows,
            ToolMap map,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, string> japanese,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> contractNotes,
            IDictionary<string, string> methodNotes,
            IDictionary<string, string> propertyNotes)
        {
            SignatureRecord signature = OneType(tool, rows, signatures);
            TypeRoleRecord role = Role(signature.DeclaringType, byType, tool);
            string group = ToolGroups.TokenOf(role.Group);
            string qualifier = Qualifier(tool, group, role);

            return new ToolDescriptionMaterial(
                tool,
                group,
                ActionWord(tool, group, qualifier),
                qualifier,
                role.ElementNoun,
                signature.DeclaringType,
                Joined(rows.Select(r => Contract(r.SignatureKey, contractNotes))),
                Joined(rows.Select(r => Note(
                    Signature(r.SignatureKey, signatures), methodNotes, propertyNotes))),
                IndexTerms(tool, map, japanese, signatures));
        }

        /// <summary>そのシグネチャの契約注記。持たなければ null。</summary>
        private static string Contract(string signatureKey, IDictionary<string, string> notes)
        {
            string note;

            return notes.TryGetValue(signatureKey, out note) ? note : null;
        }

        // 出所修飾は要素名詞を後置したもの。単数形と複数形のどちらでも後置になりうる。
        private static string Qualifier(string tool, string group, TypeRoleRecord role)
        {
            foreach (string noun in new[] { role.ElementNounPlural, role.ElementNoun })
            {
                if (noun != null && tool.EndsWith("_" + noun, StringComparison.Ordinal)
                    && tool.Length > group.Length + noun.Length + 2)
                {
                    return noun;
                }
            }

            return null;
        }

        private static string ActionWord(string tool, string group, string qualifier)
        {
            string word = tool.Substring(group.Length + 1);
            return qualifier == null
                ? word
                : word.Substring(0, word.Length - qualifier.Length - 1);
        }

        /// <summary>
        /// そのツールの行が指す1つの宣言型。対象も出所もこの型から決まるので、違う型の行が
        /// 混じっていれば止める。
        /// </summary>
        private static SignatureRecord OneType(
            string tool, IList<ToolMapRow> rows, IDictionary<string, SignatureRecord> signatures)
        {
            SignatureRecord first = Signature(rows[0].SignatureKey, signatures);
            foreach (ToolMapRow row in rows)
            {
                SignatureRecord signature = Signature(row.SignatureKey, signatures);
                if (!string.Equals(signature.DeclaringType, first.DeclaringType, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "1つのツールの行が違う宣言型を指している: " + tool + "("
                            + first.DeclaringType + " と " + signature.DeclaringType + ")");
                }
            }

            return first;
        }

        // 索引語は、そのツールへ埋め込まれた項目の名前と日本語名。
        private static IList<IndexTerm> IndexTerms(
            string tool,
            ToolMap map,
            IDictionary<string, string> japanese,
            IDictionary<string, SignatureRecord> signatures)
        {
            List<IndexTerm> terms = new List<IndexTerm>();
            foreach (ToolMapRow row in map.Rows
                .Where(r => r.EmbeddedIn != null && r.EmbeddedIn.Contains(tool, StringComparer.Ordinal))
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature = Signature(row.SignatureKey, signatures);
                string name;
                if (!japanese.TryGetValue(row.SignatureKey, out name))
                {
                    throw new InvalidOperationException(
                        "埋め込んだ項目の日本語名が無い: " + row.SignatureKey);
                }

                terms.Add(new IndexTerm(signature.MemberName, name));
            }

            return terms;
        }

        private static string Note(
            SignatureRecord signature,
            IDictionary<string, string> methodNotes,
            IDictionary<string, string> propertyNotes)
        {
            string note;
            if (signature.MemberKind == MemberKind.Method)
            {
                return methodNotes.TryGetValue(DocumentNoteReader.MemberName(signature), out note)
                    ? note
                    : null;
            }

            if (signature.MemberKind != MemberKind.Property)
            {
                return null;
            }

            return propertyNotes.TryGetValue(
                DocumentNoteReader.MemberName(signature.DeclaringType, signature.MemberName), out note)
                ? note
                : null;
        }

        /// <summary>
        /// プロパティの行キーごとの日本語名。名前を起こした項目は正本から採り、ほかは記載から採る
        /// ——正本に載るのは記載を引けない項目だけなので、載っていなければ記載が名前になる。
        /// </summary>
        private static IDictionary<string, string> JapaneseNames(
            IList<PropertyNameRecord> names,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> propertyNotes)
        {
            IDictionary<string, string> authored = names.ToDictionary(
                n => n.DeclaringType + "|" + n.MemberName, n => n.JapaneseName, StringComparer.Ordinal);
            Dictionary<string, string> japanese = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in signatures.Values
                .Where(s => s.MemberKind == MemberKind.Property))
            {
                string name;
                if (!authored.TryGetValue(
                        signature.DeclaringType + "|" + signature.MemberName, out name)
                    && !propertyNotes.TryGetValue(
                        DocumentNoteReader.MemberName(
                            signature.DeclaringType, signature.MemberName),
                        out name))
                {
                    continue;
                }

                japanese[signature.Key] = name;
            }

            return japanese;
        }

        private static string Joined(IEnumerable<string> notes)
        {
            string[] kept = notes.Where(n => n != null && n.Trim().Length != 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            return kept.Length == 0 ? null : string.Join("。", kept);
        }

        private static SignatureRecord Signature(
            string key, IDictionary<string, SignatureRecord> signatures)
        {
            SignatureRecord signature;
            if (!signatures.TryGetValue(key, out signature))
            {
                throw new InvalidOperationException("配布物に無いシグネチャを指している: " + key);
            }

            return signature;
        }

        private static TypeRoleRecord Role(
            string typeName, IDictionary<string, TypeRoleRecord> byType, string tool)
        {
            TypeRoleRecord role;
            if (!byType.TryGetValue(TypeDefinitionName.OfElement(typeName), out role))
            {
                throw new InvalidOperationException(
                    "型役割表に無い型のツールがある: " + tool + "(" + typeName + ")");
            }

            if (string.IsNullOrEmpty(role.ElementNoun))
            {
                throw new InvalidOperationException(
                    "要素名詞を持たない型のツールがある: " + tool + "(" + typeName + ")");
            }

            if (role.Group == CapabilityOwner.None)
            {
                throw new InvalidOperationException(
                    "担当群を持たない型のツールがある: " + tool + "(" + typeName + ")");
            }

            return role;
        }
    }
}
