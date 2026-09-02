using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型役割表の日本語名が、規則どおりに付いているかを検査する。名前が的確かどうかは測れないので、
    /// 機械で確かめられる範囲——項目の過不足、決め方の強制、根拠の解決、同一型内の重複——に限る。
    /// </summary>
    public static class PropertyNameGate
    {
        /// <summary>
        /// 規則に反していれば <see cref="InvalidOperationException"/>。
        /// <paramref name="lineCount"/> は配布物からの相対パスを受け、その資料の行数を返す。資料が
        /// 無ければ負を返すこと。
        /// </summary>
        public static void Require(
            IEnumerable<PropertyNameRecord> records,
            IEnumerable<PropertyRecord> properties,
            IDictionary<string, string> notes,
            Func<string, int> lineCount)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            if (notes == null)
            {
                throw new ArgumentNullException(nameof(notes));
            }

            if (lineCount == null)
            {
                throw new ArgumentNullException(nameof(lineCount));
            }

            IList<PropertyNameRecord> listed = records.ToList();
            RequireSameItems(listed, properties.ToList());
            RequireDecisionMatchesTheNoteCount(listed, notes);
            RequireQuotedNamesComeFromTheNote(listed, notes);
            RequireAuthoredBasisResolves(listed, lineCount);
            RequireDistinctNamesWithinAType(listed);
        }

        /// <summary>
        /// 表の項目と列挙結果を一対一で突き合わせる。これが無いと、公開プロパティを丸ごと書き落として
        /// も、同じプロパティを別の名前で二重に載せても、残った項目だけが条件を満たして通る。
        /// </summary>
        private static void RequireSameItems(
            IList<PropertyNameRecord> records, IList<PropertyRecord> properties)
        {
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (PropertyNameRecord record in records)
            {
                if (!listed.Add(record.Property.Key))
                {
                    throw new InvalidOperationException("表に同じ項目が二度在る: " + record.Property.Key);
                }
            }

            HashSet<string> enumerated = new HashSet<string>(
                properties.Select(p => p.Key), StringComparer.Ordinal);

            string missing = enumerated.Except(listed, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("表に無い項目が在る: " + missing);
            }

            string extra = listed.Except(enumerated, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("列挙結果に無い項目が在る: " + extra);
            }
        }

        /// <summary>
        /// 決め方は書き手が選べない。これを課さないと、一意な記載がある項目を勝手に起こしたり、同じ
        /// 記載を持つ項目群の1件だけを残して他へ別の名前を付けたりできてしまう。
        /// </summary>
        private static void RequireDecisionMatchesTheNoteCount(
            IList<PropertyNameRecord> records, IDictionary<string, string> notes)
        {
            Dictionary<string, Dictionary<string, int>> counts =
                new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (PropertyNameRecord record in records)
            {
                string note = NoteOf(record, notes);
                if (note == null)
                {
                    continue;
                }

                Dictionary<string, int> within;
                if (!counts.TryGetValue(record.Property.DeclaringType, out within))
                {
                    within = new Dictionary<string, int>(StringComparer.Ordinal);
                    counts.Add(record.Property.DeclaringType, within);
                }

                within[note] = within.ContainsKey(note) ? within[note] + 1 : 1;
            }

            foreach (PropertyNameRecord record in records)
            {
                string note = NoteOf(record, notes);
                bool quotable = note != null
                    && counts[record.Property.DeclaringType][note] == 1;
                NameDecision expected = quotable ? NameDecision.Quoted : NameDecision.Authored;
                if (record.Decision != expected)
                {
                    throw new InvalidOperationException(
                        "決め方が記載の出現数と合わない: " + record.Property.Key);
                }
            }
        }

        private static void RequireQuotedNamesComeFromTheNote(
            IList<PropertyNameRecord> records, IDictionary<string, string> notes)
        {
            foreach (PropertyNameRecord record in records.Where(r => r.Decision == NameDecision.Quoted))
            {
                if (!string.Equals(record.JapaneseName, NoteOf(record, notes), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("名前が記載と違う: " + record.Property.Key);
                }
            }
        }

        private static void RequireAuthoredBasisResolves(
            IList<PropertyNameRecord> records, Func<string, int> lineCount)
        {
            foreach (PropertyNameRecord record in records
                .Where(r => r.Decision == NameDecision.Authored
                    && r.Basis.Kind == NameBasisKind.DocumentSection))
            {
                int lines = lineCount(record.Basis.Path);
                if (lines < 0)
                {
                    throw new InvalidOperationException("根拠の資料が無い: " + record.Basis.Path);
                }

                if (record.Basis.LastLine > lines)
                {
                    throw new InvalidOperationException(
                        "根拠の行が資料の行数を超える: " + record.Property.Key + " "
                        + record.Basis.LastLine.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private static void RequireDistinctNamesWithinAType(IList<PropertyNameRecord> records)
        {
            foreach (IGrouping<string, PropertyNameRecord> within in records
                .GroupBy(r => r.Property.DeclaringType, StringComparer.Ordinal))
            {
                IGrouping<string, PropertyNameRecord> repeated = within
                    .GroupBy(r => r.JapaneseName, StringComparer.Ordinal)
                    .FirstOrDefault(g => g.Count() > 1);
                if (repeated != null)
                {
                    throw new InvalidOperationException(
                        "同じ型の中で日本語名が重なる: " + within.Key + " " + repeated.Key);
                }
            }
        }

        private static string NoteOf(PropertyNameRecord record, IDictionary<string, string> notes)
        {
            string note;
            return notes.TryGetValue(
                DocumentNoteReader.MemberName(record.Property.DeclaringType, record.Property.MemberName),
                out note)
                ? note
                : null;
        }
    }
}
