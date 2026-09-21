using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、関連する要素で選び直すツール。
    /// </summary>
    public static class ViewSelectRelated
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_select_related";

        /// <summary>選んだ頂点だけで作られている面を選ぶ。</summary>
        public const string VerticesToFaces = "verticesToFaces";

        /// <summary>選んだ面が使う頂点を選ぶ。</summary>
        public const string FacesToVertices = "facesToVertices";

        /// <summary>選んだ面と辺を共有する面を足す。</summary>
        public const string ExpandAdjacentFaces = "expandAdjacentFaces";

        /// <summary>指した材質の面を選ぶ。</summary>
        public const string MaterialToFaces = "materialToFaces";

        /// <summary>選んだ頂点を使う材質の面を選ぶ。</summary>
        public const string VerticesToMaterials = "verticesToMaterials";

        /// <summary>選んだ面を持つ材質の面を全部選ぶ。</summary>
        public const string FacesToMaterials = "facesToMaterials";

        /// <summary>選んだ面を持つ材質の面を、選択から外す。</summary>
        public const string ExcludeFacesMaterials = "excludeFacesMaterials";

        /// <summary>どの面にも使われていない頂点を選ぶ。</summary>
        public const string UnusedVertices = "unusedVertices";

        /// <summary>エッジの倍率が1でない頂点を選ぶ。</summary>
        public const string EdgeScaleChangedVertices = "edgeScaleChangedVertices";

        public const string BonesToWeightedVertices = "bonesToWeightedVertices";

        /// <summary>UVがその範囲に入る頂点を選ぶ。</summary>
        public const string UvRegionVertices = "uvRegionVertices";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { VerticesToFaces, FacesToVertices, ExpandAdjacentFaces, MaterialToFaces, VerticesToMaterials, FacesToMaterials, ExcludeFacesMaterials, UnusedVertices, EdgeScaleChangedVertices, BonesToWeightedVertices, UvRegionVertices };
            }
        }

        /// <summary>材質の位置を受け取る入力の名前。</summary>
        public const string MaterialIndicesName = "materialIndices";

        public const string ReleaseSourceName = "releaseSource";

        public const string MinUName = "minU";

        public const string MaxUName = "maxU";

        public const string MinVName = "minV";

        public const string MaxVName = "maxV";

        /// <summary>選んだ要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        public const string KindName = "kind";

        /// <summary>辺を共有する2つの面が同じくする頂点の数。</summary>
        private const int SharedCorners = 2;

        /// <summary>ツールを表へ足す。</summary>
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

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                MaterialIndicesName,
                ReleaseSourceName,
                MinUName,
                MaxUName,
                MinVName,
                MaxVName,
            };
            methods.Add(
                ToolName, screen.Method(known, ScreenNeeds.View | ScreenNeeds.Pmx, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            string operation;
            string code;
            string message;
            IList<int> materials;
            UvRegion region;
            bool release;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !ComposedInput.TryIndices(
                    context,
                    MaterialIndicesName,
                    operation,
                    new[] { MaterialToFaces },
                    model.Material.Count,
                    out materials,
                    out code,
                    out message)
                || !TryRegion(context, operation, out region, out code, out message)
                || !TryReleaseSource(context, out release, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXFace> faces = ViewSelection.Faces(model);
            IList<int> owners = ViewSelection.Owners(model);
            IList<int> made;
            string kind = ElementKinds.Face;
            switch (operation)
            {
                case VerticesToFaces:
                    made = Whole(faces, Chosen(model, Held(parts, model, ElementKinds.Vertex)));

                    break;

                case FacesToVertices:
                    kind = ElementKinds.Vertex;
                    made = Used(model, faces, Held(parts, model, ElementKinds.Face));

                    break;

                case ExpandAdjacentFaces:
                    made = Beside(faces, Held(parts, model, ElementKinds.Face));

                    break;

                case MaterialToFaces:
                    made = Owned(owners, materials);

                    break;

                case VerticesToMaterials:
                    made = Owned(
                        owners,
                        Reaching(
                            faces, owners, Chosen(model, Held(parts, model, ElementKinds.Vertex))));

                    break;

                case UnusedVertices:
                    kind = ElementKinds.Vertex;
                    made = Loose(model, faces);

                    break;

                case EdgeScaleChangedVertices:
                    kind = ElementKinds.Vertex;
                    made = Edged(model);

                    break;

                case BonesToWeightedVertices:
                    kind = ElementKinds.Vertex;
                    made = Weighed(model, Held(parts, model, ElementKinds.Bone));

                    break;

                case UvRegionVertices:
                    kind = ElementKinds.Vertex;
                    made = Inside(model, region);

                    break;

                default:
                    IList<int> held = Held(parts, model, ElementKinds.Face);
                    IList<int> theirs = Owned(owners, held.Select(at => owners[at]).ToList());
                    made = string.Equals(operation, FacesToMaterials, StringComparison.Ordinal)
                        ? theirs
                        : held.Except(theirs).ToList();

                    break;
            }

            ViewSelection.Put(parts.View, kind, made);
            string source = Source(operation);
            if (release && source != null && !string.Equals(source, kind, StringComparison.Ordinal))
            {
                ViewSelection.Put(parts.View, source, new int[0]);
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { KindName, kind },
                    { SelectedName, made.Count },
                });
        }

        private static string Source(string operation)
        {
            switch (operation)
            {
                case VerticesToFaces:
                case VerticesToMaterials:
                    return ElementKinds.Vertex;

                case FacesToVertices:
                case ExpandAdjacentFaces:
                case FacesToMaterials:
                case ExcludeFacesMaterials:
                    return ElementKinds.Face;

                case BonesToWeightedVertices:
                    return ElementKinds.Bone;

                default:
                    return null;
            }
        }

        private static bool TryReleaseSource(
            McpMethodContext context, out bool release, out string code, out string message)
        {
            code = null;
            message = null;
            release = false;
            object given;
            if (!context.Params.TryGetValue(ReleaseSourceName, out given))
            {
                return true;
            }

            if (!(given is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = ReleaseSourceName + " は真偽でなければならない。";

                return false;
            }

            release = (bool)given;

            return true;
        }

        /// <summary>UVの範囲を読む。上限が下限より小さければ偽を返し、断る内容を渡す。</summary>
        private static bool TryRegion(
            McpMethodContext context,
            string operation,
            out UvRegion region,
            out string code,
            out string message)
        {
            region = null;
            string[] wanted = new[] { UvRegionVertices };
            float minU;
            float maxU;
            float minV;
            float maxV;
            if (!ComposedInput.TryFloat(
                    context, MinUName, operation, wanted, ComposedInput.NoFloor,
                    ComposedInput.NoCeiling, out minU, out code, out message)
                || !ComposedInput.TryFloat(
                    context, MaxUName, operation, wanted, ComposedInput.NoFloor,
                    ComposedInput.NoCeiling, out maxU, out code, out message)
                || !ComposedInput.TryFloat(
                    context, MinVName, operation, wanted, ComposedInput.NoFloor,
                    ComposedInput.NoCeiling, out minV, out code, out message)
                || !ComposedInput.TryFloat(
                    context, MaxVName, operation, wanted, ComposedInput.NoFloor,
                    ComposedInput.NoCeiling, out maxV, out code, out message))
            {
                return false;
            }

            if (maxU < minU || maxV < minV)
            {
                code = ToolEnvelope.InvalidArgument;
                message = MaxUName + " は " + MinUName + " 以上、" + MaxVName + " は "
                    + MinVName + " 以上でなければならない。";

                return false;
            }

            region = new UvRegion(minU, maxU, minV, maxV);

            return true;
        }

        /// <summary>UVがその範囲に入る頂点の位置。UVを持たない頂点は入らない。</summary>
        private static IList<int> Inside(IPXPmx model, UvRegion region)
        {
            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => region.Holds(model.Vertex[at].UV))
                .ToList();
        }

        private static IList<int> Held(ScreenParts parts, IPXPmx model, string kind)
        {
            return ViewSelection.Taken(parts.View, kind, ViewSelection.Count(model, kind));
        }

        /// <summary>3つの頂点がすべて選ばれている面。</summary>
        private static IList<int> Whole(IList<IPXFace> faces, ICollection<IPXVertex> chosen)
        {
            return Enumerable.Range(0, faces.Count)
                .Where(at => ViewSelection.Corners(faces[at]).All(chosen.Contains))
                .ToList();
        }

        /// <summary>指した面が使っている頂点の位置。</summary>
        private static IList<int> Used(IPXPmx model, IList<IPXFace> faces, IList<int> held)
        {
            IDictionary<IPXVertex, int> at = Placed(model.Vertex);

            return held.SelectMany(face => ViewSelection.Corners(faces[face]))
                .Where(vertex => vertex != null && at.ContainsKey(vertex))
                .Select(vertex => at[vertex])
                .Distinct()
                .ToList();
        }

        /// <summary>指した面と、その面と辺を共有する面。</summary>
        private static IList<int> Beside(IList<IPXFace> faces, IList<int> held)
        {
            IDictionary<IPXVertex, IList<int>> touching = Touching(faces);
            HashSet<int> made = new HashSet<int>(held);
            foreach (int at in held)
            {
                IPXVertex[] theirs = ViewSelection.Corners(faces[at]);
                foreach (int other in Around(touching, theirs))
                {
                    if (ViewSelection.Corners(faces[other])
                            .Count(corner => theirs.Any(held => ReferenceEquals(held, corner)))
                        >= SharedCorners)
                    {
                        made.Add(other);
                    }
                }
            }

            return made.ToList();
        }

        /// <summary>頂点ごとの、その頂点を使っている面の位置。</summary>
        private static IDictionary<IPXVertex, IList<int>> Touching(IList<IPXFace> faces)
        {
            Dictionary<IPXVertex, IList<int>> made =
                new Dictionary<IPXVertex, IList<int>>(ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < faces.Count; at++)
            {
                foreach (IPXVertex corner in
                    ViewSelection.Corners(faces[at]).Where(corner => corner != null))
                {
                    IList<int> held;
                    if (!made.TryGetValue(corner, out held))
                    {
                        held = new List<int>();
                        made.Add(corner, held);
                    }

                    held.Add(at);
                }
            }

            return made;
        }

        /// <summary>その頂点のどれかを使っている面の位置。</summary>
        private static IEnumerable<int> Around(
            IDictionary<IPXVertex, IList<int>> touching, IEnumerable<IPXVertex> corners)
        {
            return corners
                .Where(corner => corner != null && touching.ContainsKey(corner))
                .SelectMany(corner => touching[corner])
                .Distinct();
        }

        /// <summary>指した材質が持つ面の位置。</summary>
        private static IList<int> Owned(IList<int> owners, IList<int> materials)
        {
            HashSet<int> chosen = new HashSet<int>(materials);

            return Enumerable.Range(0, owners.Count)
                .Where(at => chosen.Contains(owners[at]))
                .ToList();
        }

        /// <summary>選んだ頂点を使っている材質の位置。</summary>
        private static IList<int> Reaching(
            IList<IPXFace> faces, IList<int> owners, ICollection<IPXVertex> chosen)
        {
            return Enumerable.Range(0, faces.Count)
                .Where(at => ViewSelection.Corners(faces[at]).Any(chosen.Contains))
                .Select(at => owners[at])
                .Distinct()
                .ToList();
        }

        /// <summary>どの面にも使われていない頂点の位置。</summary>
        private static IList<int> Loose(IPXPmx model, IList<IPXFace> faces)
        {
            HashSet<IPXVertex> used = new HashSet<IPXVertex>(
                faces.SelectMany(ViewSelection.Corners).Where(vertex => vertex != null),
                ReferenceComparer<IPXVertex>.Instance);

            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => !used.Contains(model.Vertex[at]))
                .ToList();
        }

        private static IList<int> Weighed(IPXPmx model, IList<int> bones)
        {
            HashSet<IPXBone> chosen = new HashSet<IPXBone>(
                bones.Select(at => model.Bone[at]), ReferenceComparer<IPXBone>.Instance);

            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => VertexWeights.All(model.Vertex[at])
                    .Any(share => share.Value > 0f && chosen.Contains(share.Key)))
                .ToList();
        }

        /// <summary>エッジの倍率が1でない頂点の位置。</summary>
        private static IList<int> Edged(IPXPmx model)
        {
            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => model.Vertex[at].EdgeScale != 1f)
                .ToList();
        }

        /// <summary>その位置に居る頂点。</summary>
        private static HashSet<IPXVertex> Chosen(IPXPmx model, IList<int> held)
        {
            return new HashSet<IPXVertex>(
                held.Select(at => model.Vertex[at]), ReferenceComparer<IPXVertex>.Instance);
        }

        private static IDictionary<IPXVertex, int> Placed(IList<IPXVertex> items)
        {
            Dictionary<IPXVertex, int> made =
                new Dictionary<IPXVertex, int>(ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < items.Count; at++)
            {
                if (items[at] != null && !made.ContainsKey(items[at]))
                {
                    made.Add(items[at], at);
                }
            }

            return made;
        }
    }
}
