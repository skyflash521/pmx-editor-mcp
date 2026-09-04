using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要素のどの項目を返すかを決める。頼み方は共通契約仕様書の返す項目が定める。
    /// </summary>
    public static class FieldSelection
    {
        /// <summary>
        /// 頼まれた項目名を、返す項目の並びへ直す。頼まれていなければ読み取り可能な項目すべてと
        /// する。常に返す項目 <paramref name="always"/> は選び方の外に置き、先頭へ並べる。
        /// 頼み方が正しくなければ偽を返し、断る内容を渡す。
        /// </summary>
        public static bool TryResolve(
            IList<string> requested,
            IList<string> readable,
            IList<string> always,
            out IList<string> selected,
            out string code,
            out string message)
        {
            if (readable == null)
            {
                throw new ArgumentNullException(nameof(readable));
            }

            if (always == null)
            {
                throw new ArgumentNullException(nameof(always));
            }

            HashSet<string> known = new HashSet<string>(readable, StringComparer.Ordinal);
            foreach (string name in always)
            {
                if (known.Contains(name))
                {
                    throw new ArgumentException(
                        "常に返す項目 " + name + " が読み取り可能な項目にも在る。", nameof(always));
                }
            }

            selected = null;
            code = null;
            message = null;
            if (requested == null)
            {
                selected = Ordered(always, readable);

                return true;
            }

            if (requested.Count == 0)
            {
                return Invalid("fields が項目を1つも選んでいない。", out code, out message);
            }

            HashSet<string> asked = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in requested)
            {
                if (!asked.Add(name))
                {
                    return Invalid("fields が項目 " + name + " を二度選んでいる。", out code, out message);
                }
            }

            foreach (string name in requested)
            {
                if (!known.Contains(name))
                {
                    return Invalid("fields が選んだ " + name + " は読み取れる項目に無い。", out code, out message);
                }
            }

            List<string> chosen = new List<string>(asked.Count);
            foreach (string name in readable)
            {
                if (asked.Contains(name))
                {
                    chosen.Add(name);
                }
            }

            selected = Ordered(always, chosen);

            return true;
        }

        private static IList<string> Ordered(IList<string> always, IList<string> chosen)
        {
            List<string> ordered = new List<string>(always.Count + chosen.Count);
            ordered.AddRange(always);
            ordered.AddRange(chosen);

            return ordered;
        }

        /// <summary>
        /// 要素の組から、選んだ項目だけを選んだ並びで取り出す。<paramref name="selected"/> は
        /// <see cref="TryResolve"/> が返す並び、すなわち常に返す項目と選ばれた項目の連なりとする。
        /// 組がそのどれかを持たなければ <see cref="ArgumentException"/> で止める——組の側の
        /// 取りこぼしは、呼び出し側の組み立ての誤りである。
        /// </summary>
        public static IDictionary<string, object> Take(
            IDictionary<string, object> item, IList<string> selected)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (selected == null)
            {
                throw new ArgumentNullException(nameof(selected));
            }

            Dictionary<string, object> taken = new Dictionary<string, object>(
                selected.Count, StringComparer.Ordinal);
            foreach (string name in selected)
            {
                object value;
                if (!item.TryGetValue(name, out value))
                {
                    throw new ArgumentException("要素が項目 " + name + " を持たない。", nameof(item));
                }

                taken[name] = value;
            }

            return taken;
        }

        private static bool Invalid(string reason, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = reason;

            return false;
        }
    }
}
