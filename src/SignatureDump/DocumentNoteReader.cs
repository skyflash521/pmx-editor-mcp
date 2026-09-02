using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 配布物のドキュメントXMLから、公開プロパティの記載を取り出す。名前を採るのも、決め方を
    /// 分けるために同一型内で数えるのも同じ文字列を見るので、取り出し方をここ1つに置く。
    /// </summary>
    public static class DocumentNoteReader
    {
        private const string MemberPrefix = "P:";

        private static readonly string[] AccessorSuffixes = { "get/set", "get", "set" };

        /// <summary>
        /// member 名(接頭辞 <c>P:</c> を除いたもの)から記載への対応を返す。記載を取り出せない
        /// member は入れない。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static IDictionary<string, string> Read(string xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            Dictionary<string, string> notes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement member in Parse(xml).Descendants("member"))
            {
                XAttribute name = member.Attribute("name");
                if (name == null || name.Value.Length == 0)
                {
                    throw new FormatException("member に name が無い。");
                }

                if (!name.Value.StartsWith(MemberPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string key = name.Value.Substring(MemberPrefix.Length);
                if (key.Length == 0)
                {
                    throw new FormatException("member の name が接頭辞だけになっている。");
                }

                string note = NoteOf(member, key);
                if (note.Length == 0)
                {
                    continue;
                }

                if (notes.ContainsKey(key))
                {
                    throw new FormatException("同じ member が二度現れる: " + key);
                }

                notes.Add(key, note);
            }

            return new ReadOnlyDictionary<string, string>(notes);
        }

        /// <summary>
        /// 列挙側の型表記とプロパティ名から、ドキュメントXMLの member 名を組み立てる。XMLは入れ子を
        /// 点で区切り、総称型をその段自身の型引数の数で表すので、その表記へそろえる。
        /// </summary>
        public static string MemberName(string typeName, string propertyName)
        {
            RequireText(typeName, nameof(typeName));
            RequireText(propertyName, nameof(propertyName));

            StringBuilder built = new StringBuilder();
            foreach (string level in Levels(typeName))
            {
                if (built.Length != 0)
                {
                    built.Append('.');
                }

                built.Append(WithArity(level));
            }

            return built.Append('.').Append(propertyName).ToString();
        }

        private static string NoteOf(XElement member, string key)
        {
            List<XElement> summaries = member.Elements("summary").ToList();
            if (summaries.Count == 0)
            {
                return string.Empty;
            }

            if (summaries.Count > 1)
            {
                throw new FormatException("member が summary を二つ以上持つ: " + key);
            }

            if (summaries[0].HasElements)
            {
                throw new FormatException("summary が子要素を持つ: " + key);
            }

            return NoteOf(summaries[0].Value);
        }

        // 配布物のXMLは、名前に続けて意味の補足を書く箇所を、改行と縦棒の両方で作る。
        private static string NoteOf(string summary)
        {
            foreach (string line in summary.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length != 0)
                {
                    return WithoutAccessorSuffix(BeforeVerticalBar(trimmed));
                }
            }

            return string.Empty;
        }

        private static string BeforeVerticalBar(string line)
        {
            int bar = line.IndexOf('|');
            return bar < 0 ? line : line.Substring(0, bar).TrimEnd();
        }

        private static string WithoutAccessorSuffix(string line)
        {
            int boundary = line.Length;
            while (boundary > 0 && !char.IsWhiteSpace(line[boundary - 1]))
            {
                boundary--;
            }

            string last = line.Substring(boundary);
            foreach (string suffix in AccessorSuffixes)
            {
                if (string.Equals(last, suffix, StringComparison.Ordinal))
                {
                    return line.Substring(0, boundary).TrimEnd();
                }
            }

            return line;
        }

        private static IEnumerable<string> Levels(string typeName)
        {
            List<string> levels = new List<string>();
            int depth = 0;
            int start = 0;
            for (int index = 0; index < typeName.Length; index++)
            {
                char letter = typeName[index];
                if (letter == '<')
                {
                    depth++;
                }
                else if (letter == '>')
                {
                    depth--;
                }
                else if (letter == '+' && depth == 0)
                {
                    levels.Add(typeName.Substring(start, index - start));
                    start = index + 1;
                }
            }

            levels.Add(typeName.Substring(start));
            return levels;
        }

        private static string WithArity(string level)
        {
            int open = level.IndexOf('<');
            if (open < 0)
            {
                return level;
            }

            if (!level.EndsWith(">", StringComparison.Ordinal))
            {
                throw new FormatException("総称型の山括弧が閉じていない: " + level);
            }

            return level.Substring(0, open) + "`"
                + TypeDefinitionName.Arguments(level).Count();
        }

        private static XDocument Parse(string xml)
        {
            try
            {
                using (StringReader text = new StringReader(xml))
                using (XmlReader reader = XmlReader.Create(
                    text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                {
                    return XDocument.Load(reader);
                }
            }
            catch (XmlException exception)
            {
                throw new FormatException("XMLとして読めない。", exception);
            }
        }

        private static void RequireText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }
        }
    }
}
