using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelEditWeights
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_weights";

        /// <summary>指した頂点のウェイトを、その平均へそろえる。</summary>
        public const string Average = "average";

        /// <summary>隣り合う頂点のウェイトの平均へ、強さのぶんだけ寄せる。</summary>
        public const string Smooth = "smooth";

        /// <summary>いちばん近いボーンへ全部のウェイトを振る。</summary>
        public const string FromNearestBonePosition = "fromNearestBonePosition";

        /// <summary>ボーンの線分へいちばん近いボーンへ全部のウェイトを振る。</summary>
        public const string FromNearestBoneAxis = "fromNearestBoneAxis";

        /// <summary>指した軸の鏡像の位置にある頂点から、左右を入れ替えたウェイトを写す。</summary>
        public const string FromMirror = "fromMirror";

        /// <summary>ウェイトの合計を1にそろえる。</summary>
        public const string Normalize = "normalize";

        /// <summary>並びに居ないボーンを指すウェイトを直す。</summary>
        public const string RepairMissingBone = "repairMissingBone";

        public const string ReplaceBone = "replaceBone";

        public const string FromBoneName = "fromBone";

        public const string ToBoneName = "toBone";

        /// <summary>平滑化の強さを受け取る入力の名前。</summary>
        public const string StrengthName = "strength";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        // ボーンの名前で左右を表す綴りは、PMXのモデルが従っている慣例である。
        private const string LeftSide = "左";

        private const string RightSide = "右";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    Average,
                    Smooth,
                    FromNearestBonePosition,
                    FromNearestBoneAxis,
                    FromMirror,
                    Normalize,
                    RepairMissingBone,
                    ReplaceBone,
                };
            }
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
                TargetNames.Element.Selected,
                ModelFindVertexBounds.MaterialIndicesName,
                StrengthName,
                AxisName,
                FromBoneName,
                ToBoneName,
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
                || !ModelFindVertexBounds.TryVertices(
                    context.Params,
                    model,
                    context.Screen.Pick(ElementKinds.Vertex, model.Vertex.Count),
                    out chosen,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float strength;
            string axis;
            if (!ComposedInput.TryFloat(
                    context,
                    StrengthName,
                    operation,
                    new[] { Smooth },
                    0f,
                    1f,
                    out strength,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { FromMirror },
                    ModelEditVertices.Axes,
                    out axis,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int fromBone;
            int toBone;
            if (!ComposedInput.TryCount(
                    context,
                    FromBoneName,
                    operation,
                    new[] { ReplaceBone },
                    0,
                    out fromBone,
                    out code,
                    out message)
                || !ComposedInput.TryCount(
                    context,
                    ToBoneName,
                    operation,
                    new[] { ReplaceBone },
                    0,
                    out toBone,
                    out code,
                    out message)
                || !TryBones(model, operation, fromBone, toBone, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            if (string.Equals(operation, ReplaceBone, StringComparison.Ordinal))
            {
                picked = Holding(picked, model.Bone[fromBone]);
            }

            IList<IList<KeyValuePair<IPXBone, float>>> before =
                picked.Select(VertexWeights.All).ToList();
            switch (operation)
            {
                case Average:
                    Aim(picked, Averaged(picked));
                    break;

                case Smooth:
                    Smoothed(model, picked, strength);
                    break;

                case FromNearestBonePosition:
                    Nearest(model, picked, false);
                    break;

                case FromNearestBoneAxis:
                    Nearest(model, picked, true);
                    break;

                case FromMirror:
                    Mirrored(model, picked, axis);
                    break;

                case ReplaceBone:
                    Replaced(picked, model.Bone[fromBone], model.Bone[toBone]);
                    break;

                default:
                    break;
            }

            ReferenceCleanup.RepairWeights(model, picked);
            foreach (IPXVertex vertex in picked)
            {
                VertexWeights.Write(
                    vertex, VertexWeights.Settled(VertexWeights.Read(vertex)));
            }

            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                changed += VertexWeights.Same(picked[at], before[at]) ? 0 : 1;
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { ScreenRefresh.WeightKind });
        }

        private static bool TryBones(
            IPXPmx model,
            string operation,
            int fromBone,
            int toBone,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            if (!string.Equals(operation, ReplaceBone, StringComparison.Ordinal))
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            if (fromBone >= model.Bone.Count || toBone >= model.Bone.Count)
            {
                message = FromBoneName + " と " + ToBoneName + " はボーンの数 "
                    + model.Bone.Count.ToString(CultureInfo.InvariantCulture)
                    + " 未満でなければならない。";

                return false;
            }

            if (fromBone == toBone)
            {
                message = FromBoneName + " と " + ToBoneName + " は別のボーンを指さなければならない。";

                return false;
            }

            code = null;

            return true;
        }

        private static IList<IPXVertex> Holding(IEnumerable<IPXVertex> picked, IPXBone from)
        {
            return picked
                .Where(vertex => VertexWeights.Read(vertex)
                    .Any(share => share.Value > 0f && ReferenceEquals(share.Key, from)))
                .ToList();
        }

        private static void Replaced(IEnumerable<IPXVertex> held, IPXBone from, IPXBone to)
        {
            foreach (IPXVertex vertex in held)
            {
                if (vertex.SDEF)
                {
                    ReplacedInSlots(vertex, from, to);
                    continue;
                }

                VertexWeights.Write(
                    vertex,
                    VertexWeights.Settled(VertexWeights.Read(vertex).Select(share =>
                        ReferenceEquals(share.Key, from)
                            ? new KeyValuePair<IPXBone, float>(to, share.Value)
                            : share)));
            }
        }

        private static void ReplacedInSlots(IPXVertex vertex, IPXBone from, IPXBone to)
        {
            vertex.Bone1 = ReferenceEquals(vertex.Bone1, from) ? to : vertex.Bone1;
            vertex.Bone2 = ReferenceEquals(vertex.Bone2, from) ? to : vertex.Bone2;
            if (vertex.Bone1 == null || !ReferenceEquals(vertex.Bone1, vertex.Bone2))
            {
                return;
            }

            vertex.Weight1 += vertex.Weight2;
            vertex.Bone2 = null;
            vertex.Weight2 = 0f;
            vertex.SDEF = false;
        }

        private static IList<KeyValuePair<IPXBone, float>> Averaged(IList<IPXVertex> picked)
        {
            return VertexWeights.Settled(picked.SelectMany(VertexWeights.Read));
        }

        private static void Aim(
            IEnumerable<IPXVertex> picked, IList<KeyValuePair<IPXBone, float>> shares)
        {
            foreach (IPXVertex vertex in picked)
            {
                VertexWeights.Write(vertex, shares.ToList());
            }
        }

        private static void Smoothed(IPXPmx model, IList<IPXVertex> picked, float strength)
        {
            IDictionary<IPXVertex, ISet<IPXVertex>> around = Around(model);
            Dictionary<IPXVertex, IList<KeyValuePair<IPXBone, float>>> made =
                new Dictionary<IPXVertex, IList<KeyValuePair<IPXBone, float>>>(
                    ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                ISet<IPXVertex> neighbours;
                if (!around.TryGetValue(vertex, out neighbours) || neighbours.Count == 0)
                {
                    continue;
                }

                IList<KeyValuePair<IPXBone, float>> theirs =
                    VertexWeights.Settled(neighbours.SelectMany(VertexWeights.Read));
                made[vertex] = VertexWeights.Settled(
                    Weighted(VertexWeights.Read(vertex), 1f - strength)
                        .Concat(Weighted(theirs, strength)));
            }

            foreach (KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>> made1 in made)
            {
                VertexWeights.Write(made1.Key, made1.Value);
            }
        }

        private static IEnumerable<KeyValuePair<IPXBone, float>> Weighted(
            IEnumerable<KeyValuePair<IPXBone, float>> shares, float by)
        {
            return shares.Select(
                share => new KeyValuePair<IPXBone, float>(share.Key, share.Value * by));
        }

        /// <summary>同じ面に載っている頂点の表。自分自身は入らない。</summary>
        private static IDictionary<IPXVertex, ISet<IPXVertex>> Around(IPXPmx model)
        {
            Dictionary<IPXVertex, ISet<IPXVertex>> around =
                new Dictionary<IPXVertex, ISet<IPXVertex>>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    IPXVertex[] corners =
                        { face.Vertex1, face.Vertex2, face.Vertex3 };
                    foreach (IPXVertex corner in corners.Where(held => held != null))
                    {
                        foreach (IPXVertex other in corners.Where(held => held != null))
                        {
                            if (!ReferenceEquals(corner, other))
                            {
                                Met(around, corner).Add(other);
                            }
                        }
                    }
                }
            }

            return around;
        }

        private static ISet<IPXVertex> Met(
            IDictionary<IPXVertex, ISet<IPXVertex>> around, IPXVertex corner)
        {
            ISet<IPXVertex> held;
            if (!around.TryGetValue(corner, out held))
            {
                held = new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
                around[corner] = held;
            }

            return held;
        }

        private static void Nearest(IPXPmx model, IList<IPXVertex> picked, bool alongTheBone)
        {
            if (model.Bone.Count == 0)
            {
                return;
            }

            IList<IPXBone> bones = model.Bone;
            NearestBones table = bones.All(bone => Vectors.Finite(bone.Position)
                    && (!alongTheBone || Vectors.Finite(Tip(bone))))
                ? (alongTheBone ? NearestBones.BySegment(bones, Tip) : NearestBones.ByPosition(bones))
                : null;
            foreach (IPXVertex vertex in picked)
            {
                IPXBone closest = table != null && Vectors.Finite(vertex.Position)
                    ? table.Closest(vertex.Position)
                    : Scanned(bones, vertex, alongTheBone);
                VertexWeights.Write(
                    vertex, new[] { new KeyValuePair<IPXBone, float>(closest, 1f) });
            }
        }

        /// <summary>全ボーンを順に比べて最も近いものを選ぶ。距離が等しければ先のものを残す。</summary>
        private static IPXBone Scanned(IList<IPXBone> bones, IPXVertex vertex, bool alongTheBone)
        {
            IPXBone closest = null;
            float best = 0f;
            foreach (IPXBone bone in bones)
            {
                float apart = alongTheBone
                    ? Vectors.DistanceToSegment(vertex.Position, bone.Position, Tip(bone))
                    : Vectors.Distance(vertex.Position, bone.Position);
                if (closest != null && apart >= best)
                {
                    continue;
                }

                closest = bone;
                best = apart;
            }

            return closest;
        }

        /// <summary>そのボーンの表示先の点。表示先を持たないボーンでは根元と同じ点になる。</summary>
        private static V3 Tip(IPXBone bone)
        {
            return bone.ToBone != null
                ? bone.ToBone.Position
                : Vectors.Add(bone.Position, bone.ToOffset);
        }

        private static void Mirrored(IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            Dictionary<string, IPXVertex> spots =
                new Dictionary<string, IPXVertex>(StringComparer.Ordinal);
            foreach (IPXVertex vertex in model.Vertex)
            {
                spots[Spot(vertex.Position)] = vertex;
            }

            foreach (IPXVertex vertex in picked)
            {
                IPXVertex twin;
                if (!spots.TryGetValue(Spot(Across(vertex.Position, axis)), out twin)
                    || ReferenceEquals(twin, vertex))
                {
                    continue;
                }

                VertexWeights.Write(
                    vertex,
                    VertexWeights.Read(twin)
                        .Select(share => new KeyValuePair<IPXBone, float>(
                            Opposite(model, share.Key), share.Value))
                        .ToList());
            }
        }

        private static V3 Across(V3 given, string axis)
        {
            switch (axis)
            {
                case ModelEditVertices.AxisX:
                    return new V3(-given.X, given.Y, given.Z);

                case ModelEditVertices.AxisY:
                    return new V3(given.X, -given.Y, given.Z);

                default:
                    return new V3(given.X, given.Y, -given.Z);
            }
        }

        /// <summary>同じ点を同じ綴りで表す鍵。</summary>
        private static string Spot(V3 given)
        {
            return string.Join(
                "/",
                given.X.ToString("R", CultureInfo.InvariantCulture),
                given.Y.ToString("R", CultureInfo.InvariantCulture),
                given.Z.ToString("R", CultureInfo.InvariantCulture));
        }

        private static IPXBone Opposite(IPXPmx model, IPXBone bone)
        {
            string turned = Turned(bone.Name);

            return turned == null
                ? bone
                : model.Bone.FirstOrDefault(
                    other => string.Equals(other.Name, turned, StringComparison.Ordinal)) ?? bone;
        }

        /// <summary>左右を入れ替えた名前。どちらも入っていない名前では空を返す。</summary>
        private static string Turned(string name)
        {
            if (name == null)
            {
                return null;
            }

            if (name.Contains(LeftSide))
            {
                return name.Replace(LeftSide, RightSide);
            }

            return name.Contains(RightSide) ? name.Replace(RightSide, LeftSide) : null;
        }
    }
}
