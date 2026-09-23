using System;
using System.Collections;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 書き込む相手がまだどのPMXにも属していないために、解かずに預かった位置。書き込みの筋を
    /// 通る値として渡し、受け取った側が相手と組にして預かる。
    /// </summary>
    public sealed class DeferredPosition
    {
        public DeferredPosition(ToolField field, int position)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            Field = field;
            Position = position;
        }

        /// <summary>書き込む先の項目。</summary>
        public ToolField Field { get; }

        /// <summary>書かれた位置。</summary>
        public int Position { get; }
    }

    /// <summary>
    /// ハンドルで持つ実体へ書かれた、位置で指す項目の値を預かったもの。位置はPMXの中のリストで
    /// 数えるので、まだどのPMXにも属していない実体へ書くときは解けない。値のまま預かり、その実体が
    /// PMXの中の並びへ加わる呼び出しが、自分が相手にするPMXの中で解いて書き込む。
    /// 加える先の親もハンドルで持つ実体なら、預かりはその親へ移る——先に加わるのは親のほうなので、
    /// 親が加わる時点でまとめて解ける。書き込む相手を自分で持つのはこのためである。
    /// </summary>
    public sealed class DeferredWrite
    {
        public DeferredWrite(object target, ToolField field, int position)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            Target = target;
            Field = field;
            Position = position;
        }

        /// <summary>書き込む相手。預かりが親へ移っても、書く先はこの実体のままである。</summary>
        public object Target { get; }

        /// <summary>書き込む先の項目。数える先のリストへの道もここが持つ。</summary>
        public ToolField Field { get; }

        /// <summary>書かれた位置。数える先のリストの中で0から数える。</summary>
        public int Position { get; }
    }

    /// <summary>
    /// 1つの実体が預かる書き込みの並び。同じ相手の同じ項目は1つだけ持ち、預けた順に並ぶ。
    /// </summary>
    public sealed class DeferredWrites : IEnumerable<DeferredWrite>
    {
        private readonly List<DeferredWrite> _held = new List<DeferredWrite>();

        private readonly Dictionary<object, Dictionary<string, int>> _at =
            new Dictionary<object, Dictionary<string, int>>(ReferenceComparer<object>.Instance);

        /// <summary>
        /// 預かる。同じ相手の同じ項目を二度書いたら、前の位置のまま後の値だけを残す——直に書く
        /// ときと同じ結末にする。
        /// </summary>
        public void Put(DeferredWrite pending)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            Dictionary<string, int> fields;
            if (!_at.TryGetValue(pending.Target, out fields))
            {
                fields = new Dictionary<string, int>(StringComparer.Ordinal);
                _at.Add(pending.Target, fields);
            }

            int at;
            if (fields.TryGetValue(pending.Field.RowKey, out at))
            {
                _held[at] = pending;

                return;
            }

            fields.Add(pending.Field.RowKey, _held.Count);
            _held.Add(pending);
        }

        public IEnumerator<DeferredWrite> GetEnumerator()
        {
            return _held.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
