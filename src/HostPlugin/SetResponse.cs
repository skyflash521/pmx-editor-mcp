using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    public static class SetResponse
    {
        public const string UpdatedName = "updated";

        public const string RemovedName = "removed";

        public const string AddedName = "added";

        public const string IndicesName = "indices";

        public const string InvokedName = "invoked";

        public const string AppliedName = "applied";

        /// <summary>更新の応答。適用した対象の件数だけを返し、位置やハンドルの並びは返さない。</summary>
        public static IDictionary<string, object> Updated(int count)
        {
            return Counted(UpdatedName, count);
        }

        public static IDictionary<string, object> Removed(int count)
        {
            return Counted(RemovedName, count);
        }

        /// <summary>加える応答。加えた件数と、加えた後の位置の並び(加えた順)を返す。</summary>
        public static IDictionary<string, object> Added(IList<int> indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { AddedName, indices.Count },
                { IndicesName, indices.ToArray() },
            };
        }

        /// <summary>戻り値も出力の引数も持たないメソッドの応答。呼び出した要素の件数を返す。</summary>
        public static IDictionary<string, object> Invoked(int count)
        {
            return Counted(InvokedName, count);
        }

        /// <summary>モーションイベントのテンプレートの応答。適用した対象の件数を返す。</summary>
        public static IDictionary<string, object> Applied(int count)
        {
            return Counted(AppliedName, count);
        }

        /// <summary>
        /// 戻り値か出力に現れる引数を持つメソッドの応答。対象集合と同じ長さの並びを、解決した対象の
        /// 順で返す。対象が0件なら空の並びになる。
        /// </summary>
        public static IList<object> PerTarget(IList<object> values, int targetCount)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.Count != targetCount)
            {
                throw new ArgumentException(
                    "応答の並びの長さが対象の件数と違う。", nameof(values));
            }

            return values.ToArray();
        }

        private static IDictionary<string, object> Counted(string name, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "0以上でなければならない。");
            }

            return new Dictionary<string, object>(StringComparer.Ordinal) { { name, count } };
        }
    }
}
