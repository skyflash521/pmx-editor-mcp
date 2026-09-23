using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指したモーフの結合・まとめ・分離と、いまの材質の値からの作成を行うツール。
    /// </summary>
    public static class ModelEditMorphs
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_morphs";

        /// <summary>同じ名前のモーフを先頭の1つへまとめる。</summary>
        public const string MergeSameName = "mergeSameName";

        /// <summary>同じ種類のモーフを先頭の1つへまとめる。</summary>
        public const string MergeSameKind = "mergeSameKind";

        /// <summary>同じ種類のモーフを、同じ相手へのオフセットを足しながら1つへまとめる。</summary>
        public const string MergeSameKindAdd = "mergeSameKindAdd";

        /// <summary>指したモーフを呼ぶグループモーフを1つ足す。</summary>
        public const string GroupInto = "groupInto";

        /// <summary>指したモーフを切り替えるフリップモーフを1つ足す。</summary>
        public const string FlipInto = "flipInto";

        /// <summary>頂点モーフを、動く頂点のまとまりごとの別々のモーフへ分ける。</summary>
        public const string SplitVertices = "splitVertices";

        /// <summary>いまの材質の値を写した材質モーフを1つ足す。</summary>
        public const string MaterialFromCurrent = "materialFromCurrent";

        /// <summary>指したモーフへ、その種類が指す相手を指すオフセットを足す。</summary>
        public const string AddOffsets = "addOffsets";

        /// <summary>指した頂点を動かす頂点モーフを1つ足す。</summary>
        public const string VertexMorphFromVertices = "vertexMorphFromVertices";

        /// <summary>足すオフセットが指す相手の位置を受け取る入力の名前。</summary>
        public const string TargetIndicesName = "targetIndices";

        /// <summary>オフセットが指す相手を、画面の選択で指す入力の名前。</summary>
        public const string TargetSelectedName = "targetSelected";

        /// <summary>足したモーフの名前を受け取る入力の名前。</summary>
        public const string NameName = "name";

        /// <summary>足したモーフの位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えたモーフの数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えたモーフの数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>モーフを指さない操作。</summary>
        private static IList<string> Whole
        {
            get { return new[] { MaterialFromCurrent, VertexMorphFromVertices }; }
        }

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    MergeSameName,
                    MergeSameKind,
                    MergeSameKindAdd,
                    GroupInto,
                    FlipInto,
                    SplitVertices,
                    MaterialFromCurrent,
                    AddOffsets,
                    VertexMorphFromVertices,
                };
            }
        }

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                NameName,
                TargetIndicesName,
                TargetSelectedName,
            };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TryChosen(context, model, operation, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string name;
            IList<int> targets;
            if (!ComposedInput.TryText(
                    context,
                    NameName,
                    operation,
                    new[] { GroupInto, FlipInto, MaterialFromCurrent, VertexMorphFromVertices },
                    out name,
                    out code,
                    out message)
                || !ComposedInput.TryIndices(
                    context,
                    TargetIndicesName,
                    operation,
                    new[] { AddOffsets, VertexMorphFromVertices },
                    Reach(model, operation, chosen),
                    out targets,
                    out code,
                    out message,
                    null,
                    TargetSelectedName,
                    context.Screen.Pick(
                        Aimed(model, operation, chosen), Reach(model, operation, chosen))))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXMorph> picked = chosen.Select(at => model.Morph[at]).ToList();
            switch (operation)
            {
                case MergeSameName:
                    return Merged(
                        model, picked, morph => morph.Name + "/" + morph.Kind, false);

                case MergeSameKind:
                    return Merged(model, picked, morph => morph.Kind.ToString(), false);

                case MergeSameKindAdd:
                    return Merged(model, picked, morph => morph.Kind.ToString(), true);

                case GroupInto:
                    return Calling(model, builder, picked, name, MorphKind.Group);

                case FlipInto:
                    return Calling(model, builder, picked, name, MorphKind.Flip);

                case SplitVertices:
                    return Split(model, builder, picked);

                case AddOffsets:
                    return Filled(model, builder, picked, targets);

                case VertexMorphFromVertices:
                    return FromVertices(model, builder, name, targets);

                default:
                    return FromMaterials(model, builder, name);
            }
        }

        /// <summary>
        /// 指す相手を並べる先の数。オフセットを足す操作では、指したモーフのうち先頭の種類が指す並びの
        /// 数、頂点からモーフを作る操作では頂点の数である。ほかの操作では0でよい。
        /// </summary>
        private static int Reach(IPXPmx model, string operation, IList<int> chosen)
        {
            if (string.Equals(operation, VertexMorphFromVertices, StringComparison.Ordinal))
            {
                return model.Vertex.Count;
            }

            if (!string.Equals(operation, AddOffsets, StringComparison.Ordinal) || chosen.Count == 0)
            {
                return 0;
            }

            return Aimed(model, model.Morph[chosen[0]].Kind).Count;
        }

        /// <summary>その操作が指す相手の種類。相手を指さない操作では null。</summary>
        private static string Aimed(IPXPmx model, string operation, IList<int> chosen)
        {
            if (string.Equals(operation, VertexMorphFromVertices, StringComparison.Ordinal))
            {
                return ElementKinds.Vertex;
            }

            if (!string.Equals(operation, AddOffsets, StringComparison.Ordinal) || chosen.Count == 0)
            {
                return null;
            }

            switch (model.Morph[chosen[0]].Kind)
            {
                case MorphKind.Vertex:
                case MorphKind.UV:
                case MorphKind.UVA1:
                case MorphKind.UVA2:
                case MorphKind.UVA3:
                case MorphKind.UVA4:
                    return ElementKinds.Vertex;

                case MorphKind.Bone:
                    return ElementKinds.Bone;

                case MorphKind.Material:
                    return ElementKinds.Material;

                case MorphKind.Impulse:
                    return ElementKinds.Body;

                default:
                    return null;
            }
        }

        /// <summary>その種類のモーフのオフセットが指す相手の並び。指す相手を持たない種類では空。</summary>
        private static IList<object> Aimed(IPXPmx model, MorphKind kind)
        {
            switch (kind)
            {
                case MorphKind.Vertex:
                case MorphKind.UV:
                case MorphKind.UVA1:
                case MorphKind.UVA2:
                case MorphKind.UVA3:
                case MorphKind.UVA4:
                    return model.Vertex.Cast<object>().ToList();

                case MorphKind.Bone:
                    return model.Bone.Cast<object>().ToList();

                case MorphKind.Material:
                    return model.Material.Cast<object>().ToList();

                case MorphKind.Group:
                case MorphKind.Flip:
                    return model.Morph.Cast<object>().ToList();

                case MorphKind.Impulse:
                    return model.Body.Cast<object>().ToList();

                default:
                    return new object[0];
            }
        }

        /// <summary>
        /// 指したモーフへ、指した相手を指すオフセットを1つずつ足す。値はどれも動かさない値にする。
        /// 指したモーフの種類がそろっていなければ断る。
        /// </summary>
        private static ComposedEditResult Filled(
            IPXPmx model, Func<object> builder, IList<IPXMorph> picked, IList<int> targets)
        {
            if (picked.Count == 0)
            {
                return Answer(new int[0], 0, 0);
            }

            MorphKind kind = picked[0].Kind;
            if (picked.Any(morph => morph.Kind != kind))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    "指したモーフの種類がそろっていないので、足すオフセットの形が決まらない。");
            }

            IList<object> aimed = Aimed(model, kind);
            if (aimed.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    kind + " のモーフのオフセットは、指す相手を持たない。");
            }

            int changed = 0;
            foreach (IPXMorph morph in picked)
            {
                foreach (int at in targets)
                {
                    morph.Offsets.Add(Offset((IPXPmxBuilder)builder(), kind, aimed[at]));
                }

                changed += targets.Count > 0 ? 1 : 0;
            }

            return Answer(new int[0], changed, 0);
        }

        /// <summary>指した頂点を指す頂点モーフを1つ足す。値はどれも動かさない値にする。</summary>
        private static ComposedEditResult FromVertices(
            IPXPmx model, Func<object> builder, string name, IList<int> targets)
        {
            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            IPXMorph morph = made.Morph();
            morph.Name = name;
            morph.NameE = string.Empty;
            morph.Kind = MorphKind.Vertex;
            foreach (int at in targets)
            {
                morph.Offsets.Add(Offset(made, MorphKind.Vertex, model.Vertex[at]));
            }

            model.Morph.Add(morph);

            return Answer(new[] { model.Morph.Count - 1 }, 0, 0);
        }

        /// <summary>その相手を指す、値が動かないオフセット。</summary>
        private static IPXMorphOffset Offset(IPXPmxBuilder builder, MorphKind kind, object aimed)
        {
            switch (kind)
            {
                case MorphKind.Bone:
                    IPXBoneMorphOffset posed = builder.BoneMorphOffset();
                    posed.Bone = (IPXBone)aimed;
                    posed.Translation = new V3(0f, 0f, 0f);
                    posed.Rotation = new Q(0f, 0f, 0f, 1f);

                    return posed;

                case MorphKind.Material:
                    IPXMaterialMorphOffset painted = builder.MaterialMorphOffset();
                    painted.Material = (IPXMaterial)aimed;
                    // PMXの材質モーフは、操作形式が0なら値を掛け、1なら足す。
                    painted.Op = Adding;
                    painted.Diffuse = new V4(0f, 0f, 0f, 0f);
                    painted.Specular = new V3(0f, 0f, 0f);
                    painted.Power = 0f;
                    painted.Ambient = new V3(0f, 0f, 0f);
                    painted.EdgeColor = new V4(0f, 0f, 0f, 0f);
                    painted.EdgeSize = 0f;
                    painted.Tex = new V4(0f, 0f, 0f, 0f);
                    painted.Sphere = new V4(0f, 0f, 0f, 0f);
                    painted.Toon = new V4(0f, 0f, 0f, 0f);

                    return painted;

                case MorphKind.Group:
                case MorphKind.Flip:
                    IPXGroupMorphOffset called = builder.GroupMorphOffset();
                    called.Morph = (IPXMorph)aimed;
                    called.Ratio = 0f;

                    return called;

                case MorphKind.Impulse:
                    IPXImpulseMorphOffset pushed = builder.ImpulseMorphOffset();
                    pushed.Body = (IPXBody)aimed;
                    pushed.Velocity = new V3(0f, 0f, 0f);
                    pushed.Torque = new V3(0f, 0f, 0f);

                    return pushed;

                case MorphKind.Vertex:
                    IPXVertexMorphOffset moved = builder.VertexMorphOffset();
                    moved.Vertex = (IPXVertex)aimed;
                    moved.Offset = new V3(0f, 0f, 0f);

                    return moved;

                default:
                    IPXUVMorphOffset slid = builder.UVMorphOffset();
                    slid.Vertex = (IPXVertex)aimed;
                    slid.Offset = new V4(0f, 0f, 0f, 0f);

                    return slid;
            }
        }

        private const int Adding = 1;

        private static ComposedEditResult Merged(
            IPXPmx model, IList<IPXMorph> picked, Func<IPXMorph, string> key, bool adding)
        {
            Dictionary<IPXMorph, IPXMorph> moved =
                new Dictionary<IPXMorph, IPXMorph>(ReferenceComparer<IPXMorph>.Instance);
            int changed = 0;
            foreach (IGrouping<string, IPXMorph> group in picked
                .GroupBy(key, StringComparer.Ordinal)
                .Where(g => g.Count() > 1))
            {
                IPXMorph kept = group.First();
                Dictionary<object, IPXMorphOffset> toward = Towards(kept);
                foreach (IPXMorph dropped in group.Skip(1))
                {
                    foreach (IPXMorphOffset offset in dropped.Offsets.ToList())
                    {
                        Take(kept, toward, offset, adding);
                    }

                    model.Morph.Remove(dropped);
                    moved[dropped] = kept;
                }

                changed++;
            }

            ReferenceCleanup.Repoint(model, moved);

            return Answer(new int[0], changed, moved.Count);
        }

        /// <summary>そのモーフのオフセットを、指す相手ごとに最初の1つで引く表。</summary>
        private static Dictionary<object, IPXMorphOffset> Towards(IPXMorph morph)
        {
            Dictionary<object, IPXMorphOffset> toward =
                new Dictionary<object, IPXMorphOffset>(ReferenceComparer<object>.Instance);
            foreach (IPXMorphOffset offset in morph.Offsets)
            {
                Remember(toward, offset);
            }

            return toward;
        }

        private static void Remember(Dictionary<object, IPXMorphOffset> toward, IPXMorphOffset offset)
        {
            object target = Toward(offset);
            if (target != null && !toward.ContainsKey(target))
            {
                toward.Add(target, offset);
            }
        }

        /// <summary>
        /// <paramref name="offset"/> を <paramref name="kept"/> へ移す。<paramref name="toward"/> は
        /// <paramref name="kept"/> のオフセットを指す相手ごとに引く表で、移したぶんもここへ足す。
        /// </summary>
        private static void Take(
            IPXMorph kept,
            Dictionary<object, IPXMorphOffset> toward,
            IPXMorphOffset offset,
            bool adding)
        {
            if (!adding)
            {
                kept.Offsets.Add(offset);
                Remember(toward, offset);

                return;
            }

            object target = Toward(offset);
            IPXMorphOffset held;
            if (target == null || !toward.TryGetValue(target, out held))
            {
                kept.Offsets.Add(offset);
                Remember(toward, offset);

                return;
            }

            IPXVertexMorphOffset shifted = held as IPXVertexMorphOffset;
            if (shifted != null)
            {
                shifted.Offset = Vectors.Add(
                    shifted.Offset, ((IPXVertexMorphOffset)offset).Offset);

                return;
            }

            IPXUVMorphOffset slid = held as IPXUVMorphOffset;
            if (slid != null)
            {
                V4 more = ((IPXUVMorphOffset)offset).Offset;
                slid.Offset = new V4(
                    slid.Offset.X + more.X,
                    slid.Offset.Y + more.Y,
                    slid.Offset.Z + more.Z,
                    slid.Offset.W + more.W);

                return;
            }

            IPXGroupMorphOffset grouped = held as IPXGroupMorphOffset;
            if (grouped != null)
            {
                grouped.Ratio += ((IPXGroupMorphOffset)offset).Ratio;

                return;
            }

            kept.Offsets.Add(offset);
        }

        /// <summary>そのオフセットが指している相手。指す相手を持たない種類では空を返す。</summary>
        private static object Toward(IPXMorphOffset offset)
        {
            IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
            if (shifted != null)
            {
                return shifted.Vertex;
            }

            IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
            if (slid != null)
            {
                return slid.Vertex;
            }

            IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
            if (posed != null)
            {
                return posed.Bone;
            }

            IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;
            if (painted != null)
            {
                return painted.Material;
            }

            IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;

            return grouped == null ? null : (object)grouped.Morph;
        }

        private static ComposedEditResult Calling(
            IPXPmx model,
            Func<object> builder,
            IList<IPXMorph> picked,
            string name,
            MorphKind kind)
        {
            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            IPXMorph morph = made.Morph();
            morph.Name = name;
            morph.NameE = string.Empty;
            morph.Kind = kind;
            foreach (IPXMorph held in picked)
            {
                morph.Offsets.Add(made.GroupMorphOffset(held, 1f));
            }

            model.Morph.Add(morph);

            return Answer(new[] { model.Morph.Count - 1 }, 0, 0);
        }

        /// <summary>
        /// 頂点モーフを、面で繋がった頂点のまとまりごとの別々のモーフへ分ける。元のモーフは並びから
        /// 外す。
        /// </summary>
        private static ComposedEditResult Split(
            IPXPmx model, Func<object> builder, IList<IPXMorph> picked)
        {
            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            IDictionary<IPXVertex, int> islands = Islands(model);
            Dictionary<IPXMorph, IPXMorph> moved =
                new Dictionary<IPXMorph, IPXMorph>(ReferenceComparer<IPXMorph>.Instance);
            List<IPXMorph> split = new List<IPXMorph>();
            foreach (IPXMorph morph in picked.Where(m => m.Kind == MorphKind.Vertex))
            {
                IList<IPXMorph> parts = Parted(model, made, morph, islands);
                if (parts.Count == 0)
                {
                    continue;
                }

                model.Morph.Remove(morph);
                moved[morph] = parts[0];
                Spread(model, made, morph, parts);
                split.AddRange(parts);
            }

            ReferenceCleanup.Repoint(model, moved);
            Dictionary<IPXMorph, int> at = new Dictionary<IPXMorph, int>(ReferenceComparer<IPXMorph>.Instance);
            for (int index = 0; index < model.Morph.Count; index++)
            {
                if (!at.ContainsKey(model.Morph[index]))
                {
                    at.Add(model.Morph[index], index);
                }
            }

            return Answer(split.Select(part => at[part]).ToList(), 0, moved.Count);
        }

        private static void Spread(
            IPXPmx model, IPXPmxBuilder builder, IPXMorph morph, IList<IPXMorph> parts)
        {
            foreach (IPXMorph held in model.Morph)
            {
                IList<IPXMorphOffset> made = new List<IPXMorphOffset>();
                foreach (IPXGroupMorphOffset grouped in held.Offsets
                    .OfType<IPXGroupMorphOffset>()
                    .Where(offset => ReferenceEquals(offset.Morph, morph))
                    .ToList())
                {
                    for (int at = 1; at < parts.Count; at++)
                    {
                        made.Add(builder.GroupMorphOffset(parts[at], grouped.Ratio));
                    }
                }

                foreach (IPXMorphOffset offset in made)
                {
                    held.Offsets.Add(offset);
                }
            }

            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                IList<IPXNodeItem> made = new List<IPXNodeItem>();
                foreach (IPXMorphNodeItem item in node.Items
                    .OfType<IPXMorphNodeItem>()
                    .Where(item => ReferenceEquals(item.Morph, morph))
                    .ToList())
                {
                    for (int at = 1; at < parts.Count; at++)
                    {
                        made.Add(builder.MorphNodeItem(parts[at]));
                    }
                }

                foreach (IPXNodeItem item in made)
                {
                    node.Items.Add(item);
                }
            }
        }

        /// <summary>いまの材質から作る操作では、選択の指定を受け取らない。</summary>
        private static bool TryChosen(
            McpMethodContext context,
            IPXPmx model,
            string operation,
            out IList<int> chosen,
            out string code,
            out string message)
        {
            if (!Whole.Contains(operation, StringComparer.Ordinal))
            {
                return TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Morph.Count,
                    out chosen,
                    out code,
                    out message);
            }

            chosen = new int[0];

            return TargetInput.TryNoTarget(
                context.Params,
                TargetNames.Element,
                Operations.Where(held => !Whole.Contains(held, StringComparer.Ordinal))
                    .ToList(),
                out code,
                out message);
        }

        private static IList<IPXMorph> Parted(
            IPXPmx model,
            IPXPmxBuilder builder,
            IPXMorph morph,
            IDictionary<IPXVertex, int> islands)
        {
            Dictionary<int, IPXMorph> parts = new Dictionary<int, IPXMorph>();
            List<IPXMorph> order = new List<IPXMorph>();
            foreach (IPXMorphOffset offset in morph.Offsets)
            {
                IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
                if (shifted == null || shifted.Vertex == null)
                {
                    continue;
                }

                int island;
                if (!islands.TryGetValue(shifted.Vertex, out island))
                {
                    continue;
                }

                IPXMorph part;
                if (!parts.TryGetValue(island, out part))
                {
                    part = builder.Morph();
                    part.Name = morph.Name + (parts.Count + 1);
                    part.NameE = string.Empty;
                    part.Kind = MorphKind.Vertex;
                    part.Panel = morph.Panel;
                    parts[island] = part;
                    order.Add(part);
                    model.Morph.Add(part);
                }

                part.Offsets.Add(offset);
            }

            return order;
        }

        private static IDictionary<IPXVertex, int> Islands(IPXPmx model)
        {
            Dictionary<IPXVertex, int> islands =
                new Dictionary<IPXVertex, int>(ReferenceComparer<IPXVertex>.Instance);
            int next = 0;
            foreach (IPXVertex vertex in model.Vertex)
            {
                islands[vertex] = next++;
            }

            int[] parent = Enumerable.Range(0, next).ToArray();
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (ReferenceCleanup.IsSoundFace(face))
                    {
                        int first = islands[face.Vertex1];
                        Join(parent, first, islands[face.Vertex2]);
                        Join(parent, first, islands[face.Vertex3]);
                    }
                }
            }

            foreach (IPXVertex vertex in islands.Keys.ToList())
            {
                islands[vertex] = Root(parent, islands[vertex]);
            }

            return islands;
        }

        /// <summary>2つのまとまりをつなぐ。つないだまとまりの代表は、小さい方の位置になる。</summary>
        private static void Join(int[] parent, int left, int right)
        {
            int one = Root(parent, left);
            int other = Root(parent, right);
            if (one < other)
            {
                parent[other] = one;
            }
            else if (other < one)
            {
                parent[one] = other;
            }
        }

        /// <summary>その位置のまとまりの代表。まとまりの中で最も小さい位置である。</summary>
        private static int Root(int[] parent, int at)
        {
            int root = at;
            while (parent[root] != root)
            {
                root = parent[root];
            }

            while (parent[at] != root)
            {
                int up = parent[at];
                parent[at] = root;
                at = up;
            }

            return root;
        }

        private static ComposedEditResult FromMaterials(
            IPXPmx model, Func<object> builder, string name)
        {
            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            IPXMorph morph = made.Morph();
            morph.Name = name;
            morph.NameE = string.Empty;
            morph.Kind = MorphKind.Material;
            foreach (IPXMaterial material in model.Material)
            {
                IPXMaterialMorphOffset offset = made.MaterialMorphOffset();
                offset.Material = material;
                offset.Diffuse = new V4(
                    material.Diffuse.X, material.Diffuse.Y, material.Diffuse.Z, material.Diffuse.W);
                offset.Specular = new V3(
                    material.Specular.X, material.Specular.Y, material.Specular.Z);
                offset.Ambient = new V3(
                    material.Ambient.X, material.Ambient.Y, material.Ambient.Z);
                offset.EdgeColor = new V4(
                    material.EdgeColor.X,
                    material.EdgeColor.Y,
                    material.EdgeColor.Z,
                    material.EdgeColor.W);
                offset.EdgeSize = material.EdgeSize;
                offset.Power = material.Power;
                morph.Offsets.Add(offset);
            }

            model.Morph.Add(morph);

            return Answer(new[] { model.Morph.Count - 1 }, 0, 0);
        }

        private static ComposedEditResult Answer(IList<int> added, int changed, int removed)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedName, added.Cast<object>().ToArray() },
                    { ChangedName, changed },
                    { RemovedName, removed },
                });
        }
    }
}
