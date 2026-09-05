using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型ごとの表現の表の1行。</summary>
    public sealed class ValueShapeRow
    {
        public ValueShapeRow(string typeName, string shape)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            TypeName = typeName;
            Shape = shape;
        }

        /// <summary>型の綴り。</summary>
        public string TypeName { get; }

        /// <summary>表現の綴り。要素の表現を包む型では綴りが1つに決まらないので null。</summary>
        public string Shape { get; }
    }

    /// <summary>
    /// 共通契約仕様書から値の表現の表を読む。正本は仕様書の本文なので、節の見出しで場所を決め、
    /// そこから続く表だけを読む。
    /// </summary>
    public static class ValueShapeDocument
    {
        /// <summary>型ごとの表現の表を置く節の見出し。</summary>
        public const string SectionHeading = "### 型ごとの表現";

        /// <summary>表現の綴りの表を置く節の見出し。</summary>
        public const string SpellingHeading = "### 表現の綴り";

        /// <summary>
        /// 仕様書の本文から表現の綴りを読む。綴りの閉じた集合はこの表が持つので、綴りを名乗る
        /// 値はここに実在するかで確かめる。
        /// </summary>
        public static ISet<string> ReadSpellings(string text)
        {
            HashSet<string> spellings = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] cells in SpecificationTable.Rows(text, SpellingHeading))
            {
                spellings.Add(SpecificationTable.Quoted(cells[0], string.Join(" | ", cells)));
            }

            if (spellings.Count == 0)
            {
                throw new InvalidOperationException("表に行が無い: " + SpellingHeading);
            }

            return spellings;
        }

        /// <summary>仕様書の本文から表を読む。表が無いか行が読めなければ例外。</summary>
        public static IList<ValueShapeRow> Read(string text)
        {
            List<ValueShapeRow> rows = new List<ValueShapeRow>();
            foreach (string[] cells in SpecificationTable.Rows(text, SectionHeading))
            {
                rows.Add(new ValueShapeRow(
                    SpecificationTable.Quoted(cells[0], string.Join(" | ", cells)), Shape(cells[1])));
            }

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("表に行が無い: " + SectionHeading);
            }

            return new ReadOnlyCollection<ValueShapeRow>(rows);
        }

        /// <summary>表現の欄。綴りを1つ持つときだけその綴りを返す。</summary>
        private static string Shape(string cell)
        {
            return cell.StartsWith("`", StringComparison.Ordinal)
                && cell.EndsWith("`", StringComparison.Ordinal)
                && cell.Length > 2
                && cell.IndexOf('`', 1) == cell.Length - 1
                    ? cell.Substring(1, cell.Length - 2)
                    : null;
        }
    }
}
