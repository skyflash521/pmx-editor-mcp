// 指した頂点を、位置の近さでまとまりへ分ける。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

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

            bool finite = !float.IsNaN(threshold) && !float.IsInfinity(threshold)
                && picked.All(vertex => Vectors.Finite(vertex.Position));
            if (!finite)
            {
                return Scanned(picked, threshold);
            }

            return threshold > 0f ? Gridded(picked, threshold) : Same(picked, threshold == 0f);
        }

        /// <summary>
        /// しきい値が0以下のときのまとまり。0なら同じ位置の頂点どうしが、負ならどの頂点も
        /// 自分だけでまとまる。
        /// </summary>
        private static IList<IList<IPXVertex>> Same(IList<IPXVertex> picked, bool joining)
        {
            List<IList<IPXVertex>> groups = new List<IList<IPXVertex>>();
            Dictionary<Spot, List<IPXVertex>> spots = new Dictionary<Spot, List<IPXVertex>>();
            HashSet<IPXVertex> taken =
                new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                if (!taken.Add(vertex))
                {
                    continue;
                }

                List<IPXVertex> group;
                if (joining && spots.TryGetValue(new Spot(vertex.Position), out group))
                {
                    group.Add(vertex);

                    continue;
                }

                group = new List<IPXVertex> { vertex };
                groups.Add(group);
                if (joining)
                {
                    spots.Add(new Spot(vertex.Position), group);
                }
            }

            return groups;
        }

        /// <summary>
        /// しきい値より少し広い升目へ振り分け、隣り合う升目の中だけを比べる。隔たりを単精度へ
        /// 丸めてしきい値に収まる組は、隣り合う升目までに入る。
        /// </summary>
        private static IList<IList<IPXVertex>> Gridded(IList<IPXVertex> picked, float threshold)
        {
            double size = threshold > 0f ? threshold * 1.001d : 1d;
            Dictionary<Cell, List<int>> cells = new Dictionary<Cell, List<int>>();
            Cell[] placed = new Cell[picked.Count];
            for (int at = 0; at < picked.Count; at++)
            {
                placed[at] = Cell.Of(picked[at].Position, size);
                List<int> held;
                if (!cells.TryGetValue(placed[at], out held))
                {
                    held = new List<int>();
                    cells.Add(placed[at], held);
                }

                held.Add(at);
            }

            List<IList<IPXVertex>> groups = new List<IList<IPXVertex>>();
            HashSet<IPXVertex> taken =
                new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < picked.Count; at++)
            {
                IPXVertex vertex = picked[at];
                if (!taken.Add(vertex))
                {
                    continue;
                }

                List<int> near = new List<int>();
                foreach (Cell cell in placed[at].Around())
                {
                    List<int> held;
                    if (!cells.TryGetValue(cell, out held))
                    {
                        continue;
                    }

                    foreach (int other in held)
                    {
                        if (!taken.Contains(picked[other])
                            && Vectors.Distance(vertex.Position, picked[other].Position) <= threshold)
                        {
                            near.Add(other);
                        }
                    }
                }

                near.Sort();
                List<IPXVertex> group = new List<IPXVertex> { vertex };
                foreach (int other in near)
                {
                    if (taken.Add(picked[other]))
                    {
                        group.Add(picked[other]);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>すべての組を比べる。有限でない位置やしきい値が混じるときに使う。</summary>
        private static IList<IList<IPXVertex>> Scanned(IList<IPXVertex> picked, float threshold)
        {
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

        /// <summary>位置そのものを鍵にしたもの。正と負の0は同じ位置として扱う。</summary>
        private struct Spot : IEquatable<Spot>
        {
            private readonly float _x;

            private readonly float _y;

            private readonly float _z;

            public Spot(V3 position)
            {
                _x = position.X + 0f;
                _y = position.Y + 0f;
                _z = position.Z + 0f;
            }

            public bool Equals(Spot other)
            {
                return _x == other._x && _y == other._y && _z == other._z;
            }

            public override bool Equals(object other)
            {
                return other is Spot && Equals((Spot)other);
            }

            public override int GetHashCode()
            {
                return unchecked((_x.GetHashCode() * 397 ^ _y.GetHashCode()) * 397 ^ _z.GetHashCode());
            }
        }

        /// <summary>升目の番地。升目の数が long に収まらない位置は端の升目へ寄せる。</summary>
        private struct Cell : IEquatable<Cell>
        {
            private readonly long _x;

            private readonly long _y;

            private readonly long _z;

            private Cell(long x, long y, long z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public static Cell Of(V3 position, double size)
            {
                return new Cell(
                    Index(position.X, size), Index(position.Y, size), Index(position.Z, size));
            }

            /// <summary>自分と、各軸に1つずつずれた升目の27個。</summary>
            public IEnumerable<Cell> Around()
            {
                for (long x = -1; x <= 1; x++)
                {
                    for (long y = -1; y <= 1; y++)
                    {
                        for (long z = -1; z <= 1; z++)
                        {
                            yield return new Cell(Step(_x, x), Step(_y, y), Step(_z, z));
                        }
                    }
                }
            }

            public bool Equals(Cell other)
            {
                return _x == other._x && _y == other._y && _z == other._z;
            }

            public override bool Equals(object other)
            {
                return other is Cell && Equals((Cell)other);
            }

            public override int GetHashCode()
            {
                return unchecked((_x.GetHashCode() * 397 ^ _y.GetHashCode()) * 397 ^ _z.GetHashCode());
            }

            private static long Index(float coordinate, double size)
            {
                double at = Math.Floor(coordinate / size);
                if (at >= long.MaxValue - 1)
                {
                    return long.MaxValue - 1;
                }

                return at <= long.MinValue + 1 ? long.MinValue + 1 : (long)at;
            }

            private static long Step(long at, long by)
            {
                return at + by;
            }
        }
    }
}
