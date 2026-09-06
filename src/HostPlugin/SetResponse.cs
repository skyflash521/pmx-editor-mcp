using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 集合を対象とする呼び出しが返す値を組み立てる。項目の名前と形をここに集めるのは、どのツールでも
    /// 同じ名前・同じ形で返すためである。
    /// </summary>
    public static class SetResponse
    {
        /// <summary>更新した件数の項目の名前。</summary>
        public const string UpdatedName = "updated";

        /// <summary>取り除いた件数の項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>加えた件数の項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>加えた後の位置の並びの項目の名前。</summary>
        public const string IndicesName = "indices";

        /// <summary>呼び出した件数の項目の名前。</summary>
        public const string InvokedName = "invoked";

        /// <summary>適用した件数の項目の名前。</summary>
        public const string AppliedName = "applied";

        /// <summary>更新の応答。適用した対象の件数だけを返し、位置やハンドルの並びは返さない。</summary>
        public static IDictionary<string, object> Updated(int count)
        {
            return Counted(UpdatedName, count);
        }

        /// <summary>取り除く応答。取り除いた要素の件数を返す。</summary>
        public static IDictionary<string, object> Removed(int count)
        {
            return Counted(RemovedName, count);
        }

        /// <summary>
        /// 加える応答。加えた件数と、加えた後の位置の並び(加えた順)を返す。加えるとハンドルが
        /// 消えるので、位置を返さないと加えた要素をこの先どう指せばよいかが決まらない。
        /// </summary>
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
