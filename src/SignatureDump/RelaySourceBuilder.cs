using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>中継コードを組み立てた結果。</summary>
    public sealed class RelaySource
    {
        public RelaySource(string text, IList<string> resolved, IList<string> unresolved)
        {
            Text = text;
            Resolved = resolved;
            Unresolved = unresolved;
        }

        /// <summary>ホストへ組み込むC#の本文。</summary>
        public string Text { get; }

        /// <summary>中継を作れた行キー。</summary>
        public IList<string> Resolved { get; }

        /// <summary>中継を作れなかった行キー。</summary>
        public IList<string> Unresolved { get; }
    }

    /// <summary>
    /// 能力対応表の行キーから、SDKのメンバーを直接呼ぶC#を組み立てる。呼び出し先は生成した本文の
    /// 中で名前のまま書かれ、コンパイラが解決する——配布物では名前で引く経路を持たない。
    /// 解決できない行は中継を作らず、その行だけを無効として並べる。待受は止めない。
    /// </summary>
    public static class RelaySourceBuilder
    {
        private const string Indent = "            ";

        /// <summary>
        /// 中継を組み立てる。<paramref name="rowKeys"/> は能力対応表の行キーで、
        /// <paramref name="inventory"/> はSDKの公開シグネチャの列挙、
        /// <paramref name="combinableEnums"/> は名前を並べて組み合わせられる列挙の綴り、
        /// <paramref name="toolMapDigest"/> はその能力対応表の指紋。
        /// </summary>
        public static RelaySource Build(
            IEnumerable<string> rowKeys,
            InventoryRecord inventory,
            string sdkVersion,
            IEnumerable<string> combinableEnums,
            string toolMapDigest,
            IDictionary<string, ReceiverPath> receivers)
        {
            if (rowKeys == null)
            {
                throw new ArgumentNullException(nameof(rowKeys));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (sdkVersion == null)
            {
                throw new ArgumentNullException(nameof(sdkVersion));
            }

            if (combinableEnums == null)
            {
                throw new ArgumentNullException(nameof(combinableEnums));
            }

            if (toolMapDigest == null)
            {
                throw new ArgumentNullException(nameof(toolMapDigest));
            }

            if (receivers == null)
            {
                throw new ArgumentNullException(nameof(receivers));
            }

            Dictionary<string, SignatureRecord> byKey = inventory.Signatures
                .GroupBy(s => s.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            List<string> resolved = new List<string>();
            List<string> unresolved = new List<string>();
            StringBuilder calls = new StringBuilder();

            foreach (string rowKey in rowKeys.Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                string expression = byKey.TryGetValue(rowKey, out signature)
                    ? TryExpression(signature)
                    : null;

                if (expression == null)
                {
                    unresolved.Add(rowKey);
                    continue;
                }

                resolved.Add(rowKey);
                calls.Append(Indent).Append("calls.Add(").Append(Literal(rowKey)).Append(", ")
                    .Append(expression.StartsWith("(target", StringComparison.Ordinal)
                        ? expression
                        : "(target, arguments) => " + expression)
                    .Append(");")
                    .Append('\n');
            }

            return new RelaySource(
                Compose(
                    calls.ToString(), unresolved, sdkVersion, combinableEnums, toolMapDigest, receivers),
                resolved,
                unresolved);
        }

        /// <summary>
        /// そのシグネチャを直接呼ぶ式。中継を作れない形では null。作れないのは、値を返しながら
        /// 出力の引数も持つもの・参照渡し(入出力の両方)の引数を持つもの・総称型の引数を取るもの・
        /// 引数を取るプロパティ・イベント・コンストラクタで、いずれもこの生成では呼び出しの形か
        /// 返すものが1つに定まらない。
        /// </summary>
        private static string TryExpression(SignatureRecord signature)
        {
            bool outputs = signature.Parameters.Any(p => p.Direction == ParameterDirection.Out);
            if (signature.GenericArity != 0
                || signature.Parameters.Any(p => p.Direction == ParameterDirection.Ref)
                || (outputs && !IsVoid(signature.ValueType)))
            {
                return null;
            }

            // 受け手のキャストは括弧で閉じる。閉じないと、キャストがメンバーの呼び出しの結果へ
            // 掛かる式になる。
            string receiver = signature.IsStatic
                ? Code(signature.DeclaringType)
                : "(" + Cast(signature.DeclaringType, "target") + ")";

            if (signature.MemberKind == MemberKind.Method)
            {
                string arguments = string.Join(", ", Casts(signature).ToArray());
                string call = receiver + "." + signature.MemberName + "(" + arguments + ")";
                if (outputs)
                {
                    return Outputting(signature, call);
                }

                return IsVoid(signature.ValueType) ? "Call(() => " + call + ")" : call;
            }

            if (signature.MemberKind != MemberKind.Property && signature.MemberKind != MemberKind.Field)
            {
                return null;
            }

            if (signature.Parameters.Count != 0)
            {
                return null;
            }

            // 取得と更新は同じ行キーが指す。引数が無ければ取得、1つあれば更新とする。
            string place = receiver + "." + signature.MemberName;
            string read = place;
            string write = "Call(() => " + place + " = "
                + Cast(signature.ValueType, "arguments[0]") + ")";

            if (!signature.CanWrite)
            {
                return read;
            }

            if (!signature.CanRead)
            {
                return write;
            }

            return "arguments.Length == 0 ? (object)" + read + " : " + write;
        }

        /// <summary>呼び出しへ渡す引数の式。</summary>
        private static IEnumerable<string> Casts(SignatureRecord signature)
        {
            int taken = 0;
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                if (parameter.Direction == ParameterDirection.Out)
                {
                    yield return "out " + parameter.Name;
                    continue;
                }

                yield return Cast(parameter.TypeName, "arguments[" + Index(taken) + "]");
                taken++;
            }
        }

        /// <summary>出力に現れる引数を持つ呼び出しの中継の式。</summary>
        private static string Outputting(SignatureRecord signature, string call)
        {
            StringBuilder built = new StringBuilder("(target, arguments) =>\n");
            built.Append(Indent).Append("{\n");
            foreach (ParameterRecord parameter in signature.Parameters
                .Where(p => p.Direction == ParameterDirection.Out))
            {
                built.Append(Indent).Append("    ").Append(Code(parameter.TypeName)).Append(' ')
                    .Append(parameter.Name).Append(";\n");
            }

            built.Append(Indent).Append("    ").Append(call).Append(";\n");
            built.Append(Indent).Append("    return new object[] { ")
                .Append(string.Join(", ", signature.Parameters
                    .Where(p => p.Direction == ParameterDirection.Out)
                    .Select(p => p.Name)
                    .ToArray()))
                .Append(" };\n");
            built.Append(Indent).Append("}");

            return built.ToString();
        }

        private static bool IsVoid(string valueType)
        {
            return string.Equals(valueType, "System.Void", StringComparison.Ordinal);
        }

        private static string Cast(string typeName, string expression)
        {
            return "(" + Code(typeName) + ")" + expression;
        }

        /// <summary>列挙が書く型名を、C#が受け取れる綴りへ直す。入れ子の型は点で区切る。</summary>
        private static string Code(string typeName)
        {
            return "global::" + typeName.Replace('+', '.');
        }

        private static string Index(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 受け手を得る式。根から一歩ずつ進む。自動注入コネクタを取る一歩には、常駐から渡す。
        /// </summary>
        private static string Receiver(ReceiverPath path)
        {
            string expression = Root(path.Root);
            if (path.Steps.Length == 0)
            {
                return expression;
            }

            foreach (string step in path.Steps.Split('.'))
            {
                expression += "." + (step.EndsWith("()", StringComparison.Ordinal)
                    ? step.Substring(0, step.Length - 2) + "(connection.Use())"
                    : step);
            }

            return expression;
        }

        /// <summary>
        /// 接続の根を指す式。常駐が保つものと、実体を持たない静的な橋渡しから採る。これ以外の根は
        /// 常駐が渡せないので、その道は中継に使えない。
        /// </summary>
        private static string Root(string root)
        {
            switch (root)
            {
                case "PEPlugin.IPERunArgs":
                    return "connection.RunArgs";
                case "PXCPlugin.IPXCPluginRunArgs":
                    return "connection.UseRunArgs()";
                case "PXCPlugin.PXCBridge":
                    return "global::PXCPlugin.PXCBridge";
                default:
                    throw new InvalidOperationException("受け手を得られない接続の根: " + root);
            }
        }

        private static string Literal(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Compose(
            string calls,
            IList<string> unresolved,
            string sdkVersion,
            IEnumerable<string> combinableEnums,
            string toolMapDigest,
            IDictionary<string, ReceiverPath> receivers)
        {
            StringBuilder text = new StringBuilder();
            text.Append("// この本文はビルドのたびに作り直す。手で直さない。\n");
            text.Append("using System;\n");
            text.Append("using System.Collections.Generic;\n");
            text.Append("\n");
            text.Append("namespace PmxEditorMcp\n");
            text.Append("{\n");
            text.Append("    internal static class GeneratedSdkRelay\n");
            text.Append("    {\n");
            text.Append("        internal const string SdkVersion = ").Append(Literal(sdkVersion))
                .Append(";\n");
            text.Append("\n");
            text.Append("        /// <summary>中継を作った能力対応表の指紋。</summary>\n");
            text.Append("        internal const string ToolMapDigest = ").Append(Literal(toolMapDigest))
                .Append(";\n");
            text.Append("\n");
            text.Append("        internal static SdkRelayTable Create()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, SdkCall> calls =\n");
            text.Append("                new Dictionary<string, SdkCall>(StringComparer.Ordinal);\n");
            text.Append(calls);
            text.Append("\n");
            text.Append(
                "            return new SdkRelayTable(SdkVersion, ToolMapDigest, calls, new string[]\n");
            text.Append("            {\n");
            foreach (string key in unresolved)
            {
                text.Append("                ").Append(Literal(key)).Append(",\n");
            }

            text.Append("            });\n");
            text.Append("        }\n");
            text.Append("\n");
            text.Append("        /// <summary>値を返さない呼び出しも、中継の形をそろえて null を返す。</summary>\n");
            text.Append("        private static object Call(Action action)\n");
            text.Append("        {\n");
            text.Append("            action();\n");
            text.Append("\n");
            text.Append("            return null;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkEnums\n");
            text.Append("    {\n");
            text.Append("        /// <summary>名前を並べて組み合わせられるSDKの列挙。</summary>\n");
            text.Append("        internal static readonly HashSet<string> Combinable =\n");
            text.Append("            new HashSet<string>(StringComparer.Ordinal)\n");
            text.Append("            {\n");
            foreach (string name in combinableEnums.Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal))
            {
                text.Append("                ").Append(Literal(name)).Append(",\n");
            }

            text.Append("            };\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkReceivers\n");
            text.Append("    {\n");
            text.Append("        /// <summary>宣言型から受け手を得る。道はビルド時に辿ってある。</summary>\n");
            text.Append("        internal static Dictionary<string, SdkReceiver> Create()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, SdkReceiver> receivers =\n");
            text.Append("                new Dictionary<string, SdkReceiver>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, ReceiverPath> receiver in receivers
                .OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                text.Append(Indent).Append("receivers.Add(").Append(Literal(receiver.Key))
                    .Append(", connection => ")
                    .Append(Receiver(receiver.Value)).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return receivers;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("}\n");

            return text.ToString();
        }
    }
}
