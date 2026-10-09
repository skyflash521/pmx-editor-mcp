using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindSurfaceSection
    {
        public const string ToolName = "model_find_surface_section";

        public const string PlanePointName = "planePoint";

        public const string PlaneNormalName = "planeNormal";

        public const string FromName = "from";

        public const string ToName = "to";

        public const string ViaName = "via";

        public const string SpacingName = "spacing";

        public const string LinesName = "lines";

        public const string ClosedName = "closed";

        public const string LengthName = "length";

        public const string PointsName = "points";

        public const string LineLengthName = "lineLength";

        public const string FromGapName = "fromGap";

        public const string ToGapName = "toGap";

        private const int LeastPointChars = 8;

        private const int SingleShift = 149;

        private static readonly double ProductUnit = BitConverter.Int64BitsToDouble((long)(1023 - (2 * SingleShift)) << 52);

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

            List<string> known = new List<string>
            {
                ModelFindVertexBounds.MaterialIndicesName,
                PlanePointName,
                PlaneNormalName,
                FromName,
                ToName,
                ViaName,
                SpacingName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            List<int> materials;
            V3 point;
            V3 normal;
            string code;
            string message;
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context, model, ModelFindVertexBounds.MaterialIndicesName, out materials, out code, out message)
                || !ComposedInput.TrySpot(context, PlanePointName, out point, out code, out message)
                || !ComposedInput.TryDirection(context, PlaneNormalName, out normal, out code, out message)
                || !ComposedInput.TrySpot(context, PlaneNormalName, out normal, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            V3 from;
            V3 to;
            V3 via;
            double? spacing;
            if (!TryEnds(context, out from, out to, out via, out code, out message)
                || !TrySpacing(context, out spacing, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<Line> lines = Section(model, materials, point, normal);
            if (lines.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    PlanePointName + " と " + PlaneNormalName + " の平面が "
                    + ModelFindVertexBounds.MaterialIndicesName + " の材質の面と交わらない。");
            }

            int most = ResponseSize.ValueChars(context.BudgetChars) / LeastPointChars;
            Dictionary<string, object> value;
            if (from == null)
            {
                List<object> listed = new List<object>();
                foreach (Line line in lines.OrderByDescending(l => l.Length))
                {
                    List<Vec> points = Resampled(line.Corners, line.Closed, spacing, most);
                    if (points == null)
                    {
                        return TooLarge();
                    }

                    most -= points.Count;

                    listed.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { ClosedName, line.Closed },
                        { LengthName, (float)line.Length },
                        { PointsName, Listed(points) },
                    });
                }

                value = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { LinesName, listed.ToArray() },
                };
            }
            else if (!TryPath(lines, from, to, via, spacing, most, out value, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!ResponseSize.Fits(value, context.BudgetChars))
            {
                return TooLarge();
            }

            return ComposedEditResult.Complete(value);
        }

        private static ComposedEditResult TooLarge()
        {
            return ComposedEditResult.Refuse(
                ToolEnvelope.ResponseTooLarge,
                "線の点が値の枠に収まらない。" + SpacingName + " を大きくするか、" + FromName + " と "
                + ToName + " で区間を絞る。");
        }

        private static bool TryPath(
            List<Line> lines,
            V3 from,
            V3 to,
            V3 via,
            double? spacing,
            int most,
            out Dictionary<string, object> value,
            out string code,
            out string message)
        {
            value = null;
            code = ToolEnvelope.NotApplicable;
            Line line = Nearest(lines, Vec.Of(from));
            if (Nearest(lines, Vec.Of(to)) != line)
            {
                message = FromName + " と " + ToName + " に最も近い点が別々の線に乗る。";

                return false;
            }

            Snap start = line.Snapped(Vec.Of(from));
            Snap end = line.Snapped(Vec.Of(to));
            List<Vec> path;
            if (!line.Closed)
            {
                path = start.Arc <= end.Arc
                    ? line.Between(start.Arc, end.Arc)
                    : Reversed(line.Between(end.Arc, start.Arc));
            }
            else
            {
                double forward = Wrapped(end.Arc - start.Arc, line.Length);
                bool ahead = via == null
                    ? forward <= line.Length - forward
                    : Wrapped(line.Snapped(Vec.Of(via)).Arc - start.Arc, line.Length) <= forward;
                path = ahead
                    ? line.Between(start.Arc, start.Arc + forward)
                    : Reversed(line.Between(end.Arc, end.Arc + (line.Length - forward)));
            }

            List<Vec> points = Resampled(path, false, spacing, most);
            if (points == null)
            {
                code = ToolEnvelope.ResponseTooLarge;
                message = "線の点が値の枠に収まらない。" + SpacingName + " を大きくする。";

                return false;
            }

            value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { LengthName, (float)Length(path) },
                { LineLengthName, (float)line.Length },
                { ClosedName, line.Closed },
                { FromGapName, (float)start.Gap },
                { ToGapName, (float)end.Gap },
                { PointsName, Listed(points) },
            };
            code = null;
            message = null;

            return true;
        }

        private static Line Nearest(List<Line> lines, Vec point)
        {
            Line best = lines[0];
            double gap = best.Snapped(point).Gap;
            foreach (Line line in lines.Skip(1))
            {
                double other = line.Snapped(point).Gap;
                if (other < gap)
                {
                    best = line;
                    gap = other;
                }
            }

            return best;
        }

        private static double Wrapped(double arc, double length)
        {
            double wrapped = arc % length;

            return wrapped < 0 ? wrapped + length : wrapped;
        }

        private static List<Vec> Reversed(List<Vec> path)
        {
            path.Reverse();

            return path;
        }

        private static double Length(List<Vec> path)
        {
            double length = 0;
            for (int at = 1; at < path.Count; at++)
            {
                length += (path[at] - path[at - 1]).Length;
            }

            return length;
        }

        /// <summary>
        /// 閉じた線は始点へ戻る点を含めて渡し、戻った点は返さない。点が <paramref name="most"/> を超えるなら
        /// null を返す。
        /// </summary>
        private static List<Vec> Resampled(List<Vec> corners, bool closed, double? spacing, int most)
        {
            List<Vec> placed = new List<Vec>();
            if (!spacing.HasValue)
            {
                if (corners.Count > most)
                {
                    return null;
                }

                placed.AddRange(corners);
            }
            else
            {
                double total = Length(corners);
                if (total / spacing.Value > most)
                {
                    return null;
                }

                placed.Add(corners[0]);
                int step = 1;
                double walked = 0;
                for (int at = 1; at < corners.Count && step * spacing.Value < total; at++)
                {
                    Vec along = corners[at] - corners[at - 1];
                    double span = along.Length;
                    double next;
                    while ((next = step * spacing.Value) < total && next <= walked + span)
                    {
                        placed.Add(corners[at - 1] + (along * ((next - walked) / span)));
                        step++;
                    }

                    walked += span;
                }

                placed.Add(corners[corners.Count - 1]);
            }

            if (closed)
            {
                placed.RemoveAt(placed.Count - 1);
            }

            return placed;
        }

        private static object[] Listed(List<Vec> points)
        {
            return points.Select(p => (object)p.Components()).ToArray();
        }

        private static List<Line> Section(IPXPmx model, List<int> materials, V3 origin, V3 normal)
        {
            Dictionary<Spot, int> welded = new Dictionary<Spot, int>();
            Graph graph = new Graph();
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
                    double[] sides = corners.Select(c => Side(c.Position, origin, normal)).ToArray();
                    if (span.Dot(span) == 0 || sides.All(side => side == 0))
                    {
                        continue;
                    }

                    int[] ids = corners.Select(c => Weld(welded, c.Position)).ToArray();
                    List<long> cut = new List<long>();
                    for (int at = 0; at < 3; at++)
                    {
                        int next = (at + 1) % 3;
                        if (sides[at] == 0 && sides[next] == 0)
                        {
                            graph.Link(
                                graph.Node(Graph.OnVertex(ids[at]), spots[at]),
                                graph.Node(Graph.OnVertex(ids[next]), spots[next]));

                            continue;
                        }

                        if ((sides[at] >= 0) == (sides[next] >= 0))
                        {
                            continue;
                        }

                        long node;
                        if (sides[at] == 0 || sides[next] == 0)
                        {
                            int on = sides[at] == 0 ? at : next;
                            node = graph.Node(Graph.OnVertex(ids[on]), spots[on]);
                        }
                        else
                        {
                            int low = ids[at] < ids[next] ? at : next;
                            int high = low == at ? next : at;
                            double share = sides[low] / (sides[low] - sides[high]);
                            node = graph.Node(
                                Graph.OnEdge(ids[low], ids[high]),
                                spots[low] + ((spots[high] - spots[low]) * share));
                        }

                        if (!cut.Contains(node))
                        {
                            cut.Add(node);
                        }
                    }

                    if (cut.Count == 2)
                    {
                        graph.Link(cut[0], cut[1]);
                    }
                }
            }

            return graph.Lines();
        }

        /// <summary>
        /// 平面から <paramref name="spot"/> までの符号付きの距離に法線の長さを掛けた値。0を返すのは、
        /// <paramref name="spot"/> が平面の上にあるときだけである。
        /// </summary>
        private static double Side(V3 spot, V3 origin, V3 normal)
        {
            BigInteger exact = ((Scaled(spot.X) - Scaled(origin.X)) * Scaled(normal.X))
                + ((Scaled(spot.Y) - Scaled(origin.Y)) * Scaled(normal.Y))
                + ((Scaled(spot.Z) - Scaled(origin.Z)) * Scaled(normal.Z));
            if (exact.IsZero)
            {
                return 0;
            }

            double side = (double)exact * ProductUnit;

            return side != 0 ? side : exact.Sign * double.Epsilon;
        }

        private static BigInteger Scaled(float value)
        {
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            int exponent = (bits >> 23) & 0xFF;
            BigInteger scaled = bits & 0x7FFFFF;
            if (exponent != 0)
            {
                scaled = (scaled | 0x800000) << (exponent - 1);
            }

            return bits < 0 ? -scaled : scaled;
        }

        private static int Weld(Dictionary<Spot, int> welded, V3 position)
        {
            Spot key = new Spot(position.X, position.Y, position.Z);
            int id;
            if (!welded.TryGetValue(key, out id))
            {
                id = welded.Count;
                welded.Add(key, id);
            }

            return id;
        }

        private static bool TryEnds(
            McpMethodContext context, out V3 from, out V3 to, out V3 via, out string code, out string message)
        {
            from = null;
            to = null;
            via = null;
            code = ToolEnvelope.InvalidArgument;
            bool hasFrom = context.Params.ContainsKey(FromName);
            if (hasFrom != context.Params.ContainsKey(ToName))
            {
                message = FromName + " と " + ToName + " は揃えて渡さなければならない。";

                return false;
            }

            bool hasVia = context.Params.ContainsKey(ViaName);
            if (hasVia && !hasFrom)
            {
                message = ViaName + " は " + FromName + " と " + ToName + " を渡したときだけ渡せる。";

                return false;
            }

            message = null;
            if (!hasFrom)
            {
                code = null;

                return true;
            }

            return ComposedInput.TrySpot(context, FromName, out from, out code, out message)
                && ComposedInput.TrySpot(context, ToName, out to, out code, out message)
                && (!hasVia || ComposedInput.TrySpot(context, ViaName, out via, out code, out message));
        }

        private static bool TrySpacing(
            McpMethodContext context, out double? spacing, out string code, out string message)
        {
            spacing = null;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(SpacingName, out given))
            {
                return true;
            }

            float read;
            if (!ValueInput.TrySingle(given, out read) || !(read > 0))
            {
                code = ToolEnvelope.InvalidArgument;
                message = SpacingName + " は0より大きい有限の数でなければならない。";

                return false;
            }

            spacing = read;

            return true;
        }

        private sealed class Graph
        {
            private readonly Dictionary<long, Vec> _points = new Dictionary<long, Vec>();

            private readonly Dictionary<long, List<int>> _touching = new Dictionary<long, List<int>>();

            private readonly List<long> _nodes = new List<long>();

            private readonly List<long[]> _segments = new List<long[]>();

            private readonly HashSet<Tuple<long, long>> _seen = new HashSet<Tuple<long, long>>();

            public static long OnVertex(int id)
            {
                return ((long)id << 32) | uint.MaxValue;
            }

            /// <summary><paramref name="low"/> は <paramref name="high"/> より小さく渡す。</summary>
            public static long OnEdge(int low, int high)
            {
                return ((long)low << 32) | (uint)high;
            }

            public long Node(long key, Vec point)
            {
                if (!_points.ContainsKey(key))
                {
                    _points.Add(key, point);
                    _touching.Add(key, new List<int>());
                    _nodes.Add(key);
                }

                return key;
            }

            public void Link(long from, long to)
            {
                if (from == to || !_seen.Add(Tuple.Create(Math.Min(from, to), Math.Max(from, to))))
                {
                    return;
                }

                _touching[from].Add(_segments.Count);
                _touching[to].Add(_segments.Count);
                _segments.Add(new[] { from, to });
            }

            public List<Line> Lines()
            {
                bool[] used = new bool[_segments.Count];
                List<Line> lines = new List<Line>();
                foreach (long start in _nodes.Where(n => _touching[n].Count % 2 == 1).Concat(_nodes))
                {
                    while (_touching[start].Any(s => !used[s]))
                    {
                        List<long> walked = new List<long> { start };
                        long at = start;
                        int found;
                        while ((found = _touching[at].FindIndex(s => !used[s])) >= 0)
                        {
                            int step = _touching[at][found];
                            used[step] = true;
                            at = _segments[step][0] == at ? _segments[step][1] : _segments[step][0];
                            walked.Add(at);
                        }

                        Line line = Line.Of(walked.Select(n => _points[n]).ToList(), at == start);
                        if (line != null)
                        {
                            lines.Add(line);
                        }
                    }
                }

                return lines;
            }
        }

        private struct Spot : IEquatable<Spot>
        {
            private readonly float _x;

            private readonly float _y;

            private readonly float _z;

            public Spot(float x, float y, float z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public bool Equals(Spot other)
            {
                return _x.Equals(other._x) && _y.Equals(other._y) && _z.Equals(other._z);
            }

            public override bool Equals(object obj)
            {
                return obj is Spot && Equals((Spot)obj);
            }

            public override int GetHashCode()
            {
                return (((_x.GetHashCode() * 397) ^ _y.GetHashCode()) * 397) ^ _z.GetHashCode();
            }
        }

        private struct Snap
        {
            public Snap(double arc, double gap)
            {
                Arc = arc;
                Gap = gap;
            }

            public double Arc { get; }

            public double Gap { get; }
        }

        private sealed class Line
        {
            private readonly double[] _arcs;

            private Line(List<Vec> corners, bool closed)
            {
                Corners = corners;
                Closed = closed;
                _arcs = new double[corners.Count];
                for (int at = 1; at < corners.Count; at++)
                {
                    _arcs[at] = _arcs[at - 1] + (corners[at] - corners[at - 1]).Length;
                }
            }

            /// <summary>閉じた線は最後に始点をもう一度持つ。</summary>
            public List<Vec> Corners { get; }

            public bool Closed { get; }

            public double Length
            {
                get { return _arcs[_arcs.Length - 1]; }
            }

            /// <summary>位置の違う角が2つ未満なら null を返す。</summary>
            public static Line Of(List<Vec> walked, bool closed)
            {
                List<Vec> corners = new List<Vec>();
                foreach (Vec corner in walked)
                {
                    if (corners.Count == 0 || !Same(corners[corners.Count - 1], corner))
                    {
                        corners.Add(corner);
                    }
                }

                if (closed && corners.Count > 1 && !Same(corners[0], corners[corners.Count - 1]))
                {
                    corners.Add(corners[0]);
                }

                if (corners.Count < 2)
                {
                    return null;
                }

                return new Line(corners, closed && corners.Count > 3);
            }

            public Snap Snapped(Vec point)
            {
                double arc = 0;
                double best = double.PositiveInfinity;
                for (int at = 1; at < Corners.Count; at++)
                {
                    Vec along = Corners[at] - Corners[at - 1];
                    double span = along.Dot(along);
                    double share = span > 0
                        ? Math.Max(0, Math.Min(1, (point - Corners[at - 1]).Dot(along) / span))
                        : 0;
                    double gap = (point - (Corners[at - 1] + (along * share))).Length;
                    if (gap < best)
                    {
                        best = gap;
                        arc = _arcs[at - 1] + ((_arcs[at] - _arcs[at - 1]) * share);
                    }
                }

                return new Snap(arc, best);
            }

            /// <summary>閉じた線では <paramref name="to"/> が線の長さを超えて始点を回り込んでよい。</summary>
            public List<Vec> Between(double from, double to)
            {
                List<Vec> path = new List<Vec> { At(from) };
                int laps = Closed ? 2 : 1;
                for (int lap = 0; lap < laps; lap++)
                {
                    for (int at = lap == 0 ? 0 : 1; at < Corners.Count; at++)
                    {
                        double arc = _arcs[at] + (lap * Length);
                        if (arc > from && arc < to)
                        {
                            path.Add(Corners[at]);
                        }
                    }
                }

                path.Add(At(to));

                return path;
            }

            private Vec At(double arc)
            {
                if (Closed && arc > Length)
                {
                    arc -= Length;
                }

                for (int at = 1; at < Corners.Count; at++)
                {
                    if (arc <= _arcs[at] || at == Corners.Count - 1)
                    {
                        double span = _arcs[at] - _arcs[at - 1];
                        double share = span > 0 ? Math.Max(0, Math.Min(1, (arc - _arcs[at - 1]) / span)) : 0;

                        return Corners[at - 1] + ((Corners[at] - Corners[at - 1]) * share);
                    }
                }

                return Corners[0];
            }

            private static bool Same(Vec left, Vec right)
            {
                return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
            }
        }
    }
}
