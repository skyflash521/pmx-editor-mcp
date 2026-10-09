using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>一次資料が要素数を定めていない並びの上限を導く。</summary>
    public static class ElementLimitRule
    {
        /// <summary>対象の全要素へ同じものを配る組の名前。</summary>
        public const string DistributedName = "args";

        /// <summary>UTF-8の1文字が使う最大のバイト数。</summary>
        private const int BytesPerChar = 4;

        /// <summary>IPCの要求の外枠が使う構造トークン数。</summary>
        private const int EnvelopeTokens = 4;

        /// <summary>
        /// 分岐の要求の並びごとの上限。配る組の内側の並びは上限を持たず、返す表に現れない。
        /// 構造トークンの残りが並びに足りないか、要素が想定文字数を
        /// 持たなければ<see cref="InvalidOperationException"/>。<paramref name="bounded"/> は
        /// ホストが要素数の上限を宣言した並びのその上限を返し、宣言が無ければ null を返す。
        /// </summary>
        public static IDictionary<SchemaItem, int> Request(
            SchemaBranch branch,
            AssumedLength lengths,
            int budgetBytes,
            int tokenLimit,
            Func<SchemaItem, int?> bounded = null)
        {
            if (branch == null)
            {
                throw new ArgumentNullException(nameof(branch));
            }

            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            Dictionary<SchemaItem, int> limits = new Dictionary<SchemaItem, int>();
            IList<SchemaItem> sent = Sent(branch);
            IList<IList<SchemaItem>> paths = sent
                .SelectMany(i => Arrays(i, new SchemaItem[0])).ToList();
            if (paths.Count == 0)
            {
                return limits;
            }

            int share = (tokenLimit - Envelope(sent)) / Simultaneous(branch, sent);
            if (share < 1)
            {
                throw new InvalidOperationException(
                    "構造トークンの残りが並びに足りない: " + branch.Branch);
            }

            foreach (IList<SchemaItem> path in paths)
            {
                SchemaItem array = path[path.Count - 1];
                int chars = lengths.Of(array.Element);
                if (chars < 1)
                {
                    throw new InvalidOperationException(
                        "1件の想定文字数が0の並びがある: " + (array.Name ?? "名前無し"));
                }

                Func<SchemaItem, int?> fixedCount = i => i.MaxItems ?? (bounded == null ? null : bounded(i));
                long counted = Counted(path, fixedCount);
                long capacity = Math.Min(
                    budgetBytes / (counted * chars * BytesPerChar),
                    share / (counted * (Tokens(array.Element) + 1)));
                int? declared = fixedCount(array);
                if (declared.HasValue)
                {
                    int found;
                    long own = Math.Min(declared.Value, AtLeastOne(capacity));
                    limits[array] = limits.TryGetValue(array, out found)
                        ? (int)Math.Min(found, own)
                        : (int)own;
                }

                IList<SchemaItem> shared = path.Where(i => !fixedCount(i).HasValue).ToList();
                long each = AtLeastOne(Root(capacity, shared.Count));
                foreach (SchemaItem sequence in shared)
                {
                    int found;
                    limits[sequence] = limits.TryGetValue(sequence, out found)
                        ? (int)Math.Min(found, each)
                        : (int)each;
                }
            }

            return limits;
        }

        /// <summary>応答の並びの上限。</summary>
        public static int Response(SchemaItem element, AssumedLength lengths, int valueChars)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            return (int)AtLeastOne(valueChars / (lengths.Of(element) + 1));
        }

        /// <summary>
        /// 要求の並びの件数が応答の並びの件数を下から決めるときの、その要求の並びの上限。応答で
        /// 返せない件数を要求で受けると、上限どおりの要求がいつも応答の大きさで落ちる。
        /// </summary>
        public static int Bounded(int requestLimit, int responseLimit)
        {
            return Math.Min(requestLimit, responseLimit);
        }

        /// <summary>ハンドルを新しく発行するツールの分岐が `count` に持つ上限。</summary>
        public static int Issued(IEnumerable<int> requestLimits, int responseLimit)
        {
            if (requestLimits == null)
            {
                throw new ArgumentNullException(nameof(requestLimits));
            }

            return requestLimits.Concat(new[] { responseLimit }).Min();
        }

        /// <summary>
        /// 上限を導く並びを、それを囲む並びとともに外側から順に並べたもの。並びの末尾が上限を導く
        /// 並びで、手前がそれを囲む並びになる。
        /// </summary>
        private static IEnumerable<IList<SchemaItem>> Arrays(
            SchemaItem item, IList<SchemaItem> enclosing)
        {
            if (string.Equals(item.Name, DistributedName, StringComparison.Ordinal))
            {
                yield break;
            }

            IList<SchemaItem> inner = enclosing;
            if (item.Element != null)
            {
                inner = enclosing.Concat(new[] { item }).ToList();
                if (!item.MaxItems.HasValue)
                {
                    yield return inner;
                }
            }

            foreach (IList<SchemaItem> path in Inner(item).SelectMany(i => Arrays(i, inner)))
            {
                yield return path;
            }
        }

        private static long Counted(IList<SchemaItem> path, Func<SchemaItem, int?> fixedCount)
        {
            long counted = 1;
            foreach (SchemaItem enclosing in path.Take(path.Count - 1))
            {
                int? count = fixedCount(enclosing);
                if (count.HasValue)
                {
                    counted *= count.Value;
                }
            }

            return counted;
        }

        /// <summary><paramref name="count"/> 乗して <paramref name="capacity"/> を超えない最大の数。</summary>
        private static long Root(long capacity, int count)
        {
            if (count < 2)
            {
                return capacity;
            }

            long root = AtLeastOne((long)Math.Pow(Math.Max(capacity, 0), 1.0 / count));
            while (Power(root + 1, count, capacity) <= capacity)
            {
                root++;
            }

            while (root > 1 && Power(root, count, capacity) > capacity)
            {
                root--;
            }

            return root;
        }

        /// <summary>
        /// <paramref name="value"/> の <paramref name="count"/> 乗。
        /// <paramref name="ceiling"/> を超えた時点で打ち切る。
        /// </summary>
        private static long Power(long value, int count, long ceiling)
        {
            long power = 1;
            for (int i = 0; i < count; i++)
            {
                power *= value;
                if (power > ceiling)
                {
                    return power;
                }
            }

            return power;
        }

        /// <summary>
        /// 同時に指定できる可変長の並びの数。同時には持てない項目のまとまりからは1つぶんだけを数える。
        /// </summary>
        private static int Simultaneous(SchemaBranch branch, IList<SchemaItem> sent)
        {
            HashSet<string> chosen = new HashSet<string>(
                branch.Choices.SelectMany(c => c.Names), StringComparer.Ordinal);
            int count = sent
                .Where(i => i.Name == null || !chosen.Contains(i.Name))
                .Sum(i => Arrays(i, new SchemaItem[0]).Count());
            foreach (SchemaChoice choice in branch.Choices)
            {
                count += choice.Names
                    .Select(n => sent
                        .Where(i => string.Equals(i.Name, n, StringComparison.Ordinal))
                        .Sum(i => Arrays(i, new SchemaItem[0]).Count()))
                    .Max();
            }

            return count;
        }

        /// <summary>呼び出す側が実際に送る入力。ホストが自分で入れる引数は含まない。</summary>
        private static IList<SchemaItem> Sent(SchemaBranch branch)
        {
            return branch.Inputs.Where(i => !i.Injected).ToList();
        }

        /// <summary>要求の外枠と、ツールが受け取る固定のメンバーが使う構造トークン数。</summary>
        private static int Envelope(IList<SchemaItem> sent)
        {
            return EnvelopeTokens + 1 + (sent.Count - 1) + sent.Sum(i => Tokens(i));
        }

        /// <summary>項目1件が使う最大の構造トークン数。上限を導く並びは0とする。</summary>
        private static int Tokens(SchemaItem item)
        {
            if (item.Members != null)
            {
                return 1 + Math.Max(item.Members.Count - 1, 0) + item.Members.Sum(m => Tokens(m));
            }

            if (item.Element != null)
            {
                return item.MaxItems.HasValue
                    ? item.MaxItems.Value * (Tokens(item.Element) + 1)
                    : 0;
            }

            return 0;
        }

        private static IEnumerable<SchemaItem> Inner(SchemaItem item)
        {
            return (item.Members ?? new SchemaItem[0])
                .Concat(item.Element == null ? new SchemaItem[0] : new[] { item.Element });
        }

        private static long AtLeastOne(long count)
        {
            return count < 1 ? 1 : count;
        }
    }
}
