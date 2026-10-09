using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindParts
    {
        public const string ToolName = "model_find_parts";

        public const string WeldDistanceName = "weldDistance";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string PartsName = "parts";

        public const string VertexRunsName = "vertexRuns";

        public const string VertexCountName = "vertexCount";

        public const string FaceCountName = "faceCount";

        public const string MinName = "min";

        public const string MaxName = "max";

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

            List<string> known = new List<string>
            {
                ModelFindVertexBounds.MaterialIndicesName,
                WeldDistanceName,
                OffsetName,
                LimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            List<int> materials;
            string code;
            string message;
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindVertexBounds.MaterialIndicesName,
                    out materials,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float weld = 0f;
            int offset = 0;
            int limit = int.MaxValue;
            if (!TryWeldDistance(context, ref weld, out message)
                || !ComposedInput.TryNumber(context, OffsetName, 0, ref offset, out message)
                || !ComposedInput.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            object[] all = Divide(model, materials, weld).Select(Row).ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    all,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => ResponseSize.Serializer.Serialize(Valued(all.Length, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(Valued(all.Length, offset, page.Items), page.Warnings);
        }

        private static bool TryWeldDistance(McpMethodContext context, ref float weld, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(WeldDistanceName, out given))
            {
                return true;
            }

            float read;
            if (!ValueInput.TrySingle(given, out read) || !(read >= 0))
            {
                message = WeldDistanceName + " は0以上の有限の数でなければならない。";

                return false;
            }

            weld = read;

            return true;
        }

        private static IDictionary<string, object> Valued(int total, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, total },
                { PartsName, taken.ToArray() },
            };
            int next = Math.Min(offset, total) + taken.Count;
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static object Row(Part part)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { VertexRunsName, PositionRuns.Joined(part.Vertices).ToArray() },
                { VertexCountName, part.Vertices.Count },
                { FaceCountName, part.Faces },
                { MinName, part.Low.Components() },
                { MaxName, part.High.Components() },
            };
        }

        private static List<Part> Divide(IPXPmx model, IEnumerable<int> materials, double weld)
        {
            Dictionary<IPXVertex, int> places = new Dictionary<IPXVertex, int>(
                ReferenceComparer<IPXVertex>.Instance);
            Vec[] positions = new Vec[model.Vertex.Count];
            for (int at = 0; at < positions.Length; at++)
            {
                if (!places.ContainsKey(model.Vertex[at]))
                {
                    places.Add(model.Vertex[at], at);
                }

                positions[at] = Vec.Of(model.Vertex[at].Position);
            }

            int[] parent = Enumerable.Range(0, positions.Length).ToArray();
            SortedSet<int> used = new SortedSet<int>();
            List<int> firstCorners = new List<int>();
            foreach (int material in materials.Distinct())
            {
                foreach (IPXFace face in model.Material[material].Faces)
                {
                    int[] corners = ViewSelection.Corners(face)
                        .Where(corner => corner != null && places.ContainsKey(corner))
                        .Select(corner => places[corner])
                        .ToArray();
                    if (corners.Length == 0)
                    {
                        continue;
                    }

                    firstCorners.Add(corners[0]);
                    foreach (int corner in corners)
                    {
                        used.Add(corner);
                        Join(parent, corners[0], corner);
                    }
                }
            }

            foreach (int[] group in SurfaceGeometry.Coincident(positions, used, weld))
            {
                foreach (int member in group)
                {
                    Join(parent, group[0], member);
                }
            }

            Dictionary<int, Part> parts = new Dictionary<int, Part>();
            foreach (int at in used)
            {
                int root = Root(parent, at);
                Part part;
                if (!parts.TryGetValue(root, out part))
                {
                    part = new Part(positions[at]);
                    parts.Add(root, part);
                }

                part.Add(at, positions[at]);
            }

            foreach (int corner in firstCorners)
            {
                parts[Root(parent, corner)].Faces++;
            }

            return parts.Values
                .OrderByDescending(part => part.Vertices.Count)
                .ThenBy(part => part.Vertices[0])
                .ToList();
        }

        private static int Root(int[] parent, int at)
        {
            while (parent[at] != at)
            {
                parent[at] = parent[parent[at]];
                at = parent[at];
            }

            return at;
        }

        private static void Join(int[] parent, int first, int second)
        {
            int one = Root(parent, first);
            int other = Root(parent, second);
            if (one != other)
            {
                parent[Math.Max(one, other)] = Math.Min(one, other);
            }
        }

        private sealed class Part
        {
            public Part(Vec first)
            {
                Vertices = new List<int>();
                Low = first;
                High = first;
            }

            public List<int> Vertices { get; }

            public int Faces { get; set; }

            public Vec Low { get; private set; }

            public Vec High { get; private set; }

            public void Add(int index, Vec at)
            {
                Vertices.Add(index);
                Low = new Vec(Math.Min(Low.X, at.X), Math.Min(Low.Y, at.Y), Math.Min(Low.Z, at.Z));
                High = new Vec(Math.Max(High.X, at.X), Math.Max(High.Y, at.Y), Math.Max(High.Z, at.Z));
            }
        }
    }
}
