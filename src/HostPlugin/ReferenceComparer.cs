// 同じオブジェクトかどうかだけで見る比べ方。PMXの要素は値で等しくなりうるので、並びの中の1つを
// 指す表はこれで引く。

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PmxEditorMcp
{
    public sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

        private ReferenceComparer()
        {
        }

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(T item)
        {
            return RuntimeHelpers.GetHashCode(item);
        }
    }
}
