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

        /// <summary>足したモーフの名前を受け取る入力の名前。</summary>
        public const string NameName = "name";

        /// <summary>足したモーフの位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えたモーフの数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えたモーフの数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

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
            if (!ComposedInput.TryText(
                    context,
                    NameName,
                    operation,
                    new[] { GroupInto, FlipInto, MaterialFromCurrent },
                    out name,
                    out code,
                    out message))
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

                default:
                    return FromMaterials(model, builder, name);
            }
        }

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
                foreach (IPXMorph dropped in group.Skip(1))
                {
                    foreach (IPXMorphOffset offset in dropped.Offsets.ToList())
                    {
                        Take(kept, offset, adding);
                    }

                    model.Morph.Remove(dropped);
                    moved[dropped] = kept;
                }

                changed++;
            }

            ReferenceCleanup.Repoint(model, moved);

            return Answer(new int[0], changed, moved.Count);
        }

        private static void Take(IPXMorph kept, IPXMorphOffset offset, bool adding)
        {
            if (!adding)
            {
                kept.Offsets.Add(offset);

                return;
            }

            IPXMorphOffset held = kept.Offsets.FirstOrDefault(
                item => ReferenceEquals(Toward(item), Toward(offset)) && Toward(offset) != null);
            if (held == null)
            {
                kept.Offsets.Add(offset);

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
            List<int> added = new List<int>();
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
                foreach (IPXMorph part in parts)
                {
                    added.Add(model.Morph.IndexOf(part));
                }
            }

            ReferenceCleanup.Repoint(model, moved);

            return Answer(added, 0, moved.Count);
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
            if (!string.Equals(operation, MaterialFromCurrent, StringComparison.Ordinal))
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
                Operations.Where(
                    held => !string.Equals(held, MaterialFromCurrent, StringComparison.Ordinal))
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

            for (bool joined = true; joined;)
            {
                joined = false;
                foreach (IPXMaterial material in model.Material)
                {
                    foreach (IPXFace face in material.Faces)
                    {
                        if (ReferenceCleanup.IsSoundFace(face))
                        {
                            joined |= Joined(islands, face);
                        }
                    }
                }
            }

            return islands;
        }

        /// <summary>寄せたなら真を返す。</summary>
        private static bool Joined(IDictionary<IPXVertex, int> islands, IPXFace face)
        {
            IPXVertex[] corners = { face.Vertex1, face.Vertex2, face.Vertex3 };
            int least = corners.Min(corner => islands[corner]);
            bool joined = false;
            foreach (IPXVertex corner in corners)
            {
                joined |= islands[corner] != least;
                islands[corner] = least;
            }

            return joined;
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
