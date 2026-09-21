using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 所有するリストを読み書きする中継。リストの型は行キーごとにビルド時に決まっているので、
    /// ここには名前で型やメンバーを引く経路が無い。複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class SdkList
    {
        private readonly Func<object, int> _count;

        private readonly Func<object, int, object> _at;

        private readonly Action<object, object> _add;

        private readonly Action<object, int> _removeAt;

        /// <summary>
        /// 件数・位置で引く・末尾へ加える・位置で取り除くの4つを与えて生成する。どれも受け手を
        /// 第1引数に取る。
        /// </summary>
        public SdkList(
            Func<object, int> count,
            Func<object, int, object> at,
            Action<object, object> add,
            Action<object, int> removeAt)
        {
            if (count == null)
            {
                throw new ArgumentNullException(nameof(count));
            }

            if (at == null)
            {
                throw new ArgumentNullException(nameof(at));
            }

            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            if (removeAt == null)
            {
                throw new ArgumentNullException(nameof(removeAt));
            }

            _count = count;
            _at = at;
            _add = add;
            _removeAt = removeAt;
        }

        /// <summary>並んでいる件数。</summary>
        public int Count(object owner)
        {
            return _count(owner);
        }

        /// <summary>その位置の要素。</summary>
        public object At(object owner, int index)
        {
            return _at(owner, index);
        }

        /// <summary>末尾へ加える。</summary>
        public void Add(object owner, object item)
        {
            _add(owner, item);
        }

        /// <summary>その位置の要素を取り除く。</summary>
        public void RemoveAt(object owner, int index)
        {
            _removeAt(owner, index);
        }

        /// <summary>
        /// 先頭へ置いた実体の分を戻した、リストそのものの中での位置。先頭へ置いた実体の位置を
        /// 渡すと <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static int Behind(int index, int ahead)
        {
            if (index < ahead)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "先頭の" + ahead + "件はモデルがリストとは別に持つ実体で、取り除けない。");
            }

            return index - ahead;
        }
    }
}
