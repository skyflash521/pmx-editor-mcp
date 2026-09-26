using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelFindSurfaceDistances
    {
        public const string ToolName = "model_find_surface_distances";

        public const string SurfaceMaterialIndicesName = "surfaceMaterialIndices";

        public const string DistanceLimitName = "distanceLimit";

        public const string CountName = "count";

        public const string FrontCountName = "frontCount";

        public const string BackCountName = "backCount";

        public const string FarCountName = "farCount";

        public const string MinDistanceName = "minDistance";

        public const string MinVertexName = "minVertex";

        public const string MinPointName = "minPoint";

        public const string MaxDistanceName = "maxDistance";

        public const string MaxVertexName = "maxVertex";

        public const string MaxPointName = "maxPoint";

        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            List<string> known = new List<string>(ModelFindVertexBounds.VertexPointing)
            {
                ModelFindVertexBounds.MaterialIndicesName,
                ModelFindVertexBounds.BoxesName,
                SurfaceMaterialIndicesName,
                DistanceLimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IEnumerable<int> chosen;
            string code;
            string message;
            if (!ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<int> surface;
            if (!TrySurface(context, model, out surface, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float? limit;
            List<float[]> boxes;
            if (!TryLimit(context, out limit, out message)
                || !ModelFindVertexBounds.TryBoxes(context, out boxes, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            SurfaceTree tree = SurfaceTree.Of(model, surface);
            if (tree == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, SurfaceMaterialIndicesName + " の材質に面積のある面が1つも無い。");
            }

            double reach = limit.HasValue ? limit.Value : double.PositiveInfinity;
            List<Measure> measures = new List<Measure>();
            foreach (int at in new SortedSet<int>(chosen))
            {
                V3 position = model.Vertex[at].Position;
                measures.Add(new Measure(at, position, tree.Nearest(Vec.Of(position), reach)));
            }

            Dictionary<string, object> value = Summary(measures, limit.HasValue);
            if (boxes != null)
            {
                value.Add(
                    ModelFindVertexBounds.BoxesName,
                    boxes.Select(box => (object)Summary(
                        measures.Where(m => ModelFindVertexBounds.Inside(box, m.Position)).ToList(),
                        limit.HasValue)).ToArray());
            }

            return ComposedEditResult.Complete(value);
        }

        private static Dictionary<string, object> Summary(List<Measure> measures, bool limited)
        {
            List<Measure> near = measures.Where(m => m.Hit != null).ToList();
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, near.Count },
                { FrontCountName, near.Count(m => m.Hit.Signed >= 0) },
                { BackCountName, near.Count(m => m.Hit.Signed < 0) },
            };
            if (limited)
            {
                value.Add(FarCountName, measures.Count - near.Count);
            }

            if (near.Count == 0)
            {
                return value;
            }

            Measure low = near[0];
            Measure high = near[0];
            foreach (Measure measure in near)
            {
                if (measure.Hit.Signed < low.Hit.Signed)
                {
                    low = measure;
                }

                if (measure.Hit.Signed > high.Hit.Signed)
                {
                    high = measure;
                }
            }

            value.Add(MinDistanceName, (float)low.Hit.Signed);
            value.Add(MinVertexName, low.Index);
            value.Add(MinPointName, low.Hit.Point.Components());
            value.Add(MaxDistanceName, (float)high.Hit.Signed);
            value.Add(MaxVertexName, high.Index);
            value.Add(MaxPointName, high.Hit.Point.Components());

            return value;
        }

        private static bool TrySurface(
            McpMethodContext context,
            IPXPmx model,
            out List<int> surface,
            out string code,
            out string message)
        {
            surface = new List<int>();
            code = ToolEnvelope.InvalidArgument;
            string shape = SurfaceMaterialIndicesName + " は材質の位置を1つ以上並べた並びでなければならない。";
            object given;
            object[] items;
            if (!context.Params.TryGetValue(SurfaceMaterialIndicesName, out given)
                || (items = given as object[]) == null
                || items.Length == 0)
            {
                message = shape;

                return false;
            }

            foreach (object item in items)
            {
                int at;
                if (!ValueInput.TryIndex(item, out at))
                {
                    message = shape;

                    return false;
                }

                if (at < 0 || at >= model.Material.Count)
                {
                    code = ToolEnvelope.IndexOutOfRange;
                    message = SurfaceMaterialIndicesName + " が並びの外を指している: " + at;

                    return false;
                }

                surface.Add(at);
            }

            code = null;
            message = null;

            return true;
        }

        private static bool TryLimit(McpMethodContext context, out float? limit, out string message)
        {
            limit = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(DistanceLimitName, out given))
            {
                return true;
            }

            float read;
            if (!ValueInput.TrySingle(given, out read) || !(read >= 0) || float.IsInfinity(read))
            {
                message = DistanceLimitName + " は0以上の有限の数でなければならない。";

                return false;
            }

            limit = read;

            return true;
        }

        private sealed class Measure
        {
            public Measure(int index, V3 position, Hit hit)
            {
                Index = index;
                Position = position;
                Hit = hit;
            }

            public int Index { get; }

            public V3 Position { get; }

            /// <summary>面が <c>distanceLimit</c> より遠いときは null。</summary>
            public Hit Hit { get; }
        }

        private sealed class Hit
        {
            public Hit(Vec point, double signed)
            {
                Point = point;
                Signed = signed;
            }

            public Vec Point { get; }

            public double Signed { get; }
        }

        private struct Vec
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

            public object[] Components()
            {
                return new object[] { (float)X, (float)Y, (float)Z };
            }
        }

        private sealed class Triangle
        {
            private readonly Vec[] _corners;

            private readonly Vec[] _normals;

            public Triangle(Vec[] corners, Vec[] normals)
            {
                _corners = corners;
                _normals = normals;
                Low = new Vec(
                    corners.Min(c => c.X), corners.Min(c => c.Y), corners.Min(c => c.Z));
                High = new Vec(
                    corners.Max(c => c.X), corners.Max(c => c.Y), corners.Max(c => c.Z));
                Centre = (corners[0] + corners[1] + corners[2]) * (1.0 / 3.0);
            }

            public Vec Low { get; }

            public Vec High { get; }

            public Vec Centre { get; }

            /// <summary>面の上で <paramref name="point"/> に最も近い点と、そこでの表の向きを返す。</summary>
            public Vec Closest(Vec point, out Vec front)
            {
                double u;
                double v;
                double w;
                Barycentric(point, out u, out v, out w);
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

        private sealed class SurfaceTree
        {
            private const int LeafSize = 4;

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

                        triangles.Add(new Triangle(spots, corners.Select(c => Vec.Of(c.Normal)).ToArray()));
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
                bool found = false;
                Search(point, ref best, ref closest, ref front, ref found);
                if (!found)
                {
                    return null;
                }

                double distance = Math.Sqrt(best);

                return new Hit(closest, (point - closest).Dot(front) < 0 ? -distance : distance);
            }

            private void Search(Vec point, ref double best, ref Vec closest, ref Vec front, ref bool found)
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
                        Vec on = triangle.Closest(point, out facing);
                        Vec gap = point - on;
                        double squared = gap.Dot(gap);
                        if (squared < best || (!found && squared <= best))
                        {
                            best = squared;
                            closest = on;
                            front = facing;
                            found = true;
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

                first.Search(point, ref best, ref closest, ref front, ref found);
                second.Search(point, ref best, ref closest, ref front, ref found);
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
