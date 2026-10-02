// ComposedEdit に載る合成ツール。
using System;
using System.Collections.Generic;
using System.Linq;
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

        public const string BoneMorphNameName = "boneMorphName";

        public const string BoneAddedName = "boneAdded";

        public const string BoneOffsetsName = "boneOffsets";

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

            List<string> known = new List<string> { BasePmxHandleName, NameName, BoneMorphNameName };
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

            string differs = context.Params.ContainsKey(BoneMorphNameName)
                ? ModelCompareShape.Differs(based, model)
                : Differs(based, model);
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

            string boneMorphName = null;
            object boneGiven;
            if (context.Params.TryGetValue(BoneMorphNameName, out boneGiven))
            {
                boneMorphName = boneGiven as string;
                if (string.IsNullOrEmpty(boneMorphName))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        BoneMorphNameName + " に足すボーンモーフの名前を空でない文字で渡す。");
                }

                if (string.Equals(boneMorphName, name, StringComparison.Ordinal))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        BoneMorphNameName + " は " + NameName + " と違う名前を渡す。");
                }
            }

            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            if (boneMorphName != null)
            {
                return RunBack(model, based, made, name, boneMorphName);
            }

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

        private static ComposedEditResult RunBack(
            IPXPmx model, IPXPmx based, IPXPmxBuilder made, string name, string boneMorphName)
        {
            IDictionary<IPXBone, int> placed = Placed(model.Bone);
            V3[] moves = new V3[model.Bone.Count];
            for (int at = 0; at < moves.Length; at++)
            {
                moves[at] = based.Bone[at].Position - model.Bone[at].Position;
            }

            IPXMorph vertexMorph = made.Morph();
            vertexMorph.Name = name;
            vertexMorph.NameE = string.Empty;
            vertexMorph.Kind = MorphKind.Vertex;
            IPXMorph boneMorph = made.Morph();
            boneMorph.Name = boneMorphName;
            boneMorph.NameE = string.Empty;
            boneMorph.Kind = MorphKind.Bone;
            boneMorph.Panel = vertexMorph.Panel;
            for (int at = 0; at < moves.Length; at++)
            {
                int parent;
                V3 own = moves[at];
                IPXBone parentBone = model.Bone[at].Parent;
                if (parentBone != null && placed.TryGetValue(parentBone, out parent))
                {
                    own = own - moves[parent];
                }

                if (own.X == 0f && own.Y == 0f && own.Z == 0f)
                {
                    continue;
                }

                IPXBoneMorphOffset posed = made.BoneMorphOffset();
                posed.Bone = model.Bone[at];
                posed.Translation = own;
                posed.Rotation = new Q(0f, 0f, 0f, 1f);
                boneMorph.Offsets.Add(posed);
            }

            for (int at = 0; at < model.Vertex.Count; at++)
            {
                IPXVertex vertex = model.Vertex[at];
                V3 left = based.Vertex[at].Position - vertex.Position;
                IList<KeyValuePair<IPXBone, float>> shares = VertexWeights.Read(vertex)
                    .Where(share => share.Value != 0f)
                    .ToList();
                if (shares.Count == 0 && vertex.Bone1 != null)
                {
                    shares = new[] { new KeyValuePair<IPXBone, float>(vertex.Bone1, 1f) };
                }

                foreach (KeyValuePair<IPXBone, float> share in shares)
                {
                    int bone;
                    if (placed.TryGetValue(share.Key, out bone))
                    {
                        left = left - moves[bone] * share.Value;
                    }
                }

                if (left.X == 0f && left.Y == 0f && left.Z == 0f)
                {
                    continue;
                }

                IPXVertexMorphOffset offset = made.VertexMorphOffset();
                offset.Vertex = vertex;
                offset.Offset = left;
                vertexMorph.Offsets.Add(offset);
            }

            model.Morph.Add(vertexMorph);
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { AddedName, model.Morph.Count - 1 },
                { OffsetsName, vertexMorph.Offsets.Count },
            };
            if (boneMorph.Offsets.Count > 0)
            {
                model.Morph.Add(boneMorph);
                value[BoneAddedName] = model.Morph.Count - 1;
                value[BoneOffsetsName] = boneMorph.Offsets.Count;
            }

            return ComposedEditResult.Complete(value);
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

        /// <returns>
        /// 座標を除いて同じ頂点なら真。ボーンは並びの中の位置で見る。重みを持つ枠のボーンと重みの組は
        /// 枠の並びを問わずに比べる。そのうえで、ボーン・重み・変形方式・SDEFの参照点を、エディタが
        /// 反映のたびにかける正規化を両方へかけてから比べる。
        /// </returns>
        private static bool Alike(
            IPXVertex based,
            IPXVertex vertex,
            IDictionary<IPXBone, int> basedBones,
            IDictionary<IPXBone, int> bones)
        {
            Weights basedWeights = Weights.Normalized(based, basedBones);
            Weights weights = Weights.Normalized(vertex, bones);

            return basedWeights != null
                && weights != null
                && Weighted(based, basedBones).SequenceEqual(Weighted(vertex, bones))
                && basedWeights.SameAs(weights)
                && based.EdgeScale == vertex.EdgeScale
                && Same(based.Normal, vertex.Normal)
                && Same(based.SDEF_C, vertex.SDEF_C)
                && based.UV.X == vertex.UV.X
                && based.UV.Y == vertex.UV.Y
                && Same(based.UVA1, vertex.UVA1)
                && Same(based.UVA2, vertex.UVA2)
                && Same(based.UVA3, vertex.UVA3)
                && Same(based.UVA4, vertex.UVA4);
        }

        /// <returns>重みを持つ枠の、ボーンの位置と重みの組を並べたもの。ボーンを指さない枠の位置は -1。</returns>
        private static IList<KeyValuePair<int, float>> Weighted(
            IPXVertex vertex, IDictionary<IPXBone, int> bones)
        {
            IPXBone[] held = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
            float[] given = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
            List<KeyValuePair<int, float>> weighted = new List<KeyValuePair<int, float>>();
            for (int slot = 0; slot < held.Length; slot++)
            {
                int at;
                if (given[slot] != 0f)
                {
                    weighted.Add(new KeyValuePair<int, float>(
                        held[slot] != null && bones.TryGetValue(held[slot], out at) ? at : -1,
                        given[slot]));
                }
            }

            return weighted.OrderBy(w => w.Key).ThenBy(w => w.Value).ToList();
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

        /// <summary>
        /// 頂点のボーン・重み・変形方式・SDEFの参照点に、エディタが反映のたびにかける正規化
        /// (PmxVertex.NormalizeWeight)をかけたもの。ボーンは並びの中の位置で持ち、指さない枠は -1。
        /// </summary>
        private sealed class Weights
        {
            private const int Slots = 4;

            private readonly int[] _bones = new int[Slots];

            private readonly float[] _values = new float[Slots];

            private Deform _deform;

            private V3 _r0;

            private V3 _r1;

            private enum Deform
            {
                Bdef1,

                Bdef2,

                Bdef4,

                Sdef,

                Qdef,
            }

            /// <returns>重みを持つ枠が並びに居ないボーンを指していれば null。</returns>
            public static Weights Normalized(IPXVertex vertex, IDictionary<IPXBone, int> bones)
            {
                IPXBone[] held = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
                float[] given = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
                Weights made = new Weights
                {
                    _deform = vertex.SDEF ? Deform.Sdef : vertex.QDEF ? Deform.Qdef : Deform.Bdef2,
                    _r0 = vertex.SDEF_R0,
                    _r1 = vertex.SDEF_R1,
                };
                for (int slot = 0; slot < Slots; slot++)
                {
                    int at;
                    if (held[slot] == null)
                    {
                        at = -1;
                    }
                    else if (!bones.TryGetValue(held[slot], out at))
                    {
                        if (given[slot] != 0f)
                        {
                            return null;
                        }

                        at = -1;
                    }

                    made._bones[slot] = at;
                    made._values[slot] = at < 0 ? 0f : given[slot];
                }

                made.Normalize();

                return made;
            }

            public bool SameAs(Weights other)
            {
                return _deform == other._deform
                    && _bones.SequenceEqual(other._bones)
                    && _values.SequenceEqual(other._values)
                    && Same(_r0, other._r0)
                    && Same(_r1, other._r1);
            }

            private void Normalize()
            {
                if (_deform == Deform.Sdef)
                {
                    if (_bones[0] > _bones[1])
                    {
                        Swap(0, 1);
                        V3 first = _r0;
                        _r0 = _r1;
                        _r1 = first;
                    }
                }
                else
                {
                    Order();
                }

                _deform = Settled();
                if (_deform == Deform.Sdef)
                {
                    for (int slot = 2; slot < Slots; slot++)
                    {
                        _bones[slot] = -1;
                        _values[slot] = 0f;
                    }
                }

                if (_deform != Deform.Bdef4 && _deform != Deform.Qdef)
                {
                    float sum = 0f;
                    for (int slot = 0; slot < Slots; slot++)
                    {
                        sum += _values[slot];
                    }

                    if (sum != 0f && sum != 1f)
                    {
                        float scale = 1f / sum;
                        for (int slot = 0; slot < Slots; slot++)
                        {
                            _values[slot] *= scale;
                        }
                    }
                }

                int used = _deform == Deform.Bdef2 || _deform == Deform.Sdef ? 2
                    : _deform == Deform.Bdef4 || _deform == Deform.Qdef ? Slots : 1;
                for (int slot = 0; slot < used; slot++)
                {
                    if (_bones[slot] < 0)
                    {
                        _bones[slot] = 0;
                        _values[slot] = 0f;
                    }
                }

                _deform = Settled();
            }

            /// <summary>重みの絶対値の大きい順。同じ重みの枠は元の並びを保つ。</summary>
            private void Order()
            {
                for (int slot = 1; slot < Slots; slot++)
                {
                    for (int to = slot; to > 0 && Math.Abs(_values[to - 1]) < Math.Abs(_values[to]); to--)
                    {
                        Swap(to - 1, to);
                    }
                }
            }

            private Deform Settled()
            {
                int count = _values.Count(v => v != 0f);
                if (_deform == Deform.Sdef && count != 1)
                {
                    return Deform.Sdef;
                }

                if (_deform == Deform.Qdef && count != 1)
                {
                    return Deform.Qdef;
                }

                return count <= 1 ? Deform.Bdef1 : count == 2 ? Deform.Bdef2 : Deform.Bdef4;
            }

            private void Swap(int first, int second)
            {
                int bone = _bones[first];
                _bones[first] = _bones[second];
                _bones[second] = bone;
                float value = _values[first];
                _values[first] = _values[second];
                _values[second] = value;
            }
        }

        internal static bool TryBase(
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
