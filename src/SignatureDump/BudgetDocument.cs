using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// アーキテクチャ仕様書から応答サイズ予算の既定を読む。逆算した件数はこの設定から決まるので、
    /// 値を写さずその都度読む。
    /// </summary>
    public static class BudgetDocument
    {
        public const string SectionHeading = "## 応答サイズ予算の設定";

        private static readonly Regex Default = new Regex(
            "^- 未設定時の既定は \\*\\*([0-9,]+)\\*\\*", RegexOptions.CultureInvariant);

        /// <summary>節が無いか既定を読めなければ <see cref="InvalidOperationException"/>。</summary>
        public static int ReadDefault(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(
                lines, l => string.Equals(l.Trim(), SectionHeading, StringComparison.Ordinal));
            if (start < 0)
            {
                throw new InvalidOperationException("節が無い: " + SectionHeading);
            }

            for (int index = start + 1; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    break;
                }

                Match match = Default.Match(line);
                if (match.Success)
                {
                    return int.Parse(
                        match.Groups[1].Value.Replace(",", string.Empty),
                        CultureInfo.InvariantCulture);
                }
            }

            throw new InvalidOperationException("既定の予算が読めない: " + SectionHeading);
        }
    }
}
