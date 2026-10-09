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
        public const string ToolName = "model_edit_materials";

        public const string Merge = "merge";

        public const string MergeAll = "mergeAll";

        public const string MergeSame = "mergeSame";

        public const string ExtractFaces = "extractFaces";

        public const string ExtractFacesWithVertices = "extractFacesWithVertices";

        public const string ExtractVertices = "extractVertices";

        public const string VertexIndicesName = "vertexIndices";

        public const string VertexSelectedName = "vertexSelected";

        public const string DuplicateParts = "duplicateParts";

        public const string ClampColor = "clampColor";

        public const string ColorToleranceName = "colorTolerance";

        public const string PartsName = "parts";

        public const string FacesOnly = "facesOnly";

        public const string WithVertices = "withVertices";

        public const string WithMorphs = "withMorphs";

        public const string FaceIndicesName = "faceIndices";

        public const string FaceRangeName = "faceRange";

        public const string FaceAllName = "faceAll";

        public const string ChangedName = "changed";

        public const string RemovedName = "removed";

        public const string AddedName = "added";

        public const string FaceModelIndicesName = "faceModelIndices";

        private const string FaceHandlesName = "faceHandles";

        private static readonly TargetNames FaceNames =
            new TargetNames(FaceIndicesName, FaceRangeName, FaceAllName, FaceHandlesName);

        private static readonly TargetNames NumberedFaceNames = new TargetNames(
            FaceIndicesName,
            FaceRangeName,
            FaceAllName,
            FaceHandlesName,
            null,
            FaceModelIndicesName);

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
                TargetNames.Element.Selected,
                ColorToleranceName,
                PartsName,
                FaceIndicesName,
                FaceRangeName,
                FaceAllName,
                FaceModelIndicesName,
                VertexIndicesName,
                VertexSelectedName,
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
            IList<IList<int>> faceSets = null;
            if (!ComposedOperation.TryTake(
                context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool numbered = context.Params.ContainsKey(FaceModelIndicesName);
            if (numbered
                ? !TryNumberedFaces(
                    context, model, operation, out chosen, out faceSets, out code, out message)
                : !TargetInput.TryPositions(
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
                    out message,
                    null,
                    VertexSelectedName,
                    context.Screen.Pick(ElementKinds.Vertex, model.Vertex.Count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXMaterial> picked = chosen.Select(at => model.Material[at]).ToList();
            switch (operation)
            {
                case ExtractVertices:
                    return Split(context, model, picked, corners);

                case ExtractFacesWithVertices:
                    return Extracted(context, model, picked, true, faceSets);

                case Merge:
                    return Merged(model, new[] { picked });

                case MergeAll:
                    return Merged(model, new[] { model.Material.ToList() });

                case MergeSame:
                    return Merged(model, Alike(picked, tolerance));

                case ExtractFaces:
                    return Extracted(context, model, picked, false, faceSets);

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
            if (!context.Params.ContainsKey(VertexIndicesName)
                && !context.Params.ContainsKey(VertexSelectedName))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    VertexIndicesName + " に、移す面を作っている頂点の位置を渡す。"
                        + VertexSelectedName + " に真を渡すと、画面の選択で指す。");
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
            McpMethodContext context,
            IPXPmx model,
            IList<IPXMaterial> picked,
            bool apart,
            IList<IList<int>> faceSets)
        {
            List<int> added = new List<int>();
            for (int each = 0; each < picked.Count; each++)
            {
                IPXMaterial material = picked[each];
                IList<int> faces;
                string code;
                string message;
                if (faceSets != null)
                {
                    faces = faceSets[each];
                }
                else if (!TargetInput.TryPositions(
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
            IPXMaterial made = (IPXMaterial)material.Clone();
            made.Faces.Clear();

            return made;
        }

        private static ComposedEditResult Clamped(IList<IPXMaterial> picked)
        {
            int changed = 0;
            foreach (IPXMaterial material in picked)
            {
                V4 diffuse = Held(material.Diffuse);
                V4 edge = Held(material.EdgeColor);
                V3 specular = Held(material.Specular);
                V3 ambient = Held(material.Ambient);
                if (Same(diffuse, material.Diffuse) && Same(edge, material.EdgeColor)
                    && Vectors.Same(specular, material.Specular) && Vectors.Same(ambient, material.Ambient))
                {
                    continue;
                }

                material.Diffuse = diffuse;
                material.EdgeColor = edge;
                material.Specular = specular;
                material.Ambient = ambient;
                changed++;
            }

            return Answer(changed, 0, new int[0]);
        }

        private static bool Same(V4 left, V4 right)
        {
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;
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

        private static bool TryNumberedFaces(
            McpMethodContext context,
            IPXPmx model,
            string operation,
            out IList<int> materials,
            out IList<IList<int>> faceSets,
            out string code,
            out string message)
        {
            materials = null;
            faceSets = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            if (!Extracting.Contains(operation, StringComparer.Ordinal))
            {
                message = FaceModelIndicesName + " を渡せるのは "
                    + string.Join("・", Extracting.ToArray()) + " のときだけである。";

                return false;
            }

            string[] pointing =
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
            };
            if (pointing.Any(context.Params.ContainsKey))
            {
                message = FaceModelIndicesName
                    + " は材質の指し方と一緒に渡せない。面を持つ材質は面の番号から決まる。";

                return false;
            }

            TargetRequest request;
            ResolvedTargets resolved;
            IList<int> counts = model.Material.Select(m => m.Faces.Count).ToList();
            if (!TargetInput.TryTake(
                    context.Params, NumberedFaceNames, false, out request, out code, out message)
                || !TargetSelection.TryResolve(
                    request,
                    TargetForm.Numbered,
                    counts.Sum(),
                    id => false,
                    out resolved,
                    out code,
                    out message,
                    NumberedFaceNames))
            {
                return false;
            }

            List<int> owning = new List<int>();
            List<IList<int>> sets = new List<IList<int>>();
            int start = 0;
            for (int at = 0; at < counts.Count; at++)
            {
                int first = start;
                int end = start + counts[at];
                List<int> inside = resolved.Indices
                    .Where(number => number >= first && number < end)
                    .Select(number => number - first)
                    .OrderBy(number => number)
                    .ToList();
                if (inside.Count != 0)
                {
                    owning.Add(at);
                    sets.Add(inside);
                }

                start = end;
            }

            materials = owning;
            faceSets = sets;

            return true;
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
