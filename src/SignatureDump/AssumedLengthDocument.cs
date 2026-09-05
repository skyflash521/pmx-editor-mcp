using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 共通契約仕様書から綴りごとの想定文字数の表を読む。値は測り直して置き換えるものなので、
    /// 写さずその都度読む。
    /// </summary>
    public static class AssumedLengthDocument
    {
        public const string SectionHeading = "#### 想定文字数";

        /// <summary>節が無いか行が読めなければ <see cref="InvalidOperationException"/>。</summary>
        public static IDictionary<string, int> Read(string text)
        {
            Dictionary<string, int> lengths = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string[] cells in SpecificationTable.Rows(text, SectionHeading))
            {
                string spelling = SpecificationTable.Quoted(cells[0], string.Join(" | ", cells));
                int length;
                if (!int.TryParse(
                    cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out length)
                    || length < 1)
                {
                    throw new InvalidOperationException(
                        "想定文字数が1以上の整数でない: " + spelling + " | " + cells[1]);
                }

                if (lengths.ContainsKey(spelling))
                {
                    throw new InvalidOperationException("同じ綴りが二度現れる: " + spelling);
                }

                lengths.Add(spelling, length);
            }

            if (lengths.Count == 0)
            {
                throw new InvalidOperationException("表に行が無い: " + SectionHeading);
            }

            return new ReadOnlyDictionary<string, int>(lengths);
        }
    }
}
