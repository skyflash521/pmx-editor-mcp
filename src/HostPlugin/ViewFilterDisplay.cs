using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class ViewFilterDisplay
    {
        public const string ToolName = "view_filter_display";

        public const string MaterialsFromVertices = "materialsFromVertices";

        public const string MaterialsFromFaces = "materialsFromFaces";

        public const string ExcludeMaterialsFromFaces = "excludeMaterialsFromFaces";

        public const string VerticesByEdgeScale = "verticesByEdgeScale";

        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    MaterialsFromVertices,
                    MaterialsFromFaces,
                    ExcludeMaterialsFromFaces,
                    VerticesByEdgeScale,
                };
            }
        }

        public const string ShownName = "shown";

        public const string KindName = "kind";

        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            List<string> known = new List<string> { ComposedOperation.OperationName };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Parts | ScreenNeeds.Pmx,
                    ScreenRefreshKind.Drawn,
                    Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            string operation;
            string code;
            string message;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (string.Equals(operation, VerticesByEdgeScale, StringComparison.Ordinal))
            {
                return Narrowed(model, parts);
            }

            IPEPartsSelectConnector held = (IPEPartsSelectConnector)parts.Parts;
            if (!PreconditionGate.TryAccept(
                    PreconditionKind.ListedParts, held.MaterialItemsCount, false, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, message);
            }

            IList<int> made = Materials(model, parts, operation);
            if (string.Equals(operation, ExcludeMaterialsFromFaces, StringComparison.Ordinal))
            {
                HashSet<int> gone = new HashSet<int>(made);
                made = (held.GetCheckedMaterialIndices() ?? new int[0])
                    .Where(at => !gone.Contains(at))
                    .ToList();
            }

            int[] shown = made.Distinct().OrderBy(at => at).ToArray();
            held.SetCheckedMaterialIndices(shown);

            return Answer(ElementKinds.Material, shown.Length);
        }

        private static IList<int> Materials(IPXPmx model, ScreenParts parts, string operation)
        {
            IList<int> owners = ViewSelection.Owners(model);
            IList<IPXFace> faces = ViewSelection.Faces(model);
            if (!string.Equals(operation, MaterialsFromVertices, StringComparison.Ordinal))
            {
                return Held(model, parts, ElementKinds.Face)
                    .Select(at => owners[at])
                    .Distinct()
                    .ToList();
            }

            HashSet<IPXVertex> chosen = new HashSet<IPXVertex>(
                Held(model, parts, ElementKinds.Vertex).Select(at => model.Vertex[at]),
                ReferenceComparer<IPXVertex>.Instance);

            return Enumerable.Range(0, faces.Count)
                .Where(at => ViewSelection.Corners(faces[at]).Any(chosen.Contains))
                .Select(at => owners[at])
                .Distinct()
                .ToList();
        }

        private static ComposedEditResult Narrowed(IPXPmx model, ScreenParts parts)
        {
            int[] shown = Enumerable.Range(0, model.Vertex.Count)
                .Where(at => model.Vertex[at].EdgeScale != 1f)
                .ToArray();
            ((IPXPmxViewConnector)parts.View).SetVertexIndices(shown);

            return Answer(ElementKinds.Vertex, shown.Length);
        }

        private static IList<int> Held(IPXPmx model, ScreenParts parts, string kind)
        {
            return ViewSelection.Taken(parts.View, kind, ViewSelection.Count(model, kind));
        }

        private static ComposedEditResult Answer(string kind, int shown)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { KindName, kind },
                    { ShownName, shown },
                });
        }
    }
}
