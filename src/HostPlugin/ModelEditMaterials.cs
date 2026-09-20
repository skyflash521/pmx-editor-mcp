using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した材質の結合・単一化・分割・取り出し・複製・色の丸めを行うツール。
    /// </summary>
    public static class ModelEditMaterials
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_materials";

        /// <summary>指した材質を、先頭の材質へまとめる。</summary>
        public const string Merge = "merge";

        /// <summary>
        /// モデルの全部の材質を1つへまとめる。指した材質では絞らず、並びのすべてを相手にする。
        /// </summary>
        public const string MergeAll = "mergeAll";

        /// <summary>色の許容幅の中で設定が同じ材質どうしをまとめる。</summary>
        public const string MergeSame = "mergeSame";

        /// <summary>指した面を新しい材質へ移す。</summary>
        public const string ExtractFaces = "extractFaces";

        public const string ExtractFacesWithVertices = "extractFacesWithVertices";

        public const string ExtractVertices = "extractVertices";

        public const string VertexIndicesName = "vertexIndices";

        /// <summary>材質の持ち物を複製して新しい材質にする。</summary>
        public const string DuplicateParts = "duplicateParts";

        /// <summary>材質の色の各成分を0以上1以下へ丸める。</summary>
        public const string ClampColor = "clampColor";

        /// <summary>同じ材質と見なす色の許容幅を受け取る入力の名前。</summary>
        public const string ColorToleranceName = "colorTolerance";

        /// <summary>複製する持ち物を受け取る入力の名前。</summary>
        public const string PartsName = "parts";

        /// <summary>面だけを複製する。</summary>
        public const string FacesOnly = "facesOnly";

        /// <summary>面と頂点を複製する。</summary>
        public const string WithVertices = "withVertices";

        /// <summary>面と頂点と関連するモーフを複製する。</summary>
        public const string WithMorphs = "withMorphs";

        /// <summary>材質の中の面を位置の配列で指す入力の名前。</summary>
        public const string FaceIndicesName = "faceIndices";

        /// <summary>材質の中の面を始まりと件数で指す入力の名前。</summary>
        public const string FaceRangeName = "faceRange";

        /// <summary>材質の中の面を全部指す入力の名前。</summary>
        public const string FaceAllName = "faceAll";

        /// <summary>変えた材質の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えた材質の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>足した材質の位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        private const string FaceHandlesName = "faceHandles";

        private static readonly TargetNames FaceNames =
            new TargetNames(FaceIndicesName, FaceRangeName, FaceAllName, FaceHandlesName);

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    Merge,
                    MergeAll,
                    MergeSame,
                    ExtractFaces,
                    ExtractFacesWithVertices,
                    ExtractVertices,
                    DuplicateParts,
                    ClampColor,
                };
            }
        }

        private static IList<string> Extracting
        {
            get { return new[] { ExtractFaces, ExtractFacesWithVertices }; }
        }

        /// <summary>受け取れる持ち物。スキーマが並べる順。</summary>
        public static IList<string> Parts
        {
            get { return new[] { FacesOnly, WithVertices, WithMorphs }; }
        }

        /// <summary>ツールを表へ足す。</summary>
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
                ComposedOperation.OperationName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                ColorToleranceName,
                PartsName,
                FaceIndicesName,
                FaceRangeName,
                FaceAllName,
                VertexIndicesName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Material.Count,
                    out chosen,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float tolerance;
            string parts;
            IList<int> corners;
            if (!ComposedInput.TryFloat(
                    context,
                    ColorToleranceName,
                    operation,
                    new[] { MergeSame },
                    0f,
                    ComposedInput.NoCeiling,
                    out tolerance,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    PartsName,
                    operation,
                    new[] { DuplicateParts },
                    Parts,
                    out parts,
                    out code,
                    out message)
                || !TryFacesGiven(context, operation, out code, out message)
                || !ComposedInput.TryIndices(
                    context,
                    VertexIndicesName,
                    operation,
                    new[] { ExtractVertices },
                    model.Vertex.Count,
                    out corners,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXMaterial> picked = chosen.Select(at => model.Material[at]).ToList();
            switch (operation)
            {
                case ExtractVertices:
                    return Split(context, model, picked, corners);

                case ExtractFacesWithVertices:
                    return Extracted(context, model, picked, true);

                case Merge:
                    return Merged(model, new[] { picked });

                case MergeAll:
                    return Merged(model, new[] { model.Material.ToList() });

                case MergeSame:
                    return Merged(model, Alike(picked, tolerance));

                case ExtractFaces:
                    return Extracted(context, model, picked, false);

                case DuplicateParts:
                    return Duplicated(model, picked, parts);

                default:
                    return Clamped(picked);
            }
        }

        private static ComposedEditResult Merged(
            IPXPmx model, IEnumerable<IList<IPXMaterial>> groups)
        {
            Dictionary<IPXMaterial, IPXMaterial> moved =
                new Dictionary<IPXMaterial, IPXMaterial>(ReferenceComparer<IPXMaterial>.Instance);
            int changed = 0;
            foreach (IList<IPXMaterial> group in groups.Where(g => g.Count > 1))
            {
                IPXMaterial kept = group[0];
                foreach (IPXMaterial dropped in group.Skip(1))
                {
                    foreach (IPXFace face in dropped.Faces.ToList())
                    {
                        kept.Faces.Add(face);
                    }

                    model.Material.Remove(dropped);
                    moved[dropped] = kept;
                }

                changed++;
            }

            ReferenceCleanup.Repoint(model, moved);

            return Answer(changed, moved.Count, new int[0]);
        }

        private static IEnumerable<IList<IPXMaterial>> Alike(
            IList<IPXMaterial> picked, float tolerance)
        {
            List<IList<IPXMaterial>> groups = new List<IList<IPXMaterial>>();
            foreach (IPXMaterial material in picked)
            {
                IList<IPXMaterial> group = groups.FirstOrDefault(
                    g => Same(g[0], material, tolerance));
                if (group == null)
                {
                    groups.Add(new List<IPXMaterial> { material });
                }
                else
                {
                    group.Add(material);
                }
            }

            return groups;
        }

        private static bool Same(IPXMaterial left, IPXMaterial right, float tolerance)
        {
            return string.Equals(left.Tex, right.Tex, StringComparison.Ordinal)
                && string.Equals(left.Sphere, right.Sphere, StringComparison.Ordinal)
                && string.Equals(left.Toon, right.Toon, StringComparison.Ordinal)
                && left.SphereMode == right.SphereMode
                && left.BothDraw == right.BothDraw
                && left.Shadow == right.Shadow
                && left.SelfShadow == right.SelfShadow
                && left.SelfShadowMap == right.SelfShadowMap
                && left.Edge == right.Edge
                && left.VertexColor == right.VertexColor
                && left.PrimitiveType == right.PrimitiveType
                && Close(left.Diffuse, right.Diffuse, tolerance)
                && Close(left.EdgeColor, right.EdgeColor, tolerance)
                && Close(left.Specular, right.Specular, tolerance)
                && Close(left.Ambient, right.Ambient, tolerance)
                && Math.Abs(left.Power - right.Power) <= tolerance
                && Math.Abs(left.EdgeSize - right.EdgeSize) <= tolerance;
        }

        private static bool Close(V4 left, V4 right, float tolerance)
        {
            return Math.Abs(left.X - right.X) <= tolerance
                && Math.Abs(left.Y - right.Y) <= tolerance
                && Math.Abs(left.Z - right.Z) <= tolerance
                && Math.Abs(left.W - right.W) <= tolerance;
        }

        private static bool Close(V3 left, V3 right, float tolerance)
        {
            return Math.Abs(left.X - right.X) <= tolerance
                && Math.Abs(left.Y - right.Y) <= tolerance
                && Math.Abs(left.Z - right.Z) <= tolerance;
        }

        private static ComposedEditResult Split(
            McpMethodContext context,
            IPXPmx model,
            IList<IPXMaterial> picked,
            IList<int> corners)
        {
            if (!context.Params.ContainsKey(VertexIndicesName))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    VertexIndicesName + " に、移す面を作っている頂点の位置を渡す。");
            }

            HashSet<IPXVertex> chosen = new HashSet<IPXVertex>(
                corners.Select(at => model.Vertex[at]), ReferenceComparer<IPXVertex>.Instance);
            IPXMaterial made = null;
            int changed = 0;
            foreach (IPXMaterial material in picked)
            {
                IList<IPXFace> taken = material.Faces
                    .Where(face => ViewSelection.Corners(face).All(chosen.Contains))
                    .ToList();
                if (taken.Count == 0)
                {
                    continue;
                }

                made = made ?? Emptied(material);
                foreach (IPXFace face in taken)
                {
                    material.Faces.Remove(face);
                    made.Faces.Add(face);
                }

                changed++;
            }

            if (made == null)
            {
                return Answer(0, 0, new int[0]);
            }

            model.Material.Add(made);

            return Answer(changed, 0, new[] { model.Material.Count - 1 });
        }

        private static ComposedEditResult Extracted(
            McpMethodContext context, IPXPmx model, IList<IPXMaterial> picked, bool apart)
        {
            List<int> added = new List<int>();
            foreach (IPXMaterial material in picked)
            {
                IList<int> faces;
                string code;
                string message;
                if (!TargetInput.TryPositions(
                    context.Params,
                    FaceNames,
                    material.Faces.Count,
                    out faces,
                    out code,
                    out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                IList<IPXFace> taken = faces.Select(at => material.Faces[at]).ToList();
                if (apart)
                {
                    Apart(model, taken);
                }

                foreach (IPXFace face in taken)
                {
                    material.Faces.Remove(face);
                }

                IPXMaterial made = Emptied(material);
                foreach (IPXFace face in taken)
                {
                    made.Faces.Add(face);
                }

                model.Material.Add(made);
                added.Add(model.Material.Count - 1);
            }

            return Answer(picked.Count, 0, added);
        }

        private static ComposedEditResult Duplicated(
            IPXPmx model, IList<IPXMaterial> picked, string parts)
        {
            List<int> added = new List<int>();
            foreach (IPXMaterial material in picked)
            {
                IPXMaterial made = (IPXMaterial)material.Clone();
                if (!string.Equals(parts, FacesOnly, StringComparison.Ordinal))
                {
                    Split(model, made, string.Equals(parts, WithMorphs, StringComparison.Ordinal));
                }

                model.Material.Add(made);
                added.Add(model.Material.Count - 1);
            }

            return Answer(picked.Count, 0, added);
        }

        private static void Split(IPXPmx model, IPXMaterial made, bool morphs)
        {
            Dictionary<IPXVertex, IPXVertex> apart =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXFace face in made.Faces)
            {
                foreach (IPXVertex corner in
                    new[] { face.Vertex1, face.Vertex2, face.Vertex3 })
                {
                    if (corner == null || apart.ContainsKey(corner))
                    {
                        continue;
                    }

                    IPXVertex copy = (IPXVertex)corner.Clone();
                    model.Vertex.Add(copy);
                    apart[corner] = copy;
                }
            }

            foreach (IPXFace face in made.Faces)
            {
                face.Vertex1 = apart[face.Vertex1];
                face.Vertex2 = apart[face.Vertex2];
                face.Vertex3 = apart[face.Vertex3];
            }

            if (!morphs)
            {
                return;
            }

            foreach (IPXMorph morph in model.Morph.ToList())
            {
                if (!morph.Offsets.Any(o => Touches(o, apart)))
                {
                    continue;
                }

                IPXMorph copy = (IPXMorph)morph.Clone();
                foreach (IPXMorphOffset offset in copy.Offsets)
                {
                    Follow(offset, apart);
                }

                model.Morph.Add(copy);
            }
        }

        private static bool Touches(
            IPXMorphOffset offset, IDictionary<IPXVertex, IPXVertex> apart)
        {
            IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
            if (shifted != null)
            {
                return shifted.Vertex != null && apart.ContainsKey(shifted.Vertex);
            }

            IPXUVMorphOffset slid = offset as IPXUVMorphOffset;

            return slid != null && slid.Vertex != null && apart.ContainsKey(slid.Vertex);
        }

        private static void Follow(
            IPXMorphOffset offset, IDictionary<IPXVertex, IPXVertex> apart)
        {
            IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
            if (shifted != null && shifted.Vertex != null && apart.ContainsKey(shifted.Vertex))
            {
                shifted.Vertex = apart[shifted.Vertex];
            }

            IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
            if (slid != null && slid.Vertex != null && apart.ContainsKey(slid.Vertex))
            {
                slid.Vertex = apart[slid.Vertex];
            }
        }

        private static IPXMaterial Emptied(IPXMaterial material)
        {
            // 面を作る口はSDKの並びに無いので、元を写して面を空にする。
            IPXMaterial made = (IPXMaterial)material.Clone();
            made.Faces.Clear();

            return made;
        }

        private static ComposedEditResult Clamped(IList<IPXMaterial> picked)
        {
            foreach (IPXMaterial material in picked)
            {
                material.Diffuse = Held(material.Diffuse);
                material.EdgeColor = Held(material.EdgeColor);
                material.Specular = Held(material.Specular);
                material.Ambient = Held(material.Ambient);
            }

            return Answer(picked.Count, 0, new int[0]);
        }

        private static V4 Held(V4 given)
        {
            return new V4(Held(given.X), Held(given.Y), Held(given.Z), Held(given.W));
        }

        private static V3 Held(V3 given)
        {
            return new V3(Held(given.X), Held(given.Y), Held(given.Z));
        }

        private static float Held(float given)
        {
            return Math.Min(1f, Math.Max(0f, given));
        }

        private static ComposedEditResult Answer(int changed, int removed, IList<int> added)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                    { RemovedName, removed },
                    { AddedName, added.Cast<object>().ToArray() },
                });
        }

        private static void Apart(IPXPmx model, IList<IPXFace> taken)
        {
            HashSet<IPXFace> going = new HashSet<IPXFace>(taken, ReferenceComparer<IPXFace>.Instance);
            HashSet<IPXVertex> shared = new HashSet<IPXVertex>(
                model.Material
                    .SelectMany(material => material.Faces)
                    .Where(face => !going.Contains(face))
                    .SelectMany(ViewSelection.Corners),
                ReferenceComparer<IPXVertex>.Instance);
            Dictionary<IPXVertex, IPXVertex> apart =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXFace face in taken)
            {
                face.Vertex1 = Copied(model, apart, shared, face.Vertex1);
                face.Vertex2 = Copied(model, apart, shared, face.Vertex2);
                face.Vertex3 = Copied(model, apart, shared, face.Vertex3);
            }
        }

        private static IPXVertex Copied(
            IPXPmx model,
            IDictionary<IPXVertex, IPXVertex> apart,
            ICollection<IPXVertex> shared,
            IPXVertex vertex)
        {
            if (vertex == null || !shared.Contains(vertex))
            {
                return vertex;
            }

            IPXVertex made;
            if (!apart.TryGetValue(vertex, out made))
            {
                made = (IPXVertex)vertex.Clone();
                model.Vertex.Add(made);
                apart[vertex] = made;
            }

            return made;
        }

        private static bool TryFacesGiven(
            McpMethodContext context, string operation, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            bool pointed = context.Params.ContainsKey(FaceIndicesName)
                || context.Params.ContainsKey(FaceRangeName)
                || context.Params.ContainsKey(FaceAllName);
            if (Extracting.Contains(operation, StringComparer.Ordinal) || !pointed)
            {
                code = null;

                return true;
            }

            message = FaceIndicesName + "・" + FaceRangeName + "・" + FaceAllName
                + " を渡せるのは " + string.Join("・", Extracting.ToArray()) + " のときだけである。";

            return false;
        }
    }
}
