using System;
using System.Collections.Generic;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツール定義をブリッジへ組み込むC#として綴る。ブリッジは出来た本文だけを持ち、定義を実行時に
    /// 組み立てない。
    /// </summary>
    public static class ToolDefinitionSource
    {
        public static string Compose(IEnumerable<ToolDefinition> definitions, string toolMapDigest)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (toolMapDigest == null)
            {
                throw new ArgumentNullException(nameof(toolMapDigest));
            }

            StringBuilder text = new StringBuilder();
            text.Append("// この本文はビルドのたびに作り直す。手で直さない。\n");
            text.Append("using System.Collections.Generic;\n");
            text.Append("\n");
            text.Append("namespace PmxEditorMcp.Bridge\n");
            text.Append("{\n");
            text.Append("    internal static class GeneratedToolDefinitions\n");
            text.Append("    {\n");
            text.Append("        /// <summary>定義を作った能力対応表の指紋。</summary>\n");
            text.Append("        internal const string ToolMapDigest = ").Append(Literal(toolMapDigest))
                .Append(";\n");
            text.Append("\n");
            text.Append("        internal static IReadOnlyList<GeneratedToolDefinition> Create()\n");
            text.Append("        {\n");
            text.Append("            return new GeneratedToolDefinition[]\n");
            text.Append("            {\n");
            foreach (ToolDefinition definition in definitions)
            {
                text.Append("                new GeneratedToolDefinition(\n");
                text.Append("                    ").Append(Literal(definition.Name)).Append(",\n");
                text.Append("                    ").Append(Literal(definition.Description)).Append(",\n");
                text.Append("                    ").Append(Literal(definition.InputSchema)).Append("),\n");
            }

            text.Append("            };\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("}\n");

            return text.ToString();
        }

        private static string Literal(string text)
        {
            StringBuilder quoted = new StringBuilder("\"");
            foreach (char letter in text)
            {
                switch (letter)
                {
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        quoted.Append(letter);
                        break;
                }
            }

            return quoted.Append('"').ToString();
        }
    }
}
