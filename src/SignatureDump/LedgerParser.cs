using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の表を機械可読な行の並びへ写す。台帳と公開APIの一覧を突き合わせる側が名前を
    /// 解決できるよう、対象の列を書き方で分けて、挙げられている名前を取り出しておく。名前が
    /// 型かメンバーかまでは決めない(<see cref="CapabilityRecord.TargetNames"/> を見よ)。
    ///
    /// 読み落としは後段の検査そのものを黙って素通りさせるので、能力の表と分かった範囲の中では
    /// 読めない行を読み飛ばさず例外にする。
    /// </summary>
    public static class LedgerParser
    {
        private const string GroupSeparator = " / ";

        private const char PatternMark = '*';

        private const char CellSeparator = '|';

        private const char Escape = '\\';

        /// <summary>
        /// 見出し・引用・箇条書き・囲み・生のHTML・リンクの参照定義など、別の構造の始まりを表す字。
        /// </summary>
        private const string BlockMarks = "#>-*+_=`~<[";

        private static readonly ReadOnlyCollection<string> HeaderCells =
            Array.AsReadOnly(new[] { "ID", "大分類", "対象", "分類", "担当", "備考" });

        /// <summary>
        /// 型引数の数は1以上で、先頭に0を置いた書き方もしない。この形に合わない接尾辞を落とすと、
        /// 台帳の誤記が実在する非総称型の名前へ化けて、後段の照合を黙って通る。
        /// </summary>
        private static readonly Regex GenericAritySuffix =
            new Regex("`[1-9][0-9]*$", RegexOptions.CultureInvariant);

        private static readonly Regex SeparatorCell =
            new Regex("^:?-+:?$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, CapabilityStatus> Statuses =
            new Dictionary<string, CapabilityStatus>(StringComparer.Ordinal)
            {
                { "提供", CapabilityStatus.Provided },
                { "非対応", CapabilityStatus.NotSupported },
                { "要調査", CapabilityStatus.NeedsInvestigation },
            };

        private static readonly Dictionary<string, CapabilityOwner> Owners =
            new Dictionary<string, CapabilityOwner>(StringComparer.Ordinal)
            {
                { string.Empty, CapabilityOwner.None },
                { "モデル", CapabilityOwner.Model },
                { "セッション", CapabilityOwner.Session },
                { "ビュー", CapabilityOwner.View },
                { "変形・モーション", CapabilityOwner.MotionTransform },
            };

        /// <summary>
        /// 台帳の本文から能力の行を取り出す。能力の表の外は読み飛ばす。表が終わるのは、空行か、
        /// 別の構造が始まる行に出会ったときだけとする。縦棒を失っただけの行で表が終わったことに
        /// すると、以降の能力を黙って読み落とす。
        /// </summary>
        public static IList<CapabilityRecord> Parse(string markdown)
        {
            if (markdown == null)
            {
                throw new ArgumentNullException(nameof(markdown));
            }

            string[] lines = markdown.Split('\n');
            int header = FindHeader(lines);
            if (header < 0)
            {
                return new List<CapabilityRecord>().AsReadOnly();
            }

            RequireSeparator(lines, header + 1);

            List<CapabilityRecord> records = new List<CapabilityRecord>();
            for (int i = header + 2; i < lines.Length && !StartsBlock(lines[i]); i++)
            {
                string[] cells = SplitCells(lines[i]);
                if (cells == null)
                {
                    // 下線で示す見出しの本文は、行そのものからは普通の文と区別できない。
                    if (IsUnderline(lines, i + 1))
                    {
                        break;
                    }

                    throw Malformed(i, "能力の表の行に列を隔てる縦棒が無い");
                }

                if (cells.Length != HeaderCells.Count)
                {
                    throw Malformed(i, "能力の表の行が列の数に合わない");
                }

                records.Add(BuildRecord(cells));
            }

            return records.AsReadOnly();
        }

        /// <summary>
        /// 見出しを起点にすることで、同じ列の数を持つ別の表や、表を作らない単独の行を能力として
        /// 拾わずに済む。
        /// </summary>
        private static int FindHeader(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string[] cells = SplitCells(lines[i]);
                if (cells != null && HasHeaderCells(cells))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasHeaderCells(string[] cells)
        {
            if (cells.Length != HeaderCells.Count)
            {
                return false;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (!string.Equals(cells[i], HeaderCells[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireSeparator(string[] lines, int index)
        {
            string[] cells = index < lines.Length ? SplitCells(lines[index]) : null;
            if (cells == null || cells.Length != HeaderCells.Count || !AllSeparators(cells))
            {
                throw Malformed(index, "能力の表の見出しに続く区切りの行が無い");
            }
        }

        private static bool AllSeparators(string[] cells)
        {
            foreach (string cell in cells)
            {
                if (!SeparatorCell.IsMatch(cell))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsBlock(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return true;
            }

            return BlockMarks.IndexOf(trimmed[0]) >= 0 || StartsOrderedItem(trimmed);
        }

        private static bool IsUnderline(string[] lines, int index)
        {
            if (index >= lines.Length)
            {
                return false;
            }

            string trimmed = lines[index].Trim();
            if (trimmed.Length == 0 || (trimmed[0] != '=' && trimmed[0] != '-'))
            {
                return false;
            }

            foreach (char c in trimmed)
            {
                if (c != trimmed[0])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsOrderedItem(string trimmed)
        {
            int i = 0;
            while (i < trimmed.Length && trimmed[i] >= '0' && trimmed[i] <= '9')
            {
                i++;
            }

            return i > 0 && i < trimmed.Length && (trimmed[i] == '.' || trimmed[i] == ')');
        }

        /// <summary>
        /// 行を列へ分ける。縦棒を1つも持たない行には null を返す。列を隔てる縦棒さえあれば行として
        /// 扱い、行の両端の縦棒は省かれていてもよい。両端を必須にすると、端の縦棒が落ちた行を表の
        /// 行として読めなくなる。
        /// </summary>
        private static string[] SplitCells(string line)
        {
            string trimmed = line.Trim();
            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool separated = false;
            bool endsWithSeparator = false;

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == Escape && i + 1 < trimmed.Length && IsEscapable(trimmed[i + 1]))
                {
                    // 縦棒はセルの中身としても書けるので、逃がした縦棒を列の区切りにしない。
                    // 逃がす対象でないバックスラッシュはセルの中身なので、落とさずそのまま残す。
                    cell.Append(trimmed[++i]);
                    endsWithSeparator = false;
                }
                else if (c == CellSeparator)
                {
                    separated = true;
                    endsWithSeparator = i == trimmed.Length - 1;
                    cells.Add(cell.ToString().Trim());
                    cell.Length = 0;
                }
                else
                {
                    cell.Append(c);
                    endsWithSeparator = false;
                }
            }

            if (!separated)
            {
                return null;
            }

            cells.Add(cell.ToString().Trim());
            if (trimmed[0] == CellSeparator)
            {
                cells.RemoveAt(0);
            }

            if (endsWithSeparator && cells.Count > 0)
            {
                cells.RemoveAt(cells.Count - 1);
            }

            return cells.ToArray();
        }

        private static bool IsEscapable(char c)
        {
            return c == CellSeparator || c == Escape;
        }

        private static CapabilityRecord BuildRecord(string[] cells)
        {
            string id = cells[0];
            string target = cells[2];
            CapabilityTargetKind kind = ClassifyTarget(target);
            CapabilityStatus status = Lookup(Statuses, cells[3], "分類", id);
            CapabilityOwner owner = Lookup(Owners, cells[4], "担当", id);
            RequireOwnerMatchesStatus(id, status, owner);

            return new CapabilityRecord(
                id, cells[1], target, kind, ExtractNames(target, kind), status, owner, cells[5]);
        }

        private static CapabilityTargetKind ClassifyTarget(string target)
        {
            // まとめて指す書き方は散文を伴うことがあり、その散文に区切りと同じ字が現れ得るので先に見る。
            if (target.IndexOf(PatternMark) >= 0)
            {
                return CapabilityTargetKind.Pattern;
            }

            return target.IndexOf(GroupSeparator, StringComparison.Ordinal) >= 0
                ? CapabilityTargetKind.Group
                : CapabilityTargetKind.Single;
        }

        private static ReadOnlyCollection<string> ExtractNames(string target, CapabilityTargetKind kind)
        {
            if (kind == CapabilityTargetKind.Pattern)
            {
                return Array.AsReadOnly(new string[0]);
            }

            string[] names = kind == CapabilityTargetKind.Group
                ? target.Split(new[] { GroupSeparator }, StringSplitOptions.None)
                : new[] { target };

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = GenericAritySuffix.Replace(names[i].Trim(), string.Empty);
            }

            return Array.AsReadOnly(names);
        }

        /// <summary>
        /// 知らない語で黙って既定値へ倒すと、台帳の行が誤った分類・担当のまま突き合わせへ
        /// 流れるため、その場で止める。
        /// </summary>
        private static TValue Lookup<TValue>(
            Dictionary<string, TValue> table, string cell, string columnName, string id)
        {
            TValue value;
            if (!table.TryGetValue(cell, out value))
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture, "{0} の{1}の列に知らない値がある: {2}", id, columnName, cell));
            }

            return value;
        }

        /// <summary>
        /// 台帳は担当を、分類が提供の能力を担当するツール契約仕様書として定めている。片方だけを
        /// 満たす行は、担当を持たない能力へ契約が割り当てられるか、その逆になる。
        /// </summary>
        private static void RequireOwnerMatchesStatus(
            string id, CapabilityStatus status, CapabilityOwner owner)
        {
            if ((status == CapabilityStatus.Provided) == (owner != CapabilityOwner.None))
            {
                return;
            }

            throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                "{0} は分類と担当が食い違う。担当は分類が提供の行にだけ書く: 分類={1} 担当={2}",
                id,
                status,
                owner));
        }

        private static FormatException Malformed(int index, string reason)
        {
            return new FormatException(string.Format(
                CultureInfo.InvariantCulture, "{0}行目: {1}", index + 1, reason));
        }
    }
}
