using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 一次資料が要素数を定めていない並びの上限を導く。予算と構造トークンの上限から決まる値なので、
    /// 正本へ書かず、スキーマを組み立てるときにここで導く。
    /// </summary>
    public static class ElementLimitRule
    {
        /// <summary>対象の全要素へ同じものを配る組の名前。</summary>
        public const string DistributedName = "args";

        /// <summary>UTF-8の1文字が使う最大のバイト数。</summary>
        private const int BytesPerChar = 4;

        /// <summary>IPCの要求の外枠が使う構造トークン数。</summary>
        private const int EnvelopeTokens = 4;

        /// <summary>
        /// 分岐の要求の並びごとの上限。並びが入れ子になるときは、外側と内側の上限の積が予算へ
        /// 収まればよいので、その段数の乗根を段ごとの上限とする。一次資料が要素数を定めた並びを
        /// またぐときは、その要素数を積へ掛けてから分ける。配る組の内側の並びは、要求の大きさが
        /// 対象の件数で変わらないので上限を持たず、返す表に現れない。構造トークンの残りが並びに
        /// 足りないか、要素が想定文字数を持たなければ <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<SchemaItem, int> Request(
            SchemaBranch branch, AssumedLength lengths, int budgetBytes, int tokenLimit)
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

                long counted = Counted(path);
                IList<SchemaItem> shared = path.Where(i => !i.MaxItems.HasValue).ToList();
                long each = AtLeastOne(Root(
                    Math.Min(
                        budgetBytes / (counted * chars * BytesPerChar),
                        share / (counted * (Tokens(array.Element) + 1))),
                    shared.Count));
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

        /// <summary>
        /// ハンドルを新しく発行するツールの分岐が `count` に持つ上限。配列でない `count` には並びの
        /// 規則が上限を作らないので、その分岐の要求の並びと応答の並びの逆算値の最も小さいものを採る。
        /// </summary>
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

        /// <summary>
        /// その並びを囲む並びのうち、一次資料が要素数を定めたものの積。予算を段へ分ける前に、
        /// この回数だけ取り分ける。
        /// </summary>
        private static long Counted(IList<SchemaItem> path)
        {
            long counted = 1;
            foreach (SchemaItem enclosing in path.Take(path.Count - 1))
            {
                if (enclosing.MaxItems.HasValue)
                {
                    counted *= enclosing.MaxItems.Value;
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

        /// <summary>
        /// 呼び出す側が実際に送る入力。ホストが自分で入れる引数は要求に現れないので、要求の
        /// 大きさにも数えない。
        /// </summary>
        private static IList<SchemaItem> Sent(SchemaBranch branch)
        {
            return branch.Inputs.Where(i => !i.Injected).ToList();
        }

        /// <summary>要求の外枠と、ツールが受け取る固定のメンバーが使う構造トークン数。</summary>
        private static int Envelope(IList<SchemaItem> sent)
        {
            return EnvelopeTokens + 1 + (sent.Count - 1) + sent.Sum(i => Tokens(i));
        }

        /// <summary>
        /// 項目1件が使う最大の構造トークン数。上限を導く並びは0とする——その並びが使う分は、現れる
        /// 回数のぶんまで自分の予算が持つ。
        /// </summary>
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
