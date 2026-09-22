using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>並びの位置を、連なった区間ごとに先頭と件数の組で表す。</summary>
    public static class PositionRuns
    {
        /// <summary>先頭 <paramref name="start"/> から <paramref name="count"/> 件の区間。</summary>
        public static IDictionary<string, object> Of(int start, int count)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TargetInput.StartName, start },
                { TargetInput.CountName, count },
            };
        }

        /// <summary>
        /// 位置を渡した順に見て、1つ前の次に当たる位置を同じ区間へまとめる。空なら区間も無い。
        /// </summary>
        public static IList<object> Joined(IEnumerable<int> positions)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            List<object> runs = new List<object>();
            int start = 0;
            int count = 0;
            foreach (int at in positions)
            {
                if (count > 0 && at == start + count)
                {
                    count++;
                    continue;
                }

                if (count > 0)
                {
                    runs.Add(Of(start, count));
                }

                start = at;
                count = 1;
            }

            if (count > 0)
            {
                runs.Add(Of(start, count));
            }

            return runs;
        }
    }
}
