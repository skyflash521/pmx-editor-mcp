using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    internal static class SurfaceGeometry
    {
        /// <summary>
        /// 材質の面が頂点どうしを繋ぐ辺の表。頂点の通し番号で引き、隣り合う頂点の通し番号を昇順に返す。
        /// どの面にも載らない頂点の行は空になる。
        /// </summary>
        public static IList<int>[] Neighbours(IPXPmx model, IEnumerable<int> materials)
        {
            Dictionary<IPXVertex, int> places = new Dictionary<IPXVertex, int>(ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                places[model.Vertex[at]] = at;
            }

            SortedSet<int>[] around = new SortedSet<int>[model.Vertex.Count];
            for (int at = 0; at < around.Length; at++)
            {
                around[at] = new SortedSet<int>();
            }

            foreach (int material in materials.Distinct())
            {
                foreach (IPXFace face in model.Material[material].Faces)
                {
                    int[] corners = ViewSelection.Corners(face)
                        .Where(corner => corner != null && places.ContainsKey(corner))
                        .Select(corner => places[corner])
                        .ToArray();
                    foreach (int corner in corners)
                    {
                        foreach (int other in corners.Where(other => other != corner))
                        {
                            around[corner].Add(other);
                        }
                    }
                }
            }

            return around.Select(held => (IList<int>)held.ToList()).ToArray();
        }

        /// <summary>
        /// <paramref name="indices"/> の頂点を、<paramref name="threshold"/> 以内に近い頂点どうしで繋いでできる
        /// 組に分ける。2つ以上の頂点を持つ組だけを、組の中を昇順に、先頭の通し番号の昇順で返す。
        /// </summary>
        public static IList<int[]> Coincident(IList<Vec> positions, IEnumerable<int> indices, double threshold)
        {
            int[] picked = indices.Distinct().OrderBy(at => at).ToArray();
            double cell = Math.Max(threshold, 1e-9);
            Dictionary<CellKey, List<int>> cells = new Dictionary<CellKey, List<int>>();
            foreach (int at in picked)
            {
                CellKey key = CellKey.Of(positions[at], cell);
                List<int> held;
                if (!cells.TryGetValue(key, out held))
                {
                    held = new List<int>();
                    cells[key] = held;
                }

                held.Add(at);
            }

            Dictionary<int, int> parent = picked.ToDictionary(at => at, at => at);
            foreach (int at in picked)
            {
                CellKey home = CellKey.Of(positions[at], cell);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            List<int> held;
                            if (!cells.TryGetValue(new CellKey(home.X + dx, home.Y + dy, home.Z + dz), out held))
                            {
                                continue;
                            }

                            foreach (int other in held.Where(other => other > at))
                            {
                                Vec gap = positions[at] - positions[other];
                                if (gap.Length <= threshold)
                                {
                                    Join(parent, at, other);
                                }
                            }
                        }
                    }
                }
            }

            return picked
                .GroupBy(at => Root(parent, at))
                .Where(group => group.Count() > 1)
                .Select(group => group.ToArray())
                .OrderBy(group => group[0])
                .ToList();
        }

        /// <summary>
        /// 点ごとに、<paramref name="radius"/> 以内にある自分以外の点の番号を昇順に返す。有限でない点は
        /// どの点とも組にならない。
        /// </summary>
        public static IList<int>[] Within(IList<Vec> points, double radius)
        {
            List<int>[] found = new List<int>[points.Count];
            for (int at = 0; at < found.Length; at++)
            {
                found[at] = new List<int>();
            }

            ForEachNear(points, radius, (at, other) => found[at].Add(other));
            foreach (List<int> near in found)
            {
                near.Sort();
            }

            return found;
        }

        /// <summary>
        /// <paramref name="radius"/> 以内にある自分以外の点の組を、向きごとに <paramref name="visit"/> へ渡す。
        /// 並びは決まらない。有限でない点は組にならない。
        /// </summary>
        public static void ForEachNear(IList<Vec> points, double radius, Action<int, int> visit)
        {
            double cell = Math.Max(radius, 1e-9);
            Dictionary<CellKey, List<int>> cells = Grid(points, cell);
            for (int at = 0; at < points.Count; at++)
            {
                if (!IsFinite(points[at]))
                {
                    continue;
                }

                foreach (int other in InCells(cells, points[at], cell))
                {
                    if (other != at && (points[at] - points[other]).Length <= radius)
                    {
                        visit(at, other);
                    }
                }
            }
        }

        /// <summary>
        /// 問い合わせの点ごとに、<paramref name="radius"/> 以内にある <paramref name="points"/> の番号を
        /// 昇順に返す。有限でない点は、探す側にも探される側にも組にならない。
        /// </summary>
        public static IList<int>[] Near(IList<Vec> points, IList<Vec> queries, double radius)
        {
            double cell = Math.Max(radius, 1e-9);
            Dictionary<CellKey, List<int>> cells = Grid(points, cell);
            IList<int>[] found = new IList<int>[queries.Count];
            for (int at = 0; at < found.Length; at++)
            {
                found[at] = IsFinite(queries[at])
                    ? InCells(cells, queries[at], cell)
                        .Where(other => (points[other] - queries[at]).Length <= radius)
                        .OrderBy(other => other)
                        .ToList()
                    : new int[0];
            }

            return found;
        }

        private static Dictionary<CellKey, List<int>> Grid(IList<Vec> points, double cell)
        {
            Dictionary<CellKey, List<int>> cells = new Dictionary<CellKey, List<int>>();
            for (int at = 0; at < points.Count; at++)
            {
                if (!IsFinite(points[at]))
                {
                    continue;
                }

                CellKey key = CellKey.Of(points[at], cell);
                List<int> held;
                if (!cells.TryGetValue(key, out held))
                {
                    held = new List<int>();
                    cells[key] = held;
                }

                held.Add(at);
            }

            return cells;
        }

        private static IEnumerable<int> InCells(Dictionary<CellKey, List<int>> cells, Vec at, double cell)
        {
            CellKey home = CellKey.Of(at, cell);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        List<int> held;
                        if (!cells.TryGetValue(new CellKey(home.X + dx, home.Y + dy, home.Z + dz), out held))
                        {
                            continue;
                        }

                        foreach (int other in held)
                        {
                            yield return other;
                        }
                    }
                }
            }
        }

        private static bool IsFinite(Vec at)
        {
            return !double.IsNaN(at.X + at.Y + at.Z) && !double.IsInfinity(at.X + at.Y + at.Z);
        }

        private static int Root(Dictionary<int, int> parent, int at)
        {
            while (parent[at] != at)
            {
                parent[at] = parent[parent[at]];
                at = parent[at];
            }

            return at;
        }

        private static void Join(Dictionary<int, int> parent, int first, int second)
        {
            int one = Root(parent, first);
            int other = Root(parent, second);
            if (one != other)
            {
                parent[Math.Max(one, other)] = Math.Min(one, other);
            }
        }

        private struct CellKey : IEquatable<CellKey>
        {
            public readonly long X;

            public readonly long Y;

            public readonly long Z;

            public CellKey(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static CellKey Of(Vec at, double cell)
            {
                return new CellKey(
                    (long)Math.Floor(at.X / cell), (long)Math.Floor(at.Y / cell), (long)Math.Floor(at.Z / cell));
            }

            public bool Equals(CellKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object other)
            {
                return other is CellKey && Equals((CellKey)other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397) ^ Z.GetHashCode();
                }
            }
        }

        internal sealed class Hit
        {
            public Hit(Vec point, double signed, Vec front)
                : this(point, signed, front, null, null)
            {
            }

            public Hit(Vec point, double signed, Vec front, IPXVertex[] corners, double[] barycentric)
            {
                Point = point;
                Signed = signed;
                Front = front;
                Corners = corners;
                Barycentric = barycentric;
            }

            public Vec Point { get; }

            public double Signed { get; }

            public Vec Front { get; }

            public IPXVertex[] Corners { get; }

            public double[] Barycentric { get; }
        }

        internal struct Vec
        {
            public readonly double X;

            public readonly double Y;

            public readonly double Z;

            public Vec(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Vec Of(V3 given)
            {
                return new Vec(given.X, given.Y, given.Z);
            }

            public static Vec operator +(Vec left, Vec right)
            {
                return new Vec(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
            }

            public static Vec operator -(Vec left, Vec right)
            {
                return new Vec(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
            }

            public static Vec operator *(Vec given, double by)
            {
                return new Vec(given.X * by, given.Y * by, given.Z * by);
            }

            public double Length
            {
                get { return Math.Sqrt(Dot(this)); }
            }

            public double Dot(Vec other)
            {
                return (X * other.X) + (Y * other.Y) + (Z * other.Z);
            }

            public Vec Cross(Vec other)
            {
                return new Vec(
                    (Y * other.Z) - (Z * other.Y),
                    (Z * other.X) - (X * other.Z),
                    (X * other.Y) - (Y * other.X));
            }

            public double Axis(int axis)
            {
                return axis == 0 ? X : axis == 1 ? Y : Z;
            }

            public V3 ToV3()
            {
                return new V3((float)X, (float)Y, (float)Z);
            }

            public object[] Components()
            {
                return new object[] { (float)X, (float)Y, (float)Z };
            }
        }

        private sealed class Triangle
        {
            private readonly Vec[] _corners;

            private readonly Vec[] _normals;

            public Triangle(Vec[] corners, Vec[] normals, IPXVertex[] vertices)
            {
                _corners = corners;
                _normals = normals;
                Vertices = vertices;
                Low = new Vec(
                    corners.Min(c => c.X), corners.Min(c => c.Y), corners.Min(c => c.Z));
                High = new Vec(
                    corners.Max(c => c.X), corners.Max(c => c.Y), corners.Max(c => c.Z));
                Centre = (corners[0] + corners[1] + corners[2]) * (1.0 / 3.0);
            }

            public Vec Low { get; }

            public Vec High { get; }

            public Vec Centre { get; }

            public IPXVertex[] Vertices { get; }

            public bool Hits(Vec origin, Vec direction, out double along, out double[] barycentric)
            {
                along = 0;
                barycentric = null;
                Vec first = _corners[1] - _corners[0];
                Vec second = _corners[2] - _corners[0];
                Vec across = direction.Cross(second);
                double det = first.Dot(across);
                if (Math.Abs(det) < 1e-18)
                {
                    return false;
                }

                double inverse = 1.0 / det;
                Vec from = origin - _corners[0];
                double u = from.Dot(across) * inverse;
                if (u < 0 || u > 1)
                {
                    return false;
                }

                Vec up = from.Cross(first);
                double v = direction.Dot(up) * inverse;
                if (v < 0 || u + v > 1)
                {
                    return false;
                }

                along = second.Dot(up) * inverse;
                if (!(along >= -1e-9))
                {
                    return false;
                }

                along = Math.Max(along, 0);
                barycentric = new[] { 1 - u - v, u, v };

                return true;
            }

            /// <summary>始点 <paramref name="origin"/> から向き <paramref name="direction"/> へ伸ばした半直線が面を貫くか。</summary>
            public bool Crosses(Vec origin, Vec direction)
            {
                Vec first = _corners[1] - _corners[0];
                Vec second = _corners[2] - _corners[0];
                Vec across = direction.Cross(second);
                double det = first.Dot(across);
                if (Math.Abs(det) < 1e-18)
                {
                    return false;
                }

                double inverse = 1.0 / det;
                Vec from = origin - _corners[0];
                double u = from.Dot(across) * inverse;
                if (u < 0 || u > 1)
                {
                    return false;
                }

                Vec up = from.Cross(first);
                double v = direction.Dot(up) * inverse;
                if (v < 0 || u + v > 1)
                {
                    return false;
                }

                return second.Dot(up) * inverse > 1e-9;
            }

            /// <summary>面の上で <paramref name="point"/> に最も近い点と、そこでの表の向きを返す。</summary>
            public Vec Closest(Vec point, out Vec front, out double[] barycentric)
            {
                double u;
                double v;
                double w;
                Barycentric(point, out u, out v, out w);
                barycentric = new[] { u, v, w };
                front = (_normals[0] * u) + (_normals[1] * v) + (_normals[2] * w);
                if (front.Dot(front) == 0)
                {
                    front = (_corners[1] - _corners[0]).Cross(_corners[2] - _corners[0]);
                }

                return (_corners[0] * u) + (_corners[1] * v) + (_corners[2] * w);
            }

            private void Barycentric(Vec p, out double u, out double v, out double w)
            {
                // Ericson「Real-Time Collision Detection」5.1.5 の領域判定。
                Vec a = _corners[0];
                Vec b = _corners[1];
                Vec c = _corners[2];
                Vec ab = b - a;
                Vec ac = c - a;
                Vec ap = p - a;
                double d1 = ab.Dot(ap);
                double d2 = ac.Dot(ap);
                if (d1 <= 0 && d2 <= 0)
                {
                    Set(1, 0, 0, out u, out v, out w);

                    return;
                }

                Vec bp = p - b;
                double d3 = ab.Dot(bp);
                double d4 = ac.Dot(bp);
                if (d3 >= 0 && d4 <= d3)
                {
                    Set(0, 1, 0, out u, out v, out w);

                    return;
                }

                double vc = (d1 * d4) - (d3 * d2);
                if (vc <= 0 && d1 >= 0 && d3 <= 0 && d1 - d3 > 0)
                {
                    double t = d1 / (d1 - d3);
                    Set(1 - t, t, 0, out u, out v, out w);

                    return;
                }

                Vec cp = p - c;
                double d5 = ab.Dot(cp);
                double d6 = ac.Dot(cp);
                if (d6 >= 0 && d5 <= d6)
                {
                    Set(0, 0, 1, out u, out v, out w);

                    return;
                }

                double vb = (d5 * d2) - (d1 * d6);
                if (vb <= 0 && d2 >= 0 && d6 <= 0 && d2 - d6 > 0)
                {
                    double t = d2 / (d2 - d6);
                    Set(1 - t, 0, t, out u, out v, out w);

                    return;
                }

                double va = (d3 * d6) - (d5 * d4);
                if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0 && (d4 - d3) + (d5 - d6) > 0)
                {
                    double t = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                    Set(0, 1 - t, t, out u, out v, out w);

                    return;
                }

                double sum = va + vb + vc;
                if (sum > 0)
                {
                    Set(va / sum, vb / sum, vc / sum, out u, out v, out w);

                    return;
                }

                Edge(p, 0, 1, out u, out v, out w);
                double best = Distance(p, u, v, w);
                double eu;
                double ev;
                double ew;
                Edge(p, 1, 2, out eu, out ev, out ew);
                if (Distance(p, eu, ev, ew) < best)
                {
                    best = Distance(p, eu, ev, ew);
                    Set(eu, ev, ew, out u, out v, out w);
                }

                Edge(p, 0, 2, out eu, out ev, out ew);
                if (Distance(p, eu, ev, ew) < best)
                {
                    Set(eu, ev, ew, out u, out v, out w);
                }
            }

            private void Edge(Vec p, int from, int to, out double u, out double v, out double w)
            {
                Vec along = _corners[to] - _corners[from];
                double length = along.Dot(along);
                double t = length > 0 ? Math.Max(0, Math.Min(1, (p - _corners[from]).Dot(along) / length)) : 0;
                double[] weights = new double[3];
                weights[from] += 1 - t;
                weights[to] += t;
                Set(weights[0], weights[1], weights[2], out u, out v, out w);
            }

            private double Distance(Vec p, double u, double v, double w)
            {
                Vec gap = p - ((_corners[0] * u) + (_corners[1] * v) + (_corners[2] * w));

                return gap.Dot(gap);
            }

            private static void Set(double a, double b, double c, out double u, out double v, out double w)
            {
                u = a;
                v = b;
                w = c;
            }
        }

        internal sealed class SurfaceTree
        {
            private const int LeafSize = 4;

            private const double BoxMargin = 1e-9;

            private static readonly Vec[] RayDirections =
            {
                new Vec(0.8123, 0.3917, 0.4311),
                new Vec(-0.3321, 0.7719, 0.5412),
                new Vec(0.2417, -0.5231, -0.8173),
            };

            private readonly Vec _low;

            private readonly Vec _high;

            private readonly SurfaceTree _left;

            private readonly SurfaceTree _right;

            private readonly List<Triangle> _leaf;

            private SurfaceTree(List<Triangle> triangles)
            {
                _low = new Vec(
                    triangles.Min(t => t.Low.X), triangles.Min(t => t.Low.Y), triangles.Min(t => t.Low.Z));
                _high = new Vec(
                    triangles.Max(t => t.High.X), triangles.Max(t => t.High.Y), triangles.Max(t => t.High.Z));
                if (triangles.Count <= LeafSize)
                {
                    _leaf = triangles;

                    return;
                }

                Vec size = _high - _low;
                int axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
                List<Triangle> sorted = triangles.OrderBy(t => t.Centre.Axis(axis)).ToList();
                int half = sorted.Count / 2;
                _left = new SurfaceTree(sorted.GetRange(0, half));
                _right = new SurfaceTree(sorted.GetRange(half, sorted.Count - half));
            }

            /// <summary><paramref name="materials"/> に面積のある面が1つも無ければ null を返す。</summary>
            public static SurfaceTree Of(IPXPmx model, IEnumerable<int> materials)
            {
                List<Triangle> triangles = new List<Triangle>();
                foreach (int material in materials.Distinct())
                {
                    foreach (IPXFace face in model.Material[material].Faces)
                    {
                        IPXVertex[] corners = ViewSelection.Corners(face);
                        if (corners.Length != 3 || corners.Any(c => c == null))
                        {
                            continue;
                        }

                        Vec[] spots = corners.Select(c => Vec.Of(c.Position)).ToArray();
                        Vec span = (spots[1] - spots[0]).Cross(spots[2] - spots[0]);
                        if (span.Dot(span) == 0)
                        {
                            continue;
                        }

                        triangles.Add(new Triangle(spots, corners.Select(c => Vec.Of(c.Normal)).ToArray(), corners));
                    }
                }

                return triangles.Count == 0 ? null : new SurfaceTree(triangles);
            }

            /// <summary>面が <paramref name="reach"/> より遠ければ null を返す。</summary>
            public Hit Nearest(Vec point, double reach)
            {
                double best = reach * reach;
                Vec closest = default(Vec);
                Vec front = default(Vec);
                Triangle on = null;
                double[] weights = null;
                Search(point, ref best, ref closest, ref front, ref on, ref weights);
                if (on == null)
                {
                    return null;
                }

                double distance = Math.Sqrt(best);
                double span = front.Length;

                return new Hit(
                    closest,
                    (point - closest).Dot(front) < 0 ? -distance : distance,
                    span > 0 ? front * (1.0 / span) : front,
                    on.Vertices,
                    weights);
            }

            /// <summary>
            /// 始点が面の上にあるときはその面も貫かれたものとして、半直線が最初に貫く面を返す。貫く面が無ければ null を返す。
            /// 返す <see cref="Hit.Signed"/> は始点から貫く点までの長さである。
            /// </summary>
            public Hit Cast(Vec origin, Vec direction)
            {
                double best = double.PositiveInfinity;
                Triangle found = null;
                double[] weights = null;
                Cross(origin, direction, ref best, ref found, ref weights);
                if (found == null)
                {
                    return null;
                }

                Vec on = origin + (direction * best);
                Vec front;
                double[] unused;
                found.Closest(on, out front, out unused);
                double span = front.Length;

                return new Hit(on, best, span > 0 ? front * (1.0 / span) : front, found.Vertices, weights);
            }

            private void Cross(
                Vec origin, Vec direction, ref double best, ref Triangle found, ref double[] weights)
            {
                if (!RayHitsBox(origin, direction))
                {
                    return;
                }

                if (_leaf != null)
                {
                    foreach (Triangle triangle in _leaf)
                    {
                        double along;
                        double[] barycentric;
                        if (triangle.Hits(origin, direction, out along, out barycentric) && along < best)
                        {
                            best = along;
                            found = triangle;
                            weights = barycentric;
                        }
                    }

                    return;
                }

                _left.Cross(origin, direction, ref best, ref found, ref weights);
                _right.Cross(origin, direction, ref best, ref found, ref weights);
            }

            /// <summary>3方向の半直線が面を貫く回数の偶奇のうち、奇数が多い向きの数で内側かを決める。</summary>
            public bool IsInside(Vec point)
            {
                int odd = 0;
                foreach (Vec direction in RayDirections)
                {
                    if ((Crossings(point, direction) & 1) == 1)
                    {
                        odd++;
                    }
                }

                return odd * 2 > RayDirections.Length;
            }

            private int Crossings(Vec origin, Vec direction)
            {
                if (!RayHitsBox(origin, direction))
                {
                    return 0;
                }

                if (_leaf != null)
                {
                    return _leaf.Count(triangle => triangle.Crosses(origin, direction));
                }

                return _left.Crossings(origin, direction) + _right.Crossings(origin, direction);
            }

            private bool RayHitsBox(Vec origin, Vec direction)
            {
                double near = double.NegativeInfinity;
                double far = double.PositiveInfinity;
                for (int axis = 0; axis < 3; axis++)
                {
                    double start = origin.Axis(axis);
                    double along = direction.Axis(axis);
                    double low = _low.Axis(axis) - BoxMargin;
                    double high = _high.Axis(axis) + BoxMargin;
                    if (along == 0)
                    {
                        if (start < low || start > high)
                        {
                            return false;
                        }

                        continue;
                    }

                    double first = (low - start) / along;
                    double second = (high - start) / along;
                    near = Math.Max(near, Math.Min(first, second));
                    far = Math.Min(far, Math.Max(first, second));
                }

                return near <= far && far >= 0;
            }

            private void Search(
                Vec point, ref double best, ref Vec closest, ref Vec front, ref Triangle found, ref double[] weights)
            {
                if (BoxDistance(point) > best)
                {
                    return;
                }

                if (_leaf != null)
                {
                    foreach (Triangle triangle in _leaf)
                    {
                        Vec facing;
                        double[] barycentric;
                        Vec on = triangle.Closest(point, out facing, out barycentric);
                        Vec gap = point - on;
                        double squared = gap.Dot(gap);
                        if (squared < best || (found == null && squared <= best))
                        {
                            best = squared;
                            closest = on;
                            front = facing;
                            found = triangle;
                            weights = barycentric;
                        }
                    }

                    return;
                }

                SurfaceTree first = _left;
                SurfaceTree second = _right;
                if (_right.BoxDistance(point) < _left.BoxDistance(point))
                {
                    first = _right;
                    second = _left;
                }

                first.Search(point, ref best, ref closest, ref front, ref found, ref weights);
                second.Search(point, ref best, ref closest, ref front, ref found, ref weights);
            }

            private double BoxDistance(Vec point)
            {
                double squared = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    double at = point.Axis(axis);
                    double gap = Math.Max(Math.Max(_low.Axis(axis) - at, 0), at - _high.Axis(axis));
                    squared += gap * gap;
                }

                return squared;
            }
        }
    }
}
