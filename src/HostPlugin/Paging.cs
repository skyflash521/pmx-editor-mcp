using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>一覧から切り出した1ページ。</summary>
    /// <typeparam name="T">並んでいるものの型。</typeparam>
    public sealed class Page<T>
    {
        public Page(IList<T> items, int total, int? nextOffset, IList<string> warnings)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            Items = new ReadOnlyCollection<T>(items);
            Total = total;
            NextOffset = nextOffset;
            Warnings = new ReadOnlyCollection<string>(warnings);
        }

        public IList<T> Items { get; }

        /// <summary>切り出す前の総数。</summary>
        public int Total { get; }

        /// <summary>続きがあるときの、次に渡す位置。無ければ null。</summary>
        public int? NextOffset { get; }

        /// <summary>件数を減らしたときに添える警告。減らしていなければ空。</summary>
        public IList<string> Warnings { get; }
    }

    public static class Paging
    {
        /// <summary>
        /// <paramref name="offset"/> の位置から <paramref name="limit"/> 件までを切り出す。
        /// <paramref name="measure"/> は、渡した並びを載せた値の全体の大きさをそのまま量るもので、
        /// 件数が増えたときに大きさが減らないようにする——ただし切り出したものを全件そのまま載せる
        /// ときだけは、続きの位置が付かないぶん小さくなってよい。その値が
        /// <paramref name="valueChars"/> を超えない最も多い件数まで減らす。1件も返せないときは偽。
        /// <paramref name="measure"/> には、1つの要素だけの並びと、同じ要素を2つ並べた並びも渡す。
        /// 枠に収まらなかった要素より後ろの要素は読まない。
        /// </summary>
        public static bool TryTake<T>(
            IList<T> all,
            int offset,
            int limit,
            int valueChars,
            Func<IList<T>, int> measure,
            out Page<T> page)
        {
            if (all == null)
            {
                throw new ArgumentNullException(nameof(all));
            }

            if (measure == null)
            {
                throw new ArgumentNullException(nameof(measure));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "0以上でなければならない。");
            }

            if (limit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "1以上でなければならない。");
            }

            page = null;
            int asked = Math.Max(0, Math.Min(limit, all.Count - offset));
            List<T> taken = Read(all, offset, asked, valueChars, measure);
            int fitted = Fit(taken, asked, valueChars, measure);
            if (asked > 0 && fitted == 0)
            {
                return false;
            }

            List<string> warnings = new List<string>();
            if (fitted < asked)
            {
                warnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "件数を減らした: 切り出した {0} 件のうち {1} 件を返した",
                    asked,
                    fitted));
            }

            int next = offset + fitted;
            page = new Page<T>(
                taken.Take(fitted).ToArray(),
                all.Count,
                next < all.Count ? next : (int?)null,
                warnings);

            return true;
        }

        private static List<T> Read<T>(
            IList<T> all, int offset, int asked, int valueChars, Func<IList<T>, int> measure)
        {
            List<T> taken = new List<T>();
            bool ending = offset + asked == all.Count;
            long running = 0;
            long room = valueChars;
            for (int at = 0; at < asked; at++)
            {
                T item = all[offset + at];
                taken.Add(item);
                int alone = measure(new[] { item });
                int step = measure(new[] { item, item }) - alone;
                if (at == 0)
                {
                    running = alone;
                    room = ending ? valueChars + (long)Math.Max(0, alone - step) : valueChars;
                }
                else
                {
                    running += step;
                }

                if (running > room)
                {
                    break;
                }
            }

            return taken;
        }

        private static int Fit<T>(List<T> taken, int asked, int valueChars, Func<IList<T>, int> measure)
        {
            if (taken.Count == 0 || (taken.Count == asked && measure(taken) <= valueChars))
            {
                return taken.Count;
            }

            int low = 0;
            int high = taken.Count == asked ? taken.Count : taken.Count + 1;
            while (high - low > 1)
            {
                int middle = low + ((high - low) / 2);
                if (measure(taken.Take(middle).ToArray()) <= valueChars)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
