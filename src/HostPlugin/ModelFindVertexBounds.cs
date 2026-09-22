using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelFindVertexBounds
    {
        public const string ToolName = "model_find_vertex_bounds";

        public const string MaterialIndicesName = "materialIndices";

        public const string CountName = "count";

        public const string MinName = "min";

        public const string MaxName = "max";

        public const string CenterName = "center";

        private static readonly string[] VertexPointing =
        {
            TargetNames.Element.Indices,
            TargetNames.Element.Range,
            TargetNames.Element.All,
            TargetNames.Element.Selected,
        };

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

            List<string> known = new List<string>(VertexPointing) { MaterialIndicesName };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IEnumerable<int> chosen;
            string code;
            string message;
            if (!TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<V3> points = chosen.Select(at => model.Vertex[at].Position).ToList();
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, points.Count },
            };
            if (points.Count != 0)
            {
                V3 low = new V3(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
                V3 high = new V3(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));
                value.Add(MinName, Components(low));
                value.Add(MaxName, Components(high));
                value.Add(CenterName, Components(Vectors.Between(low, high)));
            }

            return ComposedEditResult.Complete(value);
        }

        private static bool TryChosen(
            McpMethodContext context,
            IPXPmx model,
            out IEnumerable<int> chosen,
            out string code,
            out string message)
        {
            chosen = null;
            object given;
            if (!context.Params.TryGetValue(MaterialIndicesName, out given))
            {
                IList<int> positions;
                bool taken = TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Vertex.Count,
                    out positions,
                    out code,
                    out message,
                    context.Screen.Pick(ElementKinds.Vertex, model.Vertex.Count));
                chosen = positions;

                return taken;
            }

            code = ToolEnvelope.InvalidArgument;
            string both = VertexPointing.FirstOrDefault(context.Params.ContainsKey);
            if (both != null)
            {
                message = MaterialIndicesName + " と " + both + " は同時に渡せない。";

                return false;
            }

            object[] items = given as object[];
            if (items == null)
            {
                message = MaterialIndicesName + " は材質の位置の並びでなければならない。";

                return false;
            }

            List<int> materials = new List<int>();
            foreach (object item in items)
            {
                int at;
                if (!ValueInput.TryIndex(item, out at))
                {
                    message = MaterialIndicesName + " は材質の位置の並びでなければならない。";

                    return false;
                }

                if (at < 0 || at >= model.Material.Count)
                {
                    code = ToolEnvelope.IndexOutOfRange;
                    message = MaterialIndicesName + " が並びの外を指している: " + at;

                    return false;
                }

                materials.Add(at);
            }

            chosen = ModelFindMaterialVertices.Used(model, materials);
            code = null;
            message = null;

            return true;
        }

        private static object[] Components(V3 point)
        {
            return new object[] { point.X, point.Y, point.Z };
        }
    }
}
