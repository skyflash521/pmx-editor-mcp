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
        /// 分岐の要求の並びごとの上限。配る組の内側の並びは、要求の大きさが対象の件数で変わらない
        /// ので上限を持たず、返す表に現れない。構造トークンの残りが並びに足りないか、要素が想定
        /// 文字数を持たなければ <see cref="InvalidOperationException"/>。
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
                long occurrences = Occurrences(path, limits);
                int chars = lengths.Of(array.Element);
                if (chars < 1)
                {
                    throw new InvalidOperationException(
                        "1件の想定文字数が0の並びがある: " + (array.Name ?? "名前無し"));
                }

                limits.Add(array, (int)Math.Min(
                    AtLeastOne(budgetBytes / (occurrences * chars * BytesPerChar)),
                    AtLeastOne(share / (occurrences * (Tokens(array.Element) + 1)))));
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

        /// <summary>その並びが1回の要求に現れる回数。囲む並びの最大件数の積になる。</summary>
        private static long Occurrences(
            IList<SchemaItem> path, IDictionary<SchemaItem, int> limits)
        {
            long occurrences = 1;
            foreach (SchemaItem enclosing in path.Take(path.Count - 1))
            {
                occurrences *= enclosing.MaxItems ?? limits[enclosing];
            }

            return occurrences;
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
