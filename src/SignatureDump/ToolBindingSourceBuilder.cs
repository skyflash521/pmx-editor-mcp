using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>ツールの結び付きを組み立てた結果。</summary>
    public sealed class ToolBindingSource
    {
        public ToolBindingSource(
            string text, IList<string> calls, IList<string> aggregations, IList<string> elements)
        {
            Text = text;
            Calls = calls;
            Aggregations = aggregations;
            Elements = elements;
        }

        /// <summary>ホストへ組み込むC#の本文。</summary>
        public string Text { get; }

        /// <summary>SDKのメンバーへ中継するツールの名前。</summary>
        public IList<string> Calls { get; }

        /// <summary>項目を集めるツールの名前。</summary>
        public IList<string> Aggregations { get; }

        /// <summary>所有するリストへ加える・から取り除くツールの名前。</summary>
        public IList<string> Elements { get; }
    }

    /// <summary>
    /// ツールの名前から、呼ぶ行・受け手の得方・引数の型・確認の要否を引く表と、所有するリストを
    /// 読み書きする中継をC#として組み立てる。どれも行と型役割から導けるので、ホストは名前で引く
    /// 経路を持たずに振り分けられる。
    /// </summary>
    public static class ToolBindingSourceBuilder
    {
        private const string Indent = "            ";

        private const string VoidTypeName = "System.Void";

        private const string PmxTypeName = "PEPlugin.Pmx.IPXPmx";

        /// <summary>
        /// 表を組み立てる。<paramref name="toolNames"/> は行キーからツールの名前へ、
        /// <paramref name="roles"/> は担当群を解いた型役割表、<paramref name="assignments"/> は
        /// 共通契約割当の正本。
        /// </summary>
        public static ToolBindingSource Build(
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            CommonAssignmentTable assignments)
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

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, DangerKind> dangerous = DangerousOperationRule.Classify(
                signatures.Values);
            IDictionary<string, TypeRoleRecord> owned = ElementToolRule.Elements(
                map, signatures, roles);

            SortedDictionary<string, string> calls =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, List<string>> fields =
                new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            SortedDictionary<string, string> aggregated =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> elements =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> lists =
                new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                string tool;
                if (toolNames.TryGetValue(row.SignatureKey, out tool))
                {
                    calls.Add(tool, Call(row, signature, dangerous));
                    continue;
                }

                TypeRoleRecord element;
                if (owned.TryGetValue(row.SignatureKey, out element))
                {
                    lists.Add(row.SignatureKey, List(signature, element));
                    foreach (string named in ElementToolRule.Of(element))
                    {
                        elements.Add(named, Elements(row, signature, element, named));
                    }

                    continue;
                }

                foreach (KeyValuePair<string, bool> target in Aggregations(row, signature, byType))
                {
                    List<string> listed;
                    if (!fields.TryGetValue(target.Key, out listed))
                    {
                        listed = new List<string>();
                        fields.Add(target.Key, listed);
                        aggregated.Add(
                            target.Key,
                            Aggregation(row, signature, byType, target.Value));
                    }

                    listed.Add(Field(signature));
                }
            }

            return new ToolBindingSource(
                Compose(calls, fields, aggregated, elements, lists, Flows(assignments, signatures)),
                calls.Keys.ToList(),
                fields.Keys.ToList(),
                elements.Keys.ToList());
        }

        /// <summary>
        /// その行が項目を持ち込む取得と更新のツール。名前と、そのツールが書き込む側かどうかを返す。
        /// 埋め込み先が取得と更新のどちらでもないものは、そのツールの側が項目を持つので返さない。
        /// </summary>
        private static IEnumerable<KeyValuePair<string, bool>> Aggregations(
            ToolMapRow row,
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType)
        {
            TypeRoleRecord owner;
            if (row.EmbeddedIn == null
                || !byType.TryGetValue(
                    TypeDefinitionName.OfElement(signature.DeclaringType), out owner))
            {
                yield break;
            }

            string[] aggregations = AggregationToolRule.Of(owner).ToArray();
            string updating = ToolNameRule.OfRole(owner, ToolVerb.Update);
            foreach (string embedded in row.EmbeddedIn
                .Where(e => aggregations.Contains(e, StringComparer.Ordinal)))
            {
                bool updates = string.Equals(embedded, updating, StringComparison.Ordinal);
                if (updates ? !signature.CanWrite : !signature.CanRead)
                {
                    throw new InvalidOperationException(
                        (updates ? "書き込めない項目が更新へ持ち込まれている: "
                            : "読み取れない項目が取得へ持ち込まれている: ") + signature.Key);
                }

                yield return new KeyValuePair<string, bool>(embedded, updates);
            }
        }

        private static string Call(
            ToolMapRow row,
            SignatureRecord signature,
            IDictionary<string, DangerKind> dangerous)
        {
            DangerKind kind;
            string danger = dangerous.TryGetValue(signature.Key, out kind)
                ? "DangerKind." + kind
                : "DangerKind.None";
            string[] arguments = signature.Parameters
                .Select(p => "new ToolArgument(" + Literal(p.Name) + ", " + TypeOf(p.TypeName) + ")")
                .ToArray();

            return "new ToolCall(" + Literal(signature.Key) + ", " + Receiver(row, signature) + ", "
                + danger + ", new ToolArgument[] { " + string.Join(", ", arguments) + " }, "
                + (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                    ? "null"
                    : TypeOf(signature.ValueType))
                + ")";
        }

        private static string Aggregation(
            ToolMapRow row,
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            bool writes)
        {
            TypeRoleRecord owner = byType[TypeDefinitionName.OfElement(signature.DeclaringType)];

            return "new ToolFields(" + (writes ? "true" : "false") + ", "
                + (owner.Role == TypeRole.Connector ? "false" : "true") + ", "
                + Receiver(row, signature, writes ? row.EditKind : ToolMapEditKind.Read)
                + ", new ToolField[]";
        }

        private static string Elements(
            ToolMapRow row, SignatureRecord signature, TypeRoleRecord element, string tool)
        {
            bool removes = string.Equals(
                tool, ToolNameRule.OfRole(element, ToolVerb.Remove), StringComparison.Ordinal);

            return "new ToolElements(" + (removes ? "true" : "false") + ", "
                + Literal(signature.Key) + ", " + Receiver(row, signature, ToolMapEditKind.DuplicateEdit)
                + ", " + TypeOf(element.TypeName) + ")";
        }

        private static string List(SignatureRecord signature, TypeRoleRecord element)
        {
            string owner = "((" + Code(signature.DeclaringType) + ")owner)." + signature.MemberName;

            return "new SdkList("
                + "owner => " + owner + ".Count, "
                + "(owner, index) => " + owner + "[index], "
                + "(owner, item) => " + owner + ".Add((" + Code(element.TypeName) + ")item), "
                + "(owner, index) => " + owner + ".RemoveAt(index))";
        }

        private static string Field(SignatureRecord signature)
        {
            return "new ToolField(" + Literal(SdkShapeEvidence.MemberNameOf(signature.MemberName))
                + ", " + Literal(signature.Key) + ", " + TypeOf(signature.ValueType) + ")";
        }

        /// <summary>
        /// 受け手の得方。所有の根の型はどのPMXを見るかの切り替えで選び、ほかは接続の道から得る。
        /// 静的なメンバーは相手を取らない。
        /// </summary>
        private static string Receiver(
            ToolMapRow row, SignatureRecord signature, ToolMapEditKind? edit = null)
        {
            string declaring = TypeDefinitionName.OfElement(signature.DeclaringType);
            bool rooted = string.Equals(declaring, PmxTypeName, StringComparison.Ordinal);
            string type = signature.IsStatic || rooted ? "null" : Literal(declaring);

            return "new ToolReceiver(ToolReceiverKind."
                + (rooted ? "Pmx" : "Connection") + ", " + type + ", EditKind."
                + Edit(edit ?? row.EditKind) + ")";
        }

        private static string Edit(ToolMapEditKind kind)
        {
            switch (kind)
            {
                case ToolMapEditKind.DuplicateEdit:
                    return "DuplicateEdit";

                case ToolMapEditKind.DirectChange:
                    return "DirectChange";

                case ToolMapEditKind.ViewSession:
                    return "ViewSession";

                default:
                    return "Read";
            }
        }

        /// <summary>
        /// 複製編集の流れが通る行キー。状態取得は引数を取らないもの、反映は複製だけを取るものを
        /// 採る。ほかの形は反映する範囲を別に受け取るので、流れの既定にはしない。
        /// </summary>
        private static KeyValuePair<string, string> Flows(
            CommonAssignmentTable assignments, IDictionary<string, SignatureRecord> signatures)
        {
            string read = Flow(assignments, signatures, "stateRead", p => p.Count == 0);
            string commit = Flow(
                assignments,
                signatures,
                "duplicateEdit",
                p => p.Count == 1
                    && string.Equals(p[0].TypeName, PmxTypeName, StringComparison.Ordinal));

            return new KeyValuePair<string, string>(read, commit);
        }

        private static string Flow(
            CommonAssignmentTable assignments,
            IDictionary<string, SignatureRecord> signatures,
            string target,
            Func<IList<ParameterRecord>, bool> shape)
        {
            string[] found = assignments.Assignments
                .Where(a => a.Assignment == CommonAssignmentKind.InternalFlow
                    && string.Equals(a.Target, target, StringComparison.Ordinal)
                    && signatures.ContainsKey(a.SignatureKey)
                    && shape(signatures[a.SignatureKey].Parameters))
                .Select(a => a.SignatureKey)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            if (found.Length != 1)
            {
                throw new InvalidOperationException(
                    "内部フローの行が1件に決まらない: " + target + "(" + found.Length + " 件)");
            }

            return found[0];
        }

        /// <summary>行キーが指すメンバーの宣言型。</summary>
        private static string DeclaringTypeOf(string rowKey)
        {
            int open = rowKey.IndexOf('(');
            string head = open < 0 ? rowKey : rowKey.Substring(0, open);

            return head.Substring(0, head.LastIndexOf('.'));
        }

        /// <summary>列挙が書く型名から、その型の綴り。</summary>
        private static string Code(string typeName)
        {
            return "global::" + typeName.Replace('+', '.');
        }

        /// <summary>列挙が書く型名から、その型そのものを渡す式。</summary>
        private static string TypeOf(string typeName)
        {
            return "typeof(" + Code(typeName) + ")";
        }

        private static string Literal(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Compose(
            IDictionary<string, string> calls,
            IDictionary<string, List<string>> fields,
            IDictionary<string, string> aggregated,
            IDictionary<string, string> elements,
            IDictionary<string, string> lists,
            KeyValuePair<string, string> flows)
        {
            StringBuilder text = new StringBuilder();
            text.Append("// この本文はビルドのたびに作り直す。手で直さない。\n");
            text.Append("using System;\n");
            text.Append("using System.Collections.Generic;\n");
            text.Append("\n");
            text.Append("namespace PmxEditorMcp\n");
            text.Append("{\n");
            text.Append("    internal static class GeneratedTools\n");
            text.Append("    {\n");
            text.Append("        /// <summary>SDKのメンバーへ中継するツール。</summary>\n");
            text.Append("        internal static Dictionary<string, ToolCall> Calls()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolCall> calls =\n");
            text.Append("                new Dictionary<string, ToolCall>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> call in calls)
            {
                text.Append(Indent).Append("calls.Add(").Append(Literal(call.Key)).Append(", ")
                    .Append(call.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return calls;\n");
            text.Append("        }\n");
            text.Append("\n");
            text.Append("        /// <summary>項目を集めるツール。</summary>\n");
            text.Append("        internal static Dictionary<string, ToolFields> Aggregations()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolFields> aggregations =\n");
            text.Append("                new Dictionary<string, ToolFields>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, List<string>> tool in fields)
            {
                text.Append(Indent).Append("aggregations.Add(").Append(Literal(tool.Key))
                    .Append(", ").Append(aggregated[tool.Key]).Append("\n");
                text.Append(Indent).Append("{\n");
                foreach (string field in tool.Value)
                {
                    text.Append(Indent).Append("    ").Append(field).Append(",\n");
                }

                text.Append(Indent).Append("}));\n");
            }

            text.Append("\n");
            text.Append("            return aggregations;\n");
            text.Append("        }\n");
            text.Append("\n");
            text.Append("        /// <summary>所有するリストへ加える・から取り除くツール。</summary>\n");
            text.Append("        internal static Dictionary<string, ToolElements> Elements()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolElements> elements =\n");
            text.Append("                new Dictionary<string, ToolElements>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> element in elements)
            {
                text.Append(Indent).Append("elements.Add(").Append(Literal(element.Key))
                    .Append(", ").Append(element.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return elements;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkLists\n");
            text.Append("    {\n");
            text.Append("        /// <summary>行キーから、所有するリストを読み書きする中継を引く。</summary>\n");
            text.Append("        internal static Dictionary<string, SdkList> Create()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, SdkList> lists =\n");
            text.Append("                new Dictionary<string, SdkList>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> list in lists)
            {
                text.Append(Indent).Append("lists.Add(").Append(Literal(list.Key)).Append(",\n");
                text.Append(Indent).Append("    ").Append(list.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return lists;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkFlows\n");
            text.Append("    {\n");
            text.Append("        /// <summary>現在のPMXの複製を得る行。</summary>\n");
            text.Append("        internal const string StateRead = ").Append(Literal(flows.Key))
                .Append(";\n");
            text.Append("\n");
            text.Append("        /// <summary>複製した中身をまとめて反映する行。</summary>\n");
            text.Append("        internal const string Commit = ").Append(Literal(flows.Value))
                .Append(";\n");
            text.Append("\n");
            text.Append("        /// <summary>この2つの行の受け手を引く鍵。</summary>\n");
            text.Append("        internal const string Receiver = ")
                .Append(Literal(DeclaringTypeOf(flows.Key))).Append(";\n");
            text.Append("\n");
            text.Append("        /// <summary>PMXの実体の型。ハンドルの型を見分けるのに使う。</summary>\n");
            text.Append("        internal static readonly Type Pmx = ")
                .Append(TypeOf(PmxTypeName)).Append(";\n");
            text.Append("    }\n");
            text.Append("}\n");

            return text.ToString();
        }
    }
}
