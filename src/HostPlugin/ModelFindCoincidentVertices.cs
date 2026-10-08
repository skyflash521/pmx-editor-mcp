using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindCoincidentVertices
    {
        public const string ToolName = "model_find_coincident_vertices";

        public const string ThresholdName = "threshold";

        public const string SpreadLimitName = "spreadLimit";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string MaxSpreadName = "maxSpread";

        public const string OverLimitCountName = "overLimitCount";

        public const string GroupsName = "groups";

        public const string VerticesName = "vertices";

        public const string SpreadName = "spread";

        public const string NextOffsetName = "nextOffset";

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
                ThresholdName,
                SpreadLimitName,
                OffsetName,
                LimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string code;
            string message;
            IPXPmx based = null;
            if (context.Params.ContainsKey(ModelMorphFromMoved.BasePmxHandleName))
            {
                if (!ModelMorphFromMoved.TryBase(context, out based, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                string differs = ModelCompareShape.Differs(based, model);
                if (differs != null)
                {
                    return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
                }
            }

            IEnumerable<int> chosen;
            if (!ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float threshold;
            float? spreadLimit;
            int offset = 0;
            int limit = int.MaxValue;
            if (!TryThreshold(context, out threshold, out message)
                || !TrySpreadLimit(context, out spreadLimit, out message)
                || !ModelFindBoneWeights.TryNumber(context, OffsetName, 0, ref offset, out message)
                || !ModelFindBoneWeights.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            Vec[] present = ModelCompareShape.Positions(model);
            Vec[] grouping = based == null ? present : ModelCompareShape.Positions(based);
            List<Group> groups = SurfaceGeometry.Coincident(grouping, chosen, threshold)
                .Select(members => new Group(members, (float)Spread(present, members)))
                .OrderByDescending(group => group.Spread)
                .ThenBy(group => group.Vertices[0])
                .ToList();

            float? largest = groups.Count == 0 ? (float?)null : groups[0].Spread;
            int? over = spreadLimit.HasValue
                ? groups.Count(group => group.Spread > spreadLimit.Value)
                : (int?)null;
            object[] rows = groups.Select(Row).ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    rows,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => ResponseSize.Serializer.Serialize(Valued(rows.Length, largest, over, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Valued(rows.Length, largest, over, offset, page.Items), page.Warnings);
        }

        private static bool TryThreshold(McpMethodContext context, out float threshold, out string message)
        {
            threshold = 0f;
            message = ThresholdName + " は0以上の有限の数でなければならない。";
            object given;

            return context.Params.TryGetValue(ThresholdName, out given)
                && ValueInput.TrySingle(given, out threshold)
                && threshold >= 0;
        }

        private static bool TrySpreadLimit(McpMethodContext context, out float? limit, out string message)
        {
            limit = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(SpreadLimitName, out given))
            {
                return true;
            }

            float read;
            if (!ValueInput.TrySingle(given, out read) || !(read >= 0))
            {
                message = SpreadLimitName + " は0以上の有限の数でなければならない。";

                return false;
            }

            limit = read;

            return true;
        }

        private static IDictionary<string, object> Valued(
            int total, float? largest, int? over, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, total },
            };
            if (largest.HasValue)
            {
                value.Add(MaxSpreadName, largest.Value);
            }

            if (over.HasValue)
            {
                value.Add(OverLimitCountName, over.Value);
            }

            value.Add(GroupsName, taken.ToArray());
            int next = Math.Min(offset, total) + taken.Count;
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static object Row(Group group)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { VerticesName, group.Vertices.Cast<object>().ToArray() },
                { SpreadName, group.Spread },
            };
        }

        private static double Spread(IList<Vec> positions, int[] members)
        {
            Vec[] points = members.Select(at => positions[at]).ToArray();
            if (points.Length < 2)
            {
                return 0;
            }

            Vec centre = new Vec(
                (points.Min(p => p.X) + points.Max(p => p.X)) / 2,
                (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2,
                (points.Min(p => p.Z) + points.Max(p => p.Z)) / 2);
            double[] radius = points.Select(p => (p - centre).Length).ToArray();
            int[] order = Enumerable.Range(0, points.Length)
                .OrderByDescending(at => radius[at])
                .ToArray();

            Vec first = points[order[0]];
            Vec far = points.OrderByDescending(p => (p - first).Length).First();
            double widest = points.Max(p => (p - far).Length);
            for (int at = 0; at + 1 < order.Length; at++)
            {
                if (radius[order[at]] + radius[order[at + 1]] <= widest)
                {
                    break;
                }

                for (int other = at + 1; other < order.Length; other++)
                {
                    if (radius[order[at]] + radius[order[other]] <= widest)
                    {
                        break;
                    }

                    widest = Math.Max(widest, (points[order[at]] - points[order[other]]).Length);
                }
            }

            return widest;
        }

        private sealed class Group
        {
            public Group(int[] vertices, float spread)
            {
                Vertices = vertices;
                Spread = spread;
            }

            public int[] Vertices { get; }

            public float Spread { get; }
        }
    }
}
