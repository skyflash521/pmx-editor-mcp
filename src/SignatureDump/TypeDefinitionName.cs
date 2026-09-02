using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型の表記を、総称型引数を引数の数へ置き換えた鍵へ写す。開いた定義と閉じた総称型が同じ鍵に
    /// なり、引数の数が違う同名の型は別の鍵のままになる。
    /// </summary>
    public static class TypeDefinitionName
    {
        /// <summary>総称型引数を引数の数へ置き換えた鍵。</summary>
        public static string Of(string typeName)
        {
            RequireText(typeName);

            StringBuilder builder = new StringBuilder();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (c == '<')
                {
                    depth++;
                    if (depth == 1)
                    {
                        start = i + 1;
                    }
                }
                else if (c == '>')
                {
                    depth--;
                    if (depth == 0)
                    {
                        builder.Append('<')
                            .Append(Split(typeName.Substring(start, i - start)).Count
                                .ToString(CultureInfo.InvariantCulture))
                            .Append('>');
                    }
                }
                else if (depth == 0)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>総称型の各段の引数。段ごとに引数を持つ入れ子の型では全段ぶんを返す。</summary>
        public static IEnumerable<string> Arguments(string typeName)
        {
            RequireText(typeName);

            List<string> arguments = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (c == '<')
                {
                    depth++;
                    if (depth == 1)
                    {
                        start = i + 1;
                    }
                }
                else if (c == '>')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arguments.AddRange(Split(typeName.Substring(start, i - start)));
                    }
                }
            }

            return arguments;
        }

        private static IList<string> Split(string inner)
        {
            List<string> parts = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '<' || c == '[')
                {
                    depth++;
                }
                else if (c == '>' || c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    parts.Add(inner.Substring(start, i - start));
                    start = i + 1;
                }
            }

            parts.Add(inner.Substring(start));

            return parts;
        }

        private static void RequireText(string typeName)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (typeName.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(typeName));
            }
        }
    }
}
