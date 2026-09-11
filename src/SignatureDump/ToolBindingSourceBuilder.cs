using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>ツールの結び付きを組み立てた結果。</summary>
    public sealed class ToolBindingSource
    {
        public ToolBindingSource(string text, IList<string> calls, IList<string> aggregations)
        {
            Text = text;
            Calls = calls;
            Aggregations = aggregations;
        }

        /// <summary>ホストへ組み込むC#の本文。</summary>
        public string Text { get; }

        /// <summary>SDKのメンバーへ中継するツールの名前。</summary>
        public IList<string> Calls { get; }

        /// <summary>項目を集めるツールの名前。</summary>
        public IList<string> Aggregations { get; }
    }

    /// <summary>
    /// ツールの名前から、呼ぶ行・受け手の型・引数の型・確認の要否を引く表をC#として組み立てる。
    /// どれも行と型役割から導けるので、ホストは名前で引く経路を持たずに振り分けられる。
    /// </summary>
    public static class ToolBindingSourceBuilder
    {
        private const string Indent = "            ";

        private const string VoidTypeName = "System.Void";

        /// <summary>
        /// 表を組み立てる。<paramref name="toolNames"/> は行キーからツールの名前へ、
        /// <paramref name="roles"/> は担当群を解いた型役割表。
        /// </summary>
        public static ToolBindingSource Build(
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames)
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

            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, DangerKind> dangerous = DangerousOperationRule.Classify(
                signatures.Values);

            SortedDictionary<string, string> calls =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, List<string>> fields =
                new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            SortedDictionary<string, bool> writes =
                new SortedDictionary<string, bool>(StringComparer.Ordinal);

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
                    calls.Add(tool, Call(signature, dangerous));
                    continue;
                }

                foreach (KeyValuePair<string, bool> target in Aggregations(row, signature, byType))
                {
                    List<string> listed;
                    if (!fields.TryGetValue(target.Key, out listed))
                    {
                        listed = new List<string>();
                        fields.Add(target.Key, listed);
                        writes.Add(target.Key, target.Value);
                    }

                    listed.Add(Field(signature));
                }
            }

            return new ToolBindingSource(
                Compose(calls, fields, writes),
                calls.Keys.ToList(),
                fields.Keys.ToList());
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
            SignatureRecord signature, IDictionary<string, DangerKind> dangerous)
        {
            DangerKind kind;
            string danger = dangerous.TryGetValue(signature.Key, out kind)
                ? "DangerKind." + kind
                : "DangerKind.None";
            string[] arguments = signature.Parameters
                .Select(p => "new ToolArgument(" + Literal(p.Name) + ", " + Code(p.TypeName) + ")")
                .ToArray();

            return "new ToolCall(" + Literal(signature.Key) + ", " + Receiver(signature) + ", "
                + danger + ", new ToolArgument[] { " + string.Join(", ", arguments) + " }, "
                + (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                    ? "null"
                    : Code(signature.ValueType))
                + ")";
        }

        private static string Field(SignatureRecord signature)
        {
            return "new ToolField(" + Literal(SdkShapeEvidence.MemberNameOf(signature.MemberName))
                + ", " + Literal(signature.Key) + ", " + Receiver(signature) + ", "
                + Code(signature.ValueType) + ")";
        }

        /// <summary>受け手を引く鍵。静的なメンバーは相手を取らないので持たない。</summary>
        private static string Receiver(SignatureRecord signature)
        {
            return signature.IsStatic
                ? "null"
                : Literal(TypeDefinitionName.Of(signature.DeclaringType));
        }

        /// <summary>列挙が書く型名から、その型を指す式。</summary>
        private static string Code(string typeName)
        {
            return "typeof(global::" + typeName.Replace('+', '.') + ")";
        }

        private static string Literal(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Compose(
            IDictionary<string, string> calls,
            IDictionary<string, List<string>> fields,
            IDictionary<string, bool> writes)
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
                    .Append(", new ToolFields(").Append(writes[tool.Key] ? "true" : "false")
                    .Append(", new ToolField[]\n");
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
            text.Append("    }\n");
            text.Append("}\n");

            return text.ToString();
        }
    }
}
