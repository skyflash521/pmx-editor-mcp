// ComposedEdit に載る合成ツール。
using System;
using System.Collections.Generic;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelMorphFromMoved
    {
        public const string ToolName = "model_morph_from_moved";

        public const string BasePmxHandleName = "basePmxHandle";

        public const string NameName = "name";

        public const string AddedName = "added";

        public const string OffsetsName = "offsets";

        /// <param name="builder">新しい要素を作る相手を返す。</param>
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

            List<string> known = new List<string> { BasePmxHandleName, NameName };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            IPXPmx model = (IPXPmx)pmx;
            IPXPmx based;
            string code;
            string message;
            if (!TryBase(context, out based, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string differs = Differs(based, model);
            if (differs != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
            }

            object given;
            string name = context.Params.TryGetValue(NameName, out given) ? given as string : null;
            if (string.IsNullOrEmpty(name))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, NameName + " に足すモーフの名前を渡す。");
            }

            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            IPXMorph morph = made.Morph();
            morph.Name = name;
            morph.NameE = string.Empty;
            morph.Kind = MorphKind.Vertex;
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                V3 moved = model.Vertex[at].Position - based.Vertex[at].Position;
                if (moved.X == 0f && moved.Y == 0f && moved.Z == 0f)
                {
                    continue;
                }

                IPXVertexMorphOffset offset = made.VertexMorphOffset();
                offset.Vertex = model.Vertex[at];
                offset.Offset = moved;
                morph.Offsets.Add(offset);
                model.Vertex[at].Position = based.Vertex[at].Position;
            }

            model.Morph.Add(morph);

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedName, model.Morph.Count - 1 },
                    { OffsetsName, morph.Offsets.Count },
                });
        }

        /// <returns>食い違いを述べる文。食い違いが無ければ null。</returns>
        private static string Differs(IPXPmx based, IPXPmx model)
        {
            KeyValuePair<string, int[]>[] counts =
            {
                new KeyValuePair<string, int[]>(
                    "頂点", new[] { based.Vertex.Count, model.Vertex.Count }),
                new KeyValuePair<string, int[]>(
                    "材質", new[] { based.Material.Count, model.Material.Count }),
                new KeyValuePair<string, int[]>(
                    "ボーン", new[] { based.Bone.Count, model.Bone.Count }),
                new KeyValuePair<string, int[]>(
                    "モーフ", new[] { based.Morph.Count, model.Morph.Count }),
                new KeyValuePair<string, int[]>(
                    "剛体", new[] { based.Body.Count, model.Body.Count }),
            };
            foreach (KeyValuePair<string, int[]> held in counts)
            {
                if (held.Value[0] != held.Value[1])
                {
                    return "複製と" + held.Key + "の数が違う: 複製 " + held.Value[0]
                        + " 件・いま " + held.Value[1] + " 件。";
                }
            }

            IDictionary<IPXBone, int> basedBones = Placed(based.Bone);
            IDictionary<IPXBone, int> bones = Placed(model.Bone);
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                if (!Alike(based.Vertex[at], model.Vertex[at], basedBones, bones))
                {
                    return "複製と頂点の座標以外が違う: " + at + " 番目。";
                }
            }

            return null;
        }

        /// <returns>ボーンから、その並びの中の位置へ。</returns>
        private static IDictionary<IPXBone, int> Placed(IList<IPXBone> bones)
        {
            Dictionary<IPXBone, int> placed = new Dictionary<IPXBone, int>(bones.Count);
            for (int at = 0; at < bones.Count; at++)
            {
                placed[bones[at]] = at;
            }

            return placed;
        }

        /// <returns>座標を除いて同じ頂点なら真。ボーンは並びの中の位置で見る。</returns>
        private static bool Alike(
            IPXVertex based,
            IPXVertex vertex,
            IDictionary<IPXBone, int> basedBones,
            IDictionary<IPXBone, int> bones)
        {
            return Same(based.Bone1, vertex.Bone1, basedBones, bones)
                && Same(based.Bone2, vertex.Bone2, basedBones, bones)
                && Same(based.Bone3, vertex.Bone3, basedBones, bones)
                && Same(based.Bone4, vertex.Bone4, basedBones, bones)
                && based.Weight1 == vertex.Weight1
                && based.Weight2 == vertex.Weight2
                && based.Weight3 == vertex.Weight3
                && based.Weight4 == vertex.Weight4
                && based.EdgeScale == vertex.EdgeScale
                && based.QDEF == vertex.QDEF
                && based.SDEF == vertex.SDEF
                && Same(based.Normal, vertex.Normal)
                && Same(based.SDEF_C, vertex.SDEF_C)
                && Same(based.SDEF_R0, vertex.SDEF_R0)
                && Same(based.SDEF_R1, vertex.SDEF_R1)
                && based.UV.X == vertex.UV.X
                && based.UV.Y == vertex.UV.Y
                && Same(based.UVA1, vertex.UVA1)
                && Same(based.UVA2, vertex.UVA2)
                && Same(based.UVA3, vertex.UVA3)
                && Same(based.UVA4, vertex.UVA4);
        }

        private static bool Same(V3 based, V3 held)
        {
            return based.X == held.X && based.Y == held.Y && based.Z == held.Z;
        }

        private static bool Same(V4 based, V4 held)
        {
            return based.X == held.X && based.Y == held.Y && based.Z == held.Z
                && based.W == held.W;
        }

        /// <returns>
        /// どちらも指していないか、どちらも並びの中の同じ位置を指すなら真。片方でも並びに
        /// 居ないボーンを指していれば偽。
        /// </returns>
        private static bool Same(
            IPXBone based,
            IPXBone bone,
            IDictionary<IPXBone, int> basedBones,
            IDictionary<IPXBone, int> bones)
        {
            if (based == null || bone == null)
            {
                return based == null && bone == null;
            }

            int basedAt;
            int at;

            return basedBones.TryGetValue(based, out basedAt)
                && bones.TryGetValue(bone, out at)
                && basedAt == at;
        }

        private static bool TryBase(
            McpMethodContext context, out IPXPmx based, out string code, out string message)
        {
            based = null;
            code = null;
            message = null;
            object given;
            long id;
            if (!context.Params.TryGetValue(BasePmxHandleName, out given)
                || given == null
                || !ValueInput.TryInteger(given, out id))
            {
                code = ToolEnvelope.InvalidArgument;
                message = BasePmxHandleName
                    + " に、動かす前のモデルの複製のハンドルを整数で渡す。";

                return false;
            }

            object held;
            if (id < int.MinValue || id > int.MaxValue
                || !context.Handles.TryGet((int)id, out held)
                || !(held is IPXPmx))
            {
                code = ToolEnvelope.InvalidHandle;
                message = BasePmxHandleName + " が指すハンドルがモデルを持っていない: " + id;

                return false;
            }

            based = (IPXPmx)held;

            return true;
        }
    }
}
