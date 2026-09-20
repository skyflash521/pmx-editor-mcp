// 指した頂点を、位置の近さでまとまりへ分ける。

using System;
using System.Collections.Generic;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class VertexClusters
    {
        /// <summary>
        /// しきい値の中にある頂点どうしのまとまり。どの頂点もちょうど1つのまとまりに入り、
        /// まとまりの先頭は並びの中で先に来たものになる。
        /// </summary>
        public static IEnumerable<IList<IPXVertex>> Near(IList<IPXVertex> picked, float threshold)
        {
            if (picked == null)
            {
                throw new ArgumentNullException(nameof(picked));
            }

            List<IList<IPXVertex>> groups = new List<IList<IPXVertex>>();
            HashSet<IPXVertex> taken =
                new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                if (!taken.Add(vertex))
                {
                    continue;
                }

                List<IPXVertex> group = new List<IPXVertex> { vertex };
                foreach (IPXVertex other in picked)
                {
                    if (taken.Contains(other)
                        || Vectors.Distance(vertex.Position, other.Position) > threshold)
                    {
                        continue;
                    }

                    taken.Add(other);
                    group.Add(other);
                }

                groups.Add(group);
            }

            return groups;
        }
    }
}
