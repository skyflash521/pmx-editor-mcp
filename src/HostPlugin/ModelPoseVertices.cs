using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using BonePose = PmxEditorMcp.VertexPose.BonePose;
using Quat = PmxEditorMcp.VertexPose.Quat;
using Vec = PmxEditorMcp.VertexPose.Vec;

namespace PmxEditorMcp
{
    public static class ModelPoseVertices
    {
        public const string ToolName = "model_pose_vertices";

        public const string BonesName = "bones";

        public const string IndexName = "index";

        public const string RotationName = "rotation";

        public const string TranslationName = "translation";

        public const string UpdatedName = "updated";

        private static readonly string[] BoneMembers = { IndexName, RotationName, TranslationName };

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

            List<string> known = new List<string>(ModelFindVertexBounds.VertexPointing)
            {
                ModelFindVertexBounds.MaterialIndicesName,
                BonesName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string code;
            string message;
            IDictionary<int, KeyValuePair<Quat, Vec>> given;
            IEnumerable<int> chosen;
            if (!TryBones(context, model, out given, out code, out message)
                || !ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            SortedSet<int> targets = new SortedSet<int>(chosen);
            BonePoseSolver solver = new BonePoseSolver(model, given);
            Dictionary<IPXBone, BonePose> poses = new Dictionary<IPXBone, BonePose>();
            foreach (int at in targets)
            {
                foreach (IPXBone bone in VertexPose.Used(model.Vertex[at]))
                {
                    BonePose pose;
                    if (poses.ContainsKey(bone))
                    {
                        continue;
                    }

                    if (!solver.TryPose(bone, out pose, out message))
                    {
                        return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
                    }

                    if (pose == null)
                    {
                        return ComposedEditResult.Refuse(
                            ToolEnvelope.InvalidArgument, "頂点 " + at + " が参照するボーンがモデルのボーンの並びに無い。");
                    }

                    poses.Add(bone, pose);
                }
            }

            List<KeyValuePair<IPXVertex, V3[]>> posed = new List<KeyValuePair<IPXVertex, V3[]>>();
            foreach (int at in targets)
            {
                IPXVertex vertex = model.Vertex[at];
                Vec position;
                Vec normal;
                if (!VertexPose.TrySkin(vertex, bone => poses[bone], out position, out normal))
                {
                    continue;
                }

                V3[] written = { position.ToV3(), normal.ToV3() };
                if (!IsFinite(written[0]) || !IsFinite(written[1]))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument, "頂点 " + at + " の姿勢後の値が有限の数にならない。");
                }

                posed.Add(new KeyValuePair<IPXVertex, V3[]>(vertex, written));
            }

            foreach (KeyValuePair<IPXVertex, V3[]> one in posed)
            {
                one.Key.Position = one.Value[0];
                one.Key.Normal = one.Value[1];
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal) { { UpdatedName, targets.Count } },
                new[] { ElementKinds.Vertex });
        }

        private static bool IsFinite(V3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        private static bool TryBones(
            McpMethodContext context,
            IPXPmx model,
            out IDictionary<int, KeyValuePair<Quat, Vec>> given,
            out string code,
            out string message)
        {
            given = new Dictionary<int, KeyValuePair<Quat, Vec>>();
            code = ToolEnvelope.InvalidArgument;
            message = BonesName + " は、index と、省いてよい rotation・translation を持つボーンを、1つ以上並べた並びでなければならない。";
            object held;
            context.Params.TryGetValue(BonesName, out held);
            object[] items = held as object[];
            if (items == null || items.Length == 0)
            {
                return false;
            }

            foreach (object item in items)
            {
                IDictionary<string, object> bone = item as IDictionary<string, object>;
                if (bone == null || bone.Keys.Any(k => !BoneMembers.Contains(k, StringComparer.Ordinal)))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = BonesName + " の要素は " + string.Join("・", BoneMembers) + " のほかの項目を持てない。";

                    return false;
                }

                int index;
                object value;
                string name = BonesName + "[]." + IndexName;
                bone.TryGetValue(IndexName, out value);
                if (!PositionInput.TryOne(
                        value,
                        name,
                        name + " はボーンの位置でなければならない。",
                        model.Bone.Count,
                        out index,
                        out code,
                        out message))
                {
                    return false;
                }

                Quat rotation;
                Vec translation;
                if (!TryRotation(bone, out rotation, out message) || !TryTranslation(bone, out translation, out message))
                {
                    code = ToolEnvelope.InvalidArgument;

                    return false;
                }

                if (given.ContainsKey(index))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = BonesName + " に同じボーンを2度並べられない: " + index;

                    return false;
                }

                given.Add(index, new KeyValuePair<Quat, Vec>(rotation, translation));
            }

            code = null;
            message = null;

            return true;
        }

        private static bool TryRotation(IDictionary<string, object> bone, out Quat rotation, out string message)
        {
            rotation = Quat.Identity;
            message = null;
            float[] taken;
            if (!TryNumbers(bone, RotationName, 4, out taken, out message))
            {
                return false;
            }

            if (taken == null)
            {
                return true;
            }

            Quat given = new Quat(taken[0], taken[1], taken[2], taken[3]);
            double length = given.Length;
            if (!(length > 0d) || double.IsInfinity(length))
            {
                message = BonesName + "[]." + RotationName + " は長さを持つ四元数でなければならない。";

                return false;
            }

            rotation = given * (1d / length);

            return true;
        }

        private static bool TryTranslation(IDictionary<string, object> bone, out Vec translation, out string message)
        {
            translation = new Vec(0d, 0d, 0d);
            float[] taken;
            if (!TryNumbers(bone, TranslationName, 3, out taken, out message))
            {
                return false;
            }

            if (taken != null)
            {
                translation = new Vec(taken[0], taken[1], taken[2]);
            }

            return true;
        }

        private static bool TryNumbers(
            IDictionary<string, object> bone, string name, int count, out float[] taken, out string message)
        {
            taken = null;
            message = null;
            object held;
            if (!bone.TryGetValue(name, out held))
            {
                return true;
            }

            object[] items = held as object[];
            float[] read = new float[count];
            bool fits = items != null && items.Length == count;
            for (int at = 0; fits && at < count; at++)
            {
                fits = ValueInput.TrySingle(items[at], out read[at]);
            }

            if (!fits)
            {
                message = BonesName + "[]." + name + " は " + count + " つの有限の数の並びでなければならない。";

                return false;
            }

            taken = read;

            return true;
        }
    }
}
