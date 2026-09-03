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

        /// <summary>返すもの。</summary>
        public IList<T> Items { get; }

        /// <summary>切り出す前の総数。</summary>
        public int Total { get; }

        /// <summary>続きがあるときの、次に渡す位置。無ければ null。</summary>
        public int? NextOffset { get; }

        /// <summary>件数を減らしたときに添える警告。減らしていなければ空。</summary>
        public IList<string> Warnings { get; }
    }

    /// <summary>
    /// 一覧を位置と件数で切り出す。求められた件数のまま返すと枠に収まらないことがあるので、
    /// 収まる件数まで減らし、続きの位置を添えて返す。
    /// </summary>
    public static class Paging
    {
        /// <summary>
        /// <paramref name="offset"/> の位置から <paramref name="limit"/> 件までを切り出す。
        /// <paramref name="measure"/> は、渡した並びを載せた値の全体の大きさをそのまま量るもので、
        /// 件数が増えたときに大きさが減らないようにする——ただし切り出したものを全件そのまま載せる
        /// ときだけは、続きの位置が付かないぶん小さくなってよい。その値が
        /// <paramref name="valueChars"/> を超えない最も多い件数まで減らす。1件も返せないときは偽。
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
            T[] taken = all.Skip(offset).Take(asked).ToArray();
            int fitted = Fit(taken, valueChars, measure);
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

        /// <summary>枠に収まる最も多い件数。</summary>
        private static int Fit<T>(T[] taken, int valueChars, Func<IList<T>, int> measure)
        {
            if (taken.Length == 0 || measure(taken) <= valueChars)
            {
                return taken.Length;
            }

            int low = 0;
            int high = taken.Length;
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
