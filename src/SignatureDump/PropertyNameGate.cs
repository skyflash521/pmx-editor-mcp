using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 日本語名の正本が、規則どおりに付いているかを検査する。名前が的確かどうかは測れないので、
    /// 機械で確かめられる範囲——名前を起こす項目の過不足、根拠の解決、同一型内の重複——に限る。
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
            IList<PropertyRecord> enumerated = properties.ToList();
            ISet<string> quotable = Quotable(enumerated, notes);
            RequireListedAreEnumerated(listed, enumerated);
            RequireAuthoredExactlyWhereTheNoteCannotBeQuoted(listed, enumerated, quotable);
            RequireAuthoredBasisResolves(listed, lineCount);
            RequireDistinctNamesWithinAType(listed, enumerated, notes, quotable);
        }

        /// <summary>
        /// 記載を引ける項目の鍵。同じ宣言型の中でその記載を持つ項目が1件だけのときに引ける。数える
        /// 母集合は記載が取れた対象の項目に限る。
        /// </summary>
        private static ISet<string> Quotable(
            IList<PropertyRecord> properties, IDictionary<string, string> notes)
        {
            Dictionary<string, Dictionary<string, int>> counts =
                new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (PropertyRecord property in properties)
            {
                string note = NoteOf(property, notes);
                if (note == null)
                {
                    continue;
                }

                Dictionary<string, int> within;
                if (!counts.TryGetValue(property.DeclaringType, out within))
                {
                    within = new Dictionary<string, int>(StringComparer.Ordinal);
                    counts.Add(property.DeclaringType, within);
                }

                within[note] = within.ContainsKey(note) ? within[note] + 1 : 1;
            }

            HashSet<string> quotable = new HashSet<string>(StringComparer.Ordinal);
            foreach (PropertyRecord property in properties)
            {
                string note = NoteOf(property, notes);
                if (note != null && counts[property.DeclaringType][note] == 1)
                {
                    quotable.Add(Pair(property));
                }
            }

            return quotable;
        }

        /// <summary>表の項目が列挙結果に実在し、二度現れないことを求める。</summary>
        private static void RequireListedAreEnumerated(
            IList<PropertyNameRecord> records, IList<PropertyRecord> properties)
        {
            HashSet<string> enumerated = new HashSet<string>(
                properties.Select(Pair), StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PropertyNameRecord record in records)
            {
                if (!enumerated.Contains(record.Key))
                {
                    throw new InvalidOperationException("列挙結果に無い項目が在る: " + record.Key);
                }

                if (!seen.Add(record.Key))
                {
                    throw new InvalidOperationException("表に同じ項目が二度在る: " + record.Key);
                }
            }
        }

        /// <summary>
        /// 表が持つのは、記載を引けない項目だけであることを求める。引ける項目の名前は記載から導ける
        /// ので、書けば導き直しを忘れたときにずれが残る。引けない項目を落とすと名前が決まらない。
        /// </summary>
        private static void RequireAuthoredExactlyWhereTheNoteCannotBeQuoted(
            IList<PropertyNameRecord> records,
            IList<PropertyRecord> properties,
            ISet<string> quotable)
        {
            HashSet<string> listed = new HashSet<string>(
                records.Select(r => r.Key), StringComparer.Ordinal);
            string written = records.Select(r => r.Key).Where(quotable.Contains)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (written != null)
            {
                throw new InvalidOperationException("記載を引ける項目は表に置かない: " + written);
            }

            string missing = properties.Select(Pair).Where(k => !quotable.Contains(k))
                .Except(listed, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("名前を起こす項目が表に無い: " + missing);
            }
        }

        private static void RequireAuthoredBasisResolves(
            IList<PropertyNameRecord> records, Func<string, int> lineCount)
        {
            foreach (PropertyNameRecord record in records
                .Where(r => r.Basis.Kind == NameBasisKind.DocumentSection))
            {
                int lines = lineCount(record.Basis.Path);
                if (lines < 0)
                {
                    throw new InvalidOperationException("根拠の資料が無い: " + record.Basis.Path);
                }

                if (record.Basis.LastLine > lines)
                {
                    throw new InvalidOperationException(
                        "根拠の行が資料の行数を超える: " + record.Key + " "
                        + record.Basis.LastLine.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// 同じ宣言型の中で日本語名が重ならないことを、導いた名前と起こした名前の両方で見る。導いた
        /// 名前どうしは記載が一意なので重ならないが、起こした名前とは重なりうる。
        /// </summary>
        private static void RequireDistinctNamesWithinAType(
            IList<PropertyNameRecord> records,
            IList<PropertyRecord> properties,
            IDictionary<string, string> notes,
            ISet<string> quotable)
        {
            Dictionary<string, string> authored = records.ToDictionary(
                r => r.Key, r => r.JapaneseName, StringComparer.Ordinal);
            foreach (IGrouping<string, PropertyRecord> within in properties
                .GroupBy(p => p.DeclaringType, StringComparer.Ordinal))
            {
                IGrouping<string, string> repeated = within
                    .Select(p => quotable.Contains(Pair(p)) ? NoteOf(p, notes) : authored[Pair(p)])
                    .GroupBy(n => n, StringComparer.Ordinal)
                    .FirstOrDefault(g => g.Count() > 1);
                if (repeated != null)
                {
                    throw new InvalidOperationException(
                        "同じ型の中で日本語名が重なる: " + within.Key + " " + repeated.Key);
                }
            }
        }

        /// <summary>宣言型とメンバー名の組。表の項目と列挙結果を突き合わせる鍵。</summary>
        private static string Pair(PropertyRecord property)
        {
            return property.DeclaringType + "|" + property.MemberName;
        }

        private static string NoteOf(PropertyRecord property, IDictionary<string, string> notes)
        {
            string note;
            return notes.TryGetValue(
                DocumentNoteReader.MemberName(property.DeclaringType, property.MemberName), out note)
                ? note
                : null;
        }
    }
}
