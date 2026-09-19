using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 画面の構造の台帳を、配布物へ組み込むC#の本文にする。台帳はそのままの綴りで持つ。
    ///
    /// 本文はASCIIだけで綴り、その外の文字は逃がす記法へ直す。
    /// </summary>
    public static class UiStructureSourceBuilder
    {
        /// <summary>1つの文字列に入れる文字数。</summary>
        private const int ChunkChars = 2000;

        public static string Build(string catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine("// この本文は生成物である。手で書き換えない。");
            text.AppendLine();
            text.AppendLine("namespace PmxEditorMcp");
            text.AppendLine("{");
            text.AppendLine("    /// <summary>組み込んだ画面の構造の台帳。</summary>");
            text.AppendLine("    internal static class GeneratedUiStructure");
            text.AppendLine("    {");
            text.AppendLine("        /// <summary>台帳の綴り。区切って持ち、読むときに繋ぐ。</summary>");
            text.AppendLine("        internal static readonly string[] Chunks =");
            text.AppendLine("        {");

            foreach (string chunk in Split(catalog))
            {
                text.Append("            ").Append(Quote(chunk)).AppendLine(",");
            }

            text.AppendLine("        };");
            text.AppendLine("    }");
            text.AppendLine("}");

            return text.ToString();
        }

        /// <summary>区切る。代用対は片割れで切らない。</summary>
        private static IEnumerable<string> Split(string catalog)
        {
            int at = 0;
            while (at < catalog.Length)
            {
                int take = Math.Min(ChunkChars, catalog.Length - at);
                if (take == ChunkChars && char.IsHighSurrogate(catalog[at + take - 1]))
                {
                    take--;
                }

                yield return catalog.Substring(at, take);
                at += take;
            }
        }

        private static string Quote(string value)
        {
            StringBuilder text = new StringBuilder("\"");
            foreach (char one in value)
            {
                if (one == '"' || one == '\\')
                {
                    text.Append('\\').Append(one);
                }
                else if (one >= ' ' && one <= '~')
                {
                    text.Append(one);
                }
                else
                {
                    text.Append("\\u").Append(((int)one).ToString("x4", CultureInfo.InvariantCulture));
                }
            }

            return text.Append('"').ToString();
        }
    }
}
