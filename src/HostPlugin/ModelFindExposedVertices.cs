using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using SurfaceTree = PmxEditorMcp.SurfaceGeometry.SurfaceTree;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindExposedVertices
    {
        public const string ToolName = "model_find_exposed_vertices";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string MaxDepthName = "maxDepth";

        public const string MaxVertexName = "maxVertex";

        public const string VerticesName = "vertices";

        public const string VertexName = "vertex";

        public const string DepthName = "depth";

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
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                OffsetName,
                LimitName,
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

            string differs = ModelCompareShape.Differs(based, model);
            if (differs != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
            }

            IEnumerable<int> chosen;
            if (!ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<int> surface;
            if (!ModelFindSurfaceDistances.TryMaterials(
                context,
                model,
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                out surface,
                out code,
                out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int offset = 0;
            int limit = int.MaxValue;
            if (!ComposedInput.TryNumber(context, OffsetName, 0, ref offset, out message)
                || !ComposedInput.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            SurfaceTree before = SurfaceTree.Of(based, surface);
            SurfaceTree now = SurfaceTree.Of(model, surface);
            if (before == null || now == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName + " の材質に面積のある面が1つも無い。");
            }

            Vec[] posed = ModelCompareShape.Positions(model);
            Vec[] resting = ModelCompareShape.Positions(based);
            List<Exposed> exposed = new List<Exposed>();
            foreach (int at in new SortedSet<int>(chosen))
            {
                if (before.IsInside(resting[at]) && !now.IsInside(posed[at]))
                {
                    exposed.Add(new Exposed(
                        at, (float)Math.Abs(now.Nearest(posed[at], double.PositiveInfinity).Signed)));
                }
            }

            exposed = exposed.OrderByDescending(e => e.Depth).ThenBy(e => e.Vertex).ToList();
            object[] rows = exposed.Select(Row).ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    rows,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => ResponseSize.Serializer.Serialize(Valued(exposed, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(Valued(exposed, offset, page.Items), page.Warnings);
        }

        private static IDictionary<string, object> Valued(
            List<Exposed> exposed, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, exposed.Count },
            };
            if (exposed.Count > 0)
            {
                value.Add(MaxDepthName, exposed[0].Depth);
                value.Add(MaxVertexName, exposed[0].Vertex);
            }

            value.Add(VerticesName, taken.ToArray());
            int next = Math.Min(offset, exposed.Count) + taken.Count;
            if (next < exposed.Count)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static object Row(Exposed exposed)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { VertexName, exposed.Vertex },
                { DepthName, exposed.Depth },
            };
        }

        private sealed class Exposed
        {
            public Exposed(int vertex, float depth)
            {
                Vertex = vertex;
                Depth = depth;
            }

            public int Vertex { get; }

            public float Depth { get; }
        }
    }
}
