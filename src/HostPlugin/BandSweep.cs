using System;
using System.Collections.Generic;
using System.Linq;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal static class BandSweep
    {
        private const double Tiny = 1e-12;

        private const int Dimension = 3;

        /// <summary>
        /// 帯の頂点それぞれの、付け根の頂点から帯の面の辺をたどった最短距離。たどり着けない頂点は無限大。
        /// </summary>
        public static double[] Distances(
            IList<int> band, IList<int> roots, IList<int>[] around, IList<Vec> positions)
        {
            double[] reach = Enumerable.Repeat(double.PositiveInfinity, positions.Count).ToArray();
            bool[] inBand = new bool[positions.Count];
            foreach (int at in band)
            {
                inBand[at] = true;
            }

            SortedSet<Tuple<double, int>> queue = new SortedSet<Tuple<double, int>>();
            foreach (int root in roots)
            {
                reach[root] = 0d;
                queue.Add(Tuple.Create(0d, root));
            }

            while (queue.Count > 0)
            {
                Tuple<double, int> next = queue.Min;
                queue.Remove(next);
                foreach (int other in around[next.Item2].Where(held => inBand[held]))
                {
                    double through = next.Item1 + (positions[next.Item2] - positions[other]).Length;
                    if (through >= reach[other])
                    {
                        continue;
                    }

                    queue.Remove(Tuple.Create(reach[other], other));
                    reach[other] = through;
                    queue.Add(Tuple.Create(through, other));
                }
            }

            return reach;
        }

        /// <summary>
        /// 距離の小さい順に頂点を並べ、層の先頭の距離から <paramref name="tolerance"/> 以内の頂点を
        /// 同じ層にまとめる。
        /// </summary>
        public static IList<int[]> Layers(IList<int> band, double[] reach, double tolerance)
        {
            List<int[]> layers = new List<int[]>();
            List<int> held = new List<int>();
            double start = 0d;
            foreach (int at in band.OrderBy(each => reach[each]).ThenBy(each => each))
            {
                if (held.Count > 0 && reach[at] - start > tolerance)
                {
                    layers.Add(held.ToArray());
                    held.Clear();
                }

                if (held.Count == 0)
                {
                    start = reach[at];
                }

                held.Add(at);
            }

            if (held.Count > 0)
            {
                layers.Add(held.ToArray());
            }

            return layers;
        }

        public static IDictionary<int, Vec> Place(
            IList<int[]> layers,
            double[] reach,
            IList<Vec> positions,
            IList<KeyValuePair<double, Vec>> offsets,
            int fixedEnds)
        {
            int count = layers.Count;
            Vec[] centre = new Vec[count];
            Vec[] placed = new Vec[count];
            bool[] held = new bool[count];
            for (int at = 0; at < count; at++)
            {
                Vec sum = default(Vec);
                double along = 0d;
                foreach (int vertex in layers[at])
                {
                    sum = sum + positions[vertex];
                    along += reach[vertex];
                }

                centre[at] = sum * (1d / layers[at].Length);
                held[at] = at < fixedEnds || at >= count - fixedEnds;
                placed[at] = held[at]
                    ? centre[at]
                    : centre[at] + Moved(offsets, along / layers[at].Length);
            }

            Dictionary<int, Vec> result = new Dictionary<int, Vec>();
            double[,] turn = Identity();
            for (int at = 0; at < count; at++)
            {
                if (held[at])
                {
                    turn = Identity();
                    continue;
                }

                Vec? from = Tangent(centre, at);
                Vec? to = Tangent(placed, at);
                if (from.HasValue && to.HasValue)
                {
                    turn = Product(Between(Apply(turn, from.Value), to.Value), turn);
                }

                foreach (int vertex in layers[at])
                {
                    result[vertex] = placed[at] + Apply(turn, positions[vertex] - centre[at]);
                }
            }

            return result;
        }

        private static Vec Moved(IList<KeyValuePair<double, Vec>> offsets, double at)
        {
            if (at <= offsets[0].Key)
            {
                return offsets[0].Value;
            }

            for (int next = 1; next < offsets.Count; next++)
            {
                if (at <= offsets[next].Key)
                {
                    double share = (at - offsets[next - 1].Key) / (offsets[next].Key - offsets[next - 1].Key);

                    return offsets[next - 1].Value + ((offsets[next].Value - offsets[next - 1].Value) * share);
                }
            }

            return offsets[offsets.Count - 1].Value;
        }

        private static Vec? Tangent(Vec[] line, int at)
        {
            Vec gap = line[Math.Min(at + 1, line.Length - 1)] - line[Math.Max(at - 1, 0)];
            double length = gap.Length;

            return length > Tiny ? gap * (1d / length) : (Vec?)null;
        }

        private static double[,] Identity()
        {
            return new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        }

        private static Vec Apply(double[,] turn, Vec given)
        {
            return new Vec(
                (turn[0, 0] * given.X) + (turn[0, 1] * given.Y) + (turn[0, 2] * given.Z),
                (turn[1, 0] * given.X) + (turn[1, 1] * given.Y) + (turn[1, 2] * given.Z),
                (turn[2, 0] * given.X) + (turn[2, 1] * given.Y) + (turn[2, 2] * given.Z));
        }

        private static double[,] Product(double[,] left, double[,] right)
        {
            double[,] made = new double[Dimension, Dimension];
            for (int row = 0; row < Dimension; row++)
            {
                for (int column = 0; column < Dimension; column++)
                {
                    for (int inner = 0; inner < Dimension; inner++)
                    {
                        made[row, column] += left[row, inner] * right[inner, column];
                    }
                }
            }

            return made;
        }

        /// <summary>長さ1の向き <paramref name="from"/> を <paramref name="to"/> へ回す最小の回転。</summary>
        private static double[,] Between(Vec from, Vec to)
        {
            Vec axis = from.Cross(to);
            double sine = axis.Length;
            double cosine = from.Dot(to);
            if (sine <= Tiny)
            {
                return cosine > 0d ? Identity() : HalfTurn(from);
            }

            Vec k = axis * (1d / sine);
            double rest = 1d - cosine;

            return new[,]
            {
                { cosine + (k.X * k.X * rest), (k.X * k.Y * rest) - (k.Z * sine), (k.X * k.Z * rest) + (k.Y * sine) },
                { (k.Y * k.X * rest) + (k.Z * sine), cosine + (k.Y * k.Y * rest), (k.Y * k.Z * rest) - (k.X * sine) },
                { (k.Z * k.X * rest) - (k.Y * sine), (k.Z * k.Y * rest) + (k.X * sine), cosine + (k.Z * k.Z * rest) },
            };
        }

        private static double[,] HalfTurn(Vec from)
        {
            int smallest = Math.Abs(from.X) <= Math.Abs(from.Y) && Math.Abs(from.X) <= Math.Abs(from.Z)
                ? 0
                : Math.Abs(from.Y) <= Math.Abs(from.Z) ? 1 : 2;
            Vec across = from.Cross(new Vec(
                smallest == 0 ? 1d : 0d, smallest == 1 ? 1d : 0d, smallest == 2 ? 1d : 0d));
            Vec u = across * (1d / across.Length);

            return new[,]
            {
                { (2d * u.X * u.X) - 1d, 2d * u.X * u.Y, 2d * u.X * u.Z },
                { 2d * u.Y * u.X, (2d * u.Y * u.Y) - 1d, 2d * u.Y * u.Z },
                { 2d * u.Z * u.X, 2d * u.Z * u.Y, (2d * u.Z * u.Z) - 1d },
            };
        }
    }
}
