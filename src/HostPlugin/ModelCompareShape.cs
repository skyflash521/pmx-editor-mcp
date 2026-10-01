using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelCompareShape
    {
        public const string ToolName = "model_compare_shape";

        public const string StretchThresholdsName = "stretchThresholds";

        public const string BendThresholdsName = "bendThresholds";

        public const string FaceCountName = "faceCount";

        public const string EdgeCountName = "edgeCount";

        public const string MinStretchName = "minStretch";

        public const string MaxStretchName = "maxStretch";

        public const string MaxStretchVerticesName = "maxStretchVertices";

        public const string MaxStretchPointsName = "maxStretchPoints";

        public const string StretchDistributionName = "stretchDistribution";

        public const string FacePairCountName = "facePairCount";

        public const string MaxBendName = "maxBend";

        public const string MaxBendFacesName = "maxBendFaces";

        public const string BendDistributionName = "bendDistribution";

        public const string FlippedCountName = "flippedCount";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string MaterialName = "material";

        public const string FaceName = "face";

        public const string PointsName = "points";

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
                ModelMorphFromMoved.BasePmxHandleName,
                StretchThresholdsName,
                BendThresholdsName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IPXPmx based;
            string code;
            string message;
            if (!ModelMorphFromMoved.TryBase(context, out based, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string differs = Differs(based, model);
            if (differs != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
            }

            IEnumerable<int> chosen;
            if (!ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<int> materials = null;
            if (context.Params.ContainsKey(ModelFindVertexBounds.MaterialIndicesName)
                && !ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindVertexBounds.MaterialIndicesName,
                    out materials,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<float> stretchThresholds;
            List<float> bendThresholds;
            if (!ModelFindSurfaceDistances.TryThresholds(
                    context, StretchThresholdsName, out stretchThresholds, out message)
                || !ModelFindSurfaceDistances.TryThresholds(
                    context, BendThresholdsName, out bendThresholds, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            Shape shape = new Shape(
                Facets(model, materials, new HashSet<int>(chosen)),
                Positions(model),
                Positions(based));

            return ComposedEditResult.Complete(
                shape.Summary(stretchThresholds, bendThresholds));
        }

        private static string Differs(IPXPmx based, IPXPmx model)
        {
            KeyValuePair<string, int[]>[] counts =
            {
                new KeyValuePair<string, int[]>(
                    "頂点", new[] { based.Vertex.Count, model.Vertex.Count }),
                new KeyValuePair<string, int[]>(
                    "材質", new[] { based.Material.Count, model.Material.Count }),
                new KeyValuePair<string, int[]>(
                    "ボーン", new[] { based.Bone.Count, model.Bone.Count }),
            };
            foreach (KeyValuePair<string, int[]> held in counts)
            {
                if (held.Value[0] != held.Value[1])
                {
                    return "複製と" + held.Key + "の数が違う: 複製 " + held.Value[0]
                        + " 件・いま " + held.Value[1] + " 件。";
                }
            }

            for (int at = 0; at < model.Material.Count; at++)
            {
                int basedFaces = based.Material[at].Faces.Count;
                int faces = model.Material[at].Faces.Count;
                if (basedFaces != faces)
                {
                    return "複製と面の数が違う: 材質 " + at + " は複製 " + basedFaces
                        + " 件・いま " + faces + " 件。";
                }
            }

            return null;
        }

        private static Vec[] Positions(IPXPmx model)
        {
            Vec[] made = new Vec[model.Vertex.Count];
            for (int at = 0; at < made.Length; at++)
            {
                made[at] = Vec.Of(model.Vertex[at].Position);
            }

            return made;
        }

        private static List<Facet> Facets(
            IPXPmx model, IList<int> materials, ISet<int> chosen)
        {
            IDictionary<IPXVertex, int> places = ModelCleanFaces.Places(model);
            List<Facet> made = new List<Facet>();
            IEnumerable<int> scope = materials == null
                ? Enumerable.Range(0, model.Material.Count)
                : materials.Distinct().OrderBy(m => m);
            foreach (int material in scope)
            {
                IList<IPXFace> faces = model.Material[material].Faces;
                for (int at = 0; at < faces.Count; at++)
                {
                    int[] corners = ViewSelection.Corners(faces[at])
                        .Select(c => c != null && places.ContainsKey(c) ? places[c] : -1)
                        .ToArray();
                    if (corners.Any(c => c < 0) || (materials == null && !corners.All(chosen.Contains)))
                    {
                        continue;
                    }

                    made.Add(new Facet(material, at, corners));
                }
            }

            return made;
        }

        private sealed class Facet
        {
            public Facet(int material, int face, int[] corners)
            {
                Material = material;
                Face = face;
                Corners = corners;
            }

            public int Material { get; }

            public int Face { get; }

            public int[] Corners { get; }
        }

        private sealed class Shape
        {
            private readonly List<Facet> _facets;

            private readonly Vec[] _now;

            private readonly Vec[] _was;

            private readonly Vec[] _nowNormals;

            private readonly Vec[] _wasNormals;

            private readonly SortedDictionary<long, List<int>> _edges =
                new SortedDictionary<long, List<int>>();

            public Shape(List<Facet> facets, Vec[] now, Vec[] was)
            {
                _facets = facets;
                _now = now;
                _was = was;
                _nowNormals = facets.Select(f => Normal(now, f)).ToArray();
                _wasNormals = facets.Select(f => Normal(was, f)).ToArray();
                for (int at = 0; at < facets.Count; at++)
                {
                    int[] corners = facets[at].Corners;
                    for (int side = 0; side < 3; side++)
                    {
                        long key = Key(corners[side], corners[(side + 1) % 3]);
                        List<int> held;
                        if (!_edges.TryGetValue(key, out held))
                        {
                            held = new List<int>();
                            _edges.Add(key, held);
                        }

                        held.Add(at);
                    }
                }
            }

            public Dictionary<string, object> Summary(
                List<float> stretchThresholds, List<float> bendThresholds)
            {
                Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { FaceCountName, _facets.Count },
                    { EdgeCountName, _edges.Count },
                };
                Stretch(value, stretchThresholds);
                Bend(value, bendThresholds);
                int flipped = 0;
                for (int at = 0; at < _facets.Count; at++)
                {
                    if (IsSolid(_nowNormals[at]) && IsSolid(_wasNormals[at])
                        && _nowNormals[at].Dot(_wasNormals[at]) < 0)
                    {
                        flipped++;
                    }
                }

                value.Add(FlippedCountName, flipped);

                return value;
            }

            private static long Key(int first, int second)
            {
                return ((long)Math.Min(first, second) << 32) | (uint)Math.Max(first, second);
            }

            private static Vec Normal(Vec[] spots, Facet facet)
            {
                return (spots[facet.Corners[1]] - spots[facet.Corners[0]])
                    .Cross(spots[facet.Corners[2]] - spots[facet.Corners[0]]);
            }

            private static bool IsSolid(Vec normal)
            {
                return normal.Dot(normal) > 0;
            }

            private static double Angle(Vec first, Vec second)
            {
                return Math.Atan2(first.Cross(second).Length, first.Dot(second)) * 180.0 / Math.PI;
            }

            private static object[] Distribution(List<float> thresholds, List<double> measured)
            {
                return thresholds.Select(t => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { LimitName, t },
                    { CountName, measured.Count(m => m >= t) },
                }).ToArray();
            }

            private void Stretch(Dictionary<string, object> value, List<float> thresholds)
            {
                List<double> ratios = new List<double>();
                double low = 0;
                double high = 0;
                long highKey = 0;
                foreach (KeyValuePair<long, List<int>> edge in _edges)
                {
                    int first = (int)(edge.Key >> 32);
                    int second = (int)(edge.Key & 0xFFFFFFFFL);
                    double before = (_was[first] - _was[second]).Length;
                    if (before == 0)
                    {
                        continue;
                    }

                    double ratio = (_now[first] - _now[second]).Length / before;
                    if (ratios.Count == 0 || ratio < low)
                    {
                        low = ratio;
                    }

                    if (ratios.Count == 0 || ratio > high)
                    {
                        high = ratio;
                        highKey = edge.Key;
                    }

                    ratios.Add(ratio);
                }

                if (thresholds != null)
                {
                    value.Add(StretchDistributionName, Distribution(thresholds, ratios));
                }

                if (ratios.Count == 0)
                {
                    return;
                }

                int from = (int)(highKey >> 32);
                int to = (int)(highKey & 0xFFFFFFFFL);
                value.Add(MinStretchName, (float)low);
                value.Add(MaxStretchName, (float)high);
                value.Add(MaxStretchVerticesName, new object[] { from, to });
                value.Add(MaxStretchPointsName, new object[] { _now[from].Components(), _now[to].Components() });
            }

            private void Bend(Dictionary<string, object> value, List<float> thresholds)
            {
                List<double> changes = new List<double>();
                double high = 0;
                int highFirst = -1;
                int highSecond = -1;
                foreach (List<int> faces in _edges.Values)
                {
                    for (int one = 0; one < faces.Count; one++)
                    {
                        for (int other = one + 1; other < faces.Count; other++)
                        {
                            int first = faces[one];
                            int second = faces[other];
                            if (!IsSolid(_nowNormals[first]) || !IsSolid(_nowNormals[second])
                                || !IsSolid(_wasNormals[first]) || !IsSolid(_wasNormals[second]))
                            {
                                continue;
                            }

                            double change = Math.Abs(
                                Angle(_nowNormals[first], _nowNormals[second])
                                - Angle(_wasNormals[first], _wasNormals[second]));
                            if (changes.Count == 0 || change > high
                                || (change == high
                                    && (first < highFirst
                                        || (first == highFirst && second < highSecond))))
                            {
                                high = change;
                                highFirst = first;
                                highSecond = second;
                            }

                            changes.Add(change);
                        }
                    }
                }

                value.Add(FacePairCountName, changes.Count);
                if (thresholds != null)
                {
                    value.Add(BendDistributionName, Distribution(thresholds, changes));
                }

                if (changes.Count == 0)
                {
                    return;
                }

                value.Add(MaxBendName, (float)high);
                value.Add(
                    MaxBendFacesName,
                    new object[] { Row(_facets[highFirst]), Row(_facets[highSecond]) });
            }

            private object Row(Facet facet)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { MaterialName, facet.Material },
                    { FaceName, facet.Face },
                    { PointsName, facet.Corners.Select(c => (object)_now[c].Components()).ToArray() },
                };
            }
        }
    }
}
