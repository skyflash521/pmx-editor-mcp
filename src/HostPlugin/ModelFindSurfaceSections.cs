using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindSurfaceSections
    {
        public const string ToolName = "model_find_surface_sections";

        public const string FromName = "from";

        public const string ToName = "to";

        public const string AcrossName = "across";

        public const string AtName = "at";

        public const string SetsName = "sets";

        public const string IndicesName = "indices";

        public const string SectionsName = "sections";

        public const string SetName = "set";

        public const string SName = "s";

        public const string LinesName = "lines";

        public const string ClosedName = "closed";

        public const string LengthName = "length";

        public const string PointsName = "points";

        private const int LeastPointChars = 8;

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

            List<string> known = new List<string> { FromName, ToName, AcrossName, AtName, SetsName };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            V3 from;
            V3 to;
            V3 across;
            AxisFrame frame;
            string code;
            string message;
            if (!ComposedInput.TrySpot(context, FromName, out from, out code, out message)
                || !ComposedInput.TrySpot(context, ToName, out to, out code, out message)
                || !ComposedInput.TrySpot(context, AcrossName, out across, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            V3 normal = new V3(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
            if (!AxisFrame.TryCreate(
                    Vec.Of(from), Vec.Of(from) + Vec.Of(normal), Vec.Of(across), out frame, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            List<double> at;
            List<Cut> cuts;
            if (!TryAt(context, out at, out code, out message)
                || !TrySets(context, model, out cuts, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int most = ResponseSize.ValueChars(context.BudgetChars) / LeastPointChars;
            List<object> sections = new List<object>();
            for (int set = 0; set < cuts.Count; set++)
            {
                foreach (KeyValuePair<double, V3> plane in Planes(model, frame, at, cuts[set]))
                {
                    double s = plane.Key;
                    V3 origin = plane.Value;
                    List<Dictionary<string, object>> lines = Listed(
                        frame, ModelFindSurfaceSection.Section(cuts[set].Triangles, origin, normal));
                    foreach (Dictionary<string, object> line in lines)
                    {
                        most -= ((object[])line[PointsName]).Length;
                    }

                    if (most < 0)
                    {
                        return TooLarge();
                    }

                    sections.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { SetName, set },
                        { SName, (float)s },
                        { LinesName, lines.Cast<object>().ToArray() },
                    });
                }
            }

            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { SectionsName, sections.ToArray() },
            };
            if (!ResponseSize.Fits(value, context.BudgetChars))
            {
                return TooLarge();
            }

            return ComposedEditResult.Complete(value);
        }

        private static IEnumerable<KeyValuePair<double, V3>> Planes(
            IPXPmx model, AxisFrame frame, List<double> at, Cut cut)
        {
            if (at != null)
            {
                return at.Select(s => new KeyValuePair<double, V3>(s, (frame.From + (frame.Axis * s)).ToV3()));
            }

            SortedDictionary<double, V3> through = new SortedDictionary<double, V3>();
            foreach (int vertex in cut.Vertices)
            {
                V3 spot = model.Vertex[vertex].Position;
                double s = frame.Along(Vec.Of(spot));
                if (!through.ContainsKey(s))
                {
                    through.Add(s, spot);
                }
            }

            return through;
        }

        private static ComposedEditResult TooLarge()
        {
            return ComposedEditResult.Refuse(
                ToolEnvelope.ResponseTooLarge,
                "切り口の点が値の枠に収まらない。" + AtName + " の位置か " + SetsName + " の集まりを減らす。");
        }

        private static List<Dictionary<string, object>> Listed(
            AxisFrame frame, List<ModelFindSurfaceSection.Line> lines)
        {
            return lines
                .Select(line => Placed(frame, line))
                .OrderByDescending(p => p.Line.Length)
                .ThenBy(p => p.Points[0].Item1)
                .ThenBy(p => p.Points[0].Item2)
                .Select(p => new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ClosedName, p.Line.Closed },
                    { LengthName, (float)p.Line.Length },
                    {
                        PointsName,
                        p.Points.Select(c => (object)new object[] { (float)c.Item1, (float)c.Item2 }).ToArray()
                    },
                })
                .ToList();
        }

        private static PlacedLine Placed(AxisFrame frame, ModelFindSurfaceSection.Line line)
        {
            List<Tuple<double, double>> points = line.Corners
                .Select(c => Tuple.Create((c - frame.From).Dot(frame.Across), (c - frame.From).Dot(frame.Up)))
                .ToList();
            if (line.Closed)
            {
                points.RemoveAt(points.Count - 1);
                double area = 0;
                for (int at = 0; at < points.Count; at++)
                {
                    Tuple<double, double> next = points[(at + 1) % points.Count];
                    area += (points[at].Item1 * next.Item2) - (next.Item1 * points[at].Item2);
                }

                if (area < 0)
                {
                    points.Reverse();
                }

                int first = 0;
                for (int at = 1; at < points.Count; at++)
                {
                    if (Before(points[at], points[first]))
                    {
                        first = at;
                    }
                }

                points = points.Skip(first).Concat(points.Take(first)).ToList();
            }
            else if (Before(points[points.Count - 1], points[0]))
            {
                points.Reverse();
            }

            return new PlacedLine(line, points);
        }

        private static bool Before(Tuple<double, double> left, Tuple<double, double> right)
        {
            return left.Item1 != right.Item1 ? left.Item1 < right.Item1 : left.Item2 < right.Item2;
        }

        private static bool TryAt(
            McpMethodContext context, out List<double> at, out string code, out string message)
        {
            at = null;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(AtName, out given))
            {
                return true;
            }

            object[] items = given as object[];
            List<double> read = new List<double>();
            if (items != null)
            {
                foreach (object item in items)
                {
                    float one;
                    if (!ValueInput.TrySingle(item, out one))
                    {
                        break;
                    }

                    read.Add(one);
                }
            }

            if (items == null || items.Length == 0 || read.Count != items.Length)
            {
                code = ToolEnvelope.InvalidArgument;
                message = AtName + " は有限の数を1つ以上並べた並びでなければならない。";

                return false;
            }

            at = read;

            return true;
        }

        private static bool TrySets(
            McpMethodContext context, IPXPmx model, out List<Cut> cuts, out string code, out string message)
        {
            cuts = null;
            code = ToolEnvelope.InvalidArgument;
            message = SetsName + " は、" + IndicesName + "(頂点の位置の並び)か "
                + ModelFindVertexBounds.MaterialIndicesName
                + "(材質の位置の並び)のどちらか1つを持つ集まりを、1つ以上並べた並びでなければならない。";
            object given;
            context.Params.TryGetValue(SetsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                return false;
            }

            Dictionary<IPXVertex, int> placed = null;
            List<Cut> made = new List<Cut>();
            foreach (object item in items)
            {
                IDictionary<string, object> held = item as IDictionary<string, object>;
                if (held == null
                    || held.Keys.Any(k => k != IndicesName && k != ModelFindVertexBounds.MaterialIndicesName)
                    || held.ContainsKey(IndicesName) == held.ContainsKey(ModelFindVertexBounds.MaterialIndicesName))
                {
                    return false;
                }

                List<int> positions;
                string detail;
                if (held.ContainsKey(ModelFindVertexBounds.MaterialIndicesName))
                {
                    string name = SetsName + "[]." + ModelFindVertexBounds.MaterialIndicesName;
                    if (!PositionInput.TryMany(
                            held[ModelFindVertexBounds.MaterialIndicesName],
                            name,
                            name + " は材質の位置を1つ以上並べた並びでなければならない。",
                            model.Material.Count,
                            false,
                            out positions,
                            out code,
                            out detail))
                    {
                        message = detail;

                        return false;
                    }

                    made.Add(new Cut(
                        ModelFindMaterialVertices.Used(model, positions).ToList(),
                        ModelFindSurfaceSection.Triangles(
                            positions.Distinct().SelectMany(m => model.Material[m].Faces)).ToList()));

                    continue;
                }

                string vertices = SetsName + "[]." + IndicesName;
                if (!PositionInput.TryMany(
                        held[IndicesName],
                        vertices,
                        vertices + " は頂点の位置を1つ以上並べた並びでなければならない。",
                        model.Vertex.Count,
                        false,
                        out positions,
                        out code,
                        out detail))
                {
                    message = detail;

                    return false;
                }

                if (placed == null)
                {
                    placed = new Dictionary<IPXVertex, int>(ReferenceComparer<IPXVertex>.Instance);
                    for (int at = 0; at < model.Vertex.Count; at++)
                    {
                        if (!placed.ContainsKey(model.Vertex[at]))
                        {
                            placed.Add(model.Vertex[at], at);
                        }
                    }
                }

                HashSet<int> inside = new HashSet<int>(positions);
                Dictionary<IPXVertex, int> lookup = placed;
                IEnumerable<IPXFace> faces = Enumerable.Range(0, model.Material.Count)
                    .SelectMany(m => model.Material[m].Faces)
                    .Where(f => ViewSelection.Corners(f)
                        .All(c => c != null && lookup.ContainsKey(c) && inside.Contains(lookup[c])));
                made.Add(new Cut(
                    inside.OrderBy(v => v).ToList(),
                    ModelFindSurfaceSection.Triangles(faces).ToList()));
            }

            cuts = made;
            code = null;
            message = null;

            return true;
        }

        private sealed class Cut
        {
            public Cut(List<int> vertices, List<V3[]> triangles)
            {
                Vertices = vertices;
                Triangles = triangles;
            }

            public List<int> Vertices { get; }

            public List<V3[]> Triangles { get; }
        }

        private sealed class PlacedLine
        {
            public PlacedLine(ModelFindSurfaceSection.Line line, List<Tuple<double, double>> points)
            {
                Line = line;
                Points = points;
            }

            public ModelFindSurfaceSection.Line Line { get; }

            public List<Tuple<double, double>> Points { get; }
        }
    }
}
