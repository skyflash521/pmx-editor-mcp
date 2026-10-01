using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Hit = PmxEditorMcp.SurfaceGeometry.Hit;
using SurfaceTree = PmxEditorMcp.SurfaceGeometry.SurfaceTree;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindSurfaceDistances
    {
        public const string ToolName = "model_find_surface_distances";

        public const string SurfaceMaterialIndicesName = "surfaceMaterialIndices";

        public const string DistanceLimitName = "distanceLimit";

        public const string DistanceThresholdsName = "distanceThresholds";

        public const string SideByName = "sideBy";

        public const string NormalSide = "normal";

        public const string RayParitySide = "rayParity";

        public const string DistributionName = "distribution";

        public const string LimitName = "limit";

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
                DistanceThresholdsName,
                SideByName,
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
            if (!TryMaterials(context, model, SurfaceMaterialIndicesName, out surface, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float? limit;
            List<float[]> boxes;
            List<float> thresholds;
            if (!TryLimit(context, out limit, out message)
                || !TryThresholds(context, DistanceThresholdsName, out thresholds, out message)
                || !ModelFindVertexBounds.TryBoxes(context, out boxes, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            string sideBy = NormalSide;
            if (context.Params.ContainsKey(SideByName)
                && !ComposedInput.TryChoice(
                    context,
                    SideByName,
                    new[] { NormalSide, RayParitySide },
                    out sideBy,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
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
                Hit hit = tree.Nearest(Vec.Of(position), reach);
                if (hit != null && sideBy == RayParitySide)
                {
                    double away = Math.Abs(hit.Signed);
                    hit = new Hit(
                        hit.Point,
                        away > 0 && tree.IsInside(Vec.Of(position)) ? -away : away,
                        hit.Front,
                        hit.Corners,
                        hit.Barycentric);
                }

                measures.Add(new Measure(at, position, hit));
            }

            Dictionary<string, object> value = Summary(measures, limit.HasValue, thresholds);
            if (boxes != null)
            {
                value.Add(
                    ModelFindVertexBounds.BoxesName,
                    boxes.Select(box => (object)Summary(
                        measures.Where(m => ModelFindVertexBounds.Inside(box, m.Position)).ToList(),
                        limit.HasValue,
                        thresholds)).ToArray());
            }

            return ComposedEditResult.Complete(value);
        }

        private static Dictionary<string, object> Summary(
            List<Measure> measures, bool limited, List<float> thresholds)
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

            if (thresholds != null)
            {
                value.Add(
                    DistributionName,
                    thresholds.Select(t => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { LimitName, t },
                        { CountName, near.Count(m => Math.Abs(m.Hit.Signed) <= t) },
                    }).ToArray());
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

        internal static bool TryThresholds(
            McpMethodContext context, string name, out List<float> thresholds, out string message)
        {
            thresholds = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given))
            {
                return true;
            }

            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                message = name + " は0以上の有限の数を1つ以上並べた並びでなければならない。";

                return false;
            }

            List<float> read = new List<float>();
            foreach (object item in items)
            {
                float one;
                if (!ValueInput.TrySingle(item, out one) || !(one >= 0) || float.IsInfinity(one))
                {
                    message = name + " は0以上の有限の数を1つ以上並べた並びでなければならない。";

                    return false;
                }

                read.Add(one);
            }

            thresholds = read;

            return true;
        }

        internal static bool TryMaterials(
            McpMethodContext context,
            IPXPmx model,
            string name,
            out List<int> surface,
            out string code,
            out string message)
        {
            return TryPositions(
                context, name, "材質", model.Material.Count, out surface, out code, out message);
        }

        internal static bool TryPositions(
            McpMethodContext context,
            string name,
            string noun,
            int count,
            out List<int> surface,
            out string code,
            out string message)
        {
            surface = new List<int>();
            code = ToolEnvelope.InvalidArgument;
            string shape = name + " は" + noun + "の位置を1つ以上並べた並びでなければならない。";
            object given;
            object[] items;
            if (!context.Params.TryGetValue(name, out given)
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

                if (at < 0 || at >= count)
                {
                    code = ToolEnvelope.IndexOutOfRange;
                    message = name + " が並びの外を指している: " + at;

                    return false;
                }

                surface.Add(at);
            }

            code = null;
            message = null;

            return true;
        }

        internal static bool TryLimit(McpMethodContext context, out float? limit, out string message)
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
    }
}
