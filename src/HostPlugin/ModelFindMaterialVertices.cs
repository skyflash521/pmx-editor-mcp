using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelFindMaterialVertices
    {
        public const string ToolName = "model_find_material_vertices";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string TotalName = "total";

        public const string VertexIndicesName = "vertexIndices";

        public const string NextOffsetName = "nextOffset";

        private static readonly JavaScriptSerializer Sizer = new JavaScriptSerializer();

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
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                OffsetName,
                LimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IList<int> chosen;
            int offset = 0;
            int limit = int.MaxValue;
            string code;
            string message;
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Material.Count,
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(ElementKinds.Material, model.Material.Count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!TryNumber(context, OffsetName, 0, ref offset, out message)
                || !TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            object[] all = Used(model, chosen).Cast<object>().ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    all,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => Sizer.Serialize(Valued(all.Length, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Valued(all.Length, offset, page.Items), page.Warnings);
        }

        public static SortedSet<int> Used(IPXPmx model, IEnumerable<int> materials)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            Dictionary<IPXVertex, int> placed = new Dictionary<IPXVertex, int>(
                ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                if (!placed.ContainsKey(model.Vertex[at]))
                {
                    placed.Add(model.Vertex[at], at);
                }
            }

            SortedSet<int> used = new SortedSet<int>();
            foreach (int material in materials)
            {
                foreach (IPXFace face in model.Material[material].Faces)
                {
                    foreach (IPXVertex corner in ViewSelection.Corners(face))
                    {
                        int position;
                        if (corner != null && placed.TryGetValue(corner, out position))
                        {
                            used.Add(position);
                        }
                    }
                }
            }

            return used;
        }

        private static IDictionary<string, object> Valued(
            int total, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TotalName, total },
                { VertexIndicesName, taken.ToArray() },
            };
            int next = Math.Min(offset, total) + taken.Count;
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static bool TryNumber(
            McpMethodContext context, string name, int least, ref int taken, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given) || given == null)
            {
                return true;
            }

            long number;
            if (!ValueInput.TryInteger(given, out number)
                || number < least
                || number > int.MaxValue)
            {
                message = name + " は " + least.ToString(CultureInfo.InvariantCulture)
                    + " 以上の整数でなければならない。";

                return false;
            }

            taken = (int)number;

            return true;
        }
    }
}
