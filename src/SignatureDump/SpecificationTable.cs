using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 仕様書の本文に置かれた2列の表を読む。正本は本文なので、節の見出しで場所を決め、そこから
    /// 続く表だけを読む。
    /// </summary>
    public static class SpecificationTable
    {
        /// <summary>
        /// 見出しで場所を決め、そこから続く2列の表の本文の行を返す。引数の不備は呼んだ時点で
        /// 知らせる——数え上げるまで遅らせると、呼び出し元を見ても誤りの出どころが分からない。
        /// </summary>
        public static IEnumerable<string[]> Rows(string text, string heading)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (heading == null)
            {
                throw new ArgumentNullException(nameof(heading));
            }

            return Scan(text, heading);
        }

        private static IEnumerable<string[]> Scan(string text, string heading)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(
                lines, l => string.Equals(l.Trim(), heading, StringComparison.Ordinal));
            if (start < 0)
            {
                throw new InvalidOperationException("節が無い: " + heading);
            }

            bool inBody = false;
            for (int index = start + 1; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    break;
                }

                if (!line.StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] cells = Cells(line);
                if (cells.Length != 2)
                {
                    throw new InvalidOperationException("表の行が2列でない: " + line);
                }

                if (IsSeparator(cells))
                {
                    inBody = true;
                    continue;
                }

                if (inBody)
                {
                    yield return cells;
                }
            }
        }

        /// <summary>引用符で囲まれた欄の中身。囲まれていなければ例外。</summary>
        public static string Quoted(string cell, string line)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            if (!cell.StartsWith("`", StringComparison.Ordinal)
                || !cell.EndsWith("`", StringComparison.Ordinal)
                || cell.Length <= 2)
            {
                throw new InvalidOperationException("欄が引用符で囲まれていない: " + line);
            }

            return cell.Substring(1, cell.Length - 2);
        }

        private static string[] Cells(string line)
        {
            string body = line.Substring(1);
            if (body.EndsWith("|", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 1);
            }

            return body.Split('|').Select(c => c.Trim()).ToArray();
        }

        private static bool IsSeparator(string[] cells)
        {
            return cells.All(c => c.Length != 0 && c.All(x => x == '-' || x == ':'));
        }
    }
}
