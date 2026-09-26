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

        public const string MinVerticesName = "minVertices";

        public const string MaxVerticesName = "maxVertices";

        public const string BoxesName = "boxes";

        private static readonly string[] BoxEdgeNames = { "minX", "maxX", "minY", "maxY", "minZ", "maxZ" };

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

            List<string> known = new List<string>(VertexPointing) { MaterialIndicesName, BoxesName };
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

            List<float[]> boxes;
            if (!TryBoxes(context, out boxes, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            List<KeyValuePair<int, V3>> points = chosen
                .Select(at => new KeyValuePair<int, V3>(at, model.Vertex[at].Position))
                .ToList();
            Dictionary<string, object> value = Bounds(points);
            if (boxes != null)
            {
                value.Add(
                    BoxesName,
                    boxes.Select(box => (object)Bounds(points.Where(p => Inside(box, p.Value)).ToList())).ToArray());
            }

            return ComposedEditResult.Complete(value);
        }

        private static Dictionary<string, object> Bounds(List<KeyValuePair<int, V3>> points)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, points.Count },
            };
            if (points.Count == 0)
            {
                return value;
            }

            KeyValuePair<int, V3>[] lows = new KeyValuePair<int, V3>[3];
            KeyValuePair<int, V3>[] highs = new KeyValuePair<int, V3>[3];
            for (int axis = 0; axis < 3; axis++)
            {
                lows[axis] = points[0];
                highs[axis] = points[0];
                foreach (KeyValuePair<int, V3> point in points)
                {
                    if (Component(point.Value, axis) < Component(lows[axis].Value, axis))
                    {
                        lows[axis] = point;
                    }

                    if (Component(point.Value, axis) > Component(highs[axis].Value, axis))
                    {
                        highs[axis] = point;
                    }
                }
            }

            V3 min = new V3(lows[0].Value.X, lows[1].Value.Y, lows[2].Value.Z);
            V3 max = new V3(highs[0].Value.X, highs[1].Value.Y, highs[2].Value.Z);
            value.Add(MinName, Components(min));
            value.Add(MaxName, Components(max));
            value.Add(CenterName, Components(Vectors.Between(min, max)));
            value.Add(MinVerticesName, lows.Select(p => (object)p.Key).ToArray());
            value.Add(MaxVerticesName, highs.Select(p => (object)p.Key).ToArray());

            return value;
        }

        private static float Component(V3 point, int axis)
        {
            return axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;
        }

        private static bool Inside(float[] box, V3 point)
        {
            float[] components = { point.X, point.Y, point.Z };
            for (int axis = 0; axis < components.Length; axis++)
            {
                if (components[axis] < box[axis * 2] || components[axis] > box[(axis * 2) + 1])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBoxes(McpMethodContext context, out List<float[]> boxes, out string message)
        {
            boxes = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(BoxesName, out given))
            {
                return true;
            }

            string shape = BoxesName + " は " + string.Join("・", BoxEdgeNames)
                + " を省いてよい数として持つ組の並びでなければならない。";
            object[] items = given as object[];
            if (items == null)
            {
                message = shape;

                return false;
            }

            boxes = new List<float[]>();
            foreach (object item in items)
            {
                IDictionary<string, object> members = item as IDictionary<string, object>;
                if (members == null || members.Keys.Any(name => !BoxEdgeNames.Contains(name)))
                {
                    message = shape;

                    return false;
                }

                float[] box = new float[BoxEdgeNames.Length];
                for (int at = 0; at < BoxEdgeNames.Length; at++)
                {
                    object edge;
                    float read;
                    if (!members.TryGetValue(BoxEdgeNames[at], out edge))
                    {
                        read = at % 2 == 0 ? float.NegativeInfinity : float.PositiveInfinity;
                    }
                    else if (!ValueInput.TrySingle(edge, out read))
                    {
                        message = shape;

                        return false;
                    }

                    box[at] = read;
                }

                for (int at = 0; at < BoxEdgeNames.Length; at += 2)
                {
                    if (box[at] > box[at + 1])
                    {
                        message = BoxEdgeNames[at] + " が " + BoxEdgeNames[at + 1] + " より大きい。";

                        return false;
                    }
                }

                boxes.Add(box);
            }

            return true;
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
