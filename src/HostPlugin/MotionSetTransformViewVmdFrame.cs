// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PEPlugin.Pmx;
using PEPlugin.View;
using PEPlugin.Vmd;

namespace PmxEditorMcp
{
    public static class MotionSetTransformViewVmdFrame
    {
        public const string ToolName = "motion_set_transform_view_vmd_frame";

        public const string VmdName = "vmd";

        public const string FrameName = "frame";

        public const string BonesName = "bones";

        public const string MorphsName = "morphs";

        /// <param name="transformView">TransformView のコネクタを返す。UIスレッドで呼ばれる。</param>
        internal static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> transformView, IVmdPoseSource poses)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (transformView == null)
            {
                throw new ArgumentNullException(nameof(transformView));
            }

            if (poses == null)
            {
                throw new ArgumentNullException(nameof(poses));
            }

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string> { VmdName, FrameName },
                    ScreenNeeds.Pmx,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, transformView, poses)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<object> transformView, IVmdPoseSource poses)
        {
            object given;
            long id;
            if (!context.Params.TryGetValue(VmdName, out given) || !ValueInput.TryInteger(given, out id))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, VmdName + " は VMD のハンドルの整数でなければならない。");
            }

            int frame;
            if (!context.Params.TryGetValue(FrameName, out given) || !ValueInput.TryIndex(given, out frame)
                || frame < 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, FrameName + " は0以上の整数でなければならない。");
            }

            object held;
            if (id < int.MinValue || id > int.MaxValue || !context.Handles.TryGet((int)id, out held))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidHandle, VmdName + " に使えないハンドルを渡している: " + id);
            }

            IPETransformViewConnector view = transformView() as IPETransformViewConnector;
            if (view == null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, "TransformView のコネクタを引けない。");
            }

            if (!view.Visible)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView が開いていない。" + UiOpenWindow.ToolName + " で開いてから呼ぶ。");
            }

            if (!NumberFormatInfo.CurrentInfo.NumberDecimalSeparator.Equals(".", StringComparison.Ordinal))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "エディタは VPD の数を動いているカルチャの書式で読み、このカルチャの小数点は . でないので、"
                        + "数を VPD の文面に書けない。");
            }

            IPXPmx model = (IPXPmx)parts.Pmx;
            HashSet<string> boneNames = Names(model.Bone.Select(bone => bone.Name));
            HashSet<string> morphNames = MorphNamesTheEditorTakes(model);
            PoseRead read = poses.Read(held, boneNames, morphNames, frame);
            if (read.IsNotAMotion)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidHandle, VmdName + " が指すハンドルは VMD ではない: " + id);
            }

            if (read.Bones.Count == 0 && read.Morphs.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "その VMD にキーを持つボーンとモーフの名前が、いまのモデルのボーンとモーフの名前のどれにも当たらない。");
            }

            string text;
            if (!TryVpdText(read, out text))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "VMD のキーの値が有限の数でないので、姿勢を求められない。");
            }

            view.ResetTransform();
            if (!view.SetVpdFromText(text))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed, "エディタが姿勢の文面を VPD として受け取らなかった。");
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { FrameName, frame },
                    { BonesName, read.Bones.Count },
                    { MorphsName, read.Morphs.Count },
                });
        }

        /// <summary>エディタの VPD の読み取りは、名前の先頭の空白を読み飛ばし、改行で名前を切る。</summary>
        private static HashSet<string> Names(IEnumerable<string> names)
        {
            HashSet<string> taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(name) && !char.IsWhiteSpace(name[0])
                    && name.IndexOf('\r') < 0 && name.IndexOf('\n') < 0)
                {
                    taken.Add(name);
                }
            }

            return taken;
        }

        private static HashSet<string> MorphNamesTheEditorTakes(IPXPmx model)
        {
            List<string> all = model.Morph.Select(morph => morph.Name).ToList();
            Dictionary<string, int> first = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int at = 0; at < all.Count; at++)
            {
                if (all[at] != null && !first.ContainsKey(all[at]))
                {
                    first.Add(all[at], at);
                }
            }

            return Names(first.Where(pair => pair.Value < first.Count).Select(pair => pair.Key));
        }

        internal static bool TryVpdText(PoseRead read, out string text)
        {
            text = null;
            StringBuilder made = new StringBuilder();
            made.AppendLine("Vocaloid Pose Data file");
            made.AppendLine();
            made.AppendLine("vmd.osm;");
            made.AppendLine(read.Bones.Count.ToString(CultureInfo.InvariantCulture) + ";");
            made.AppendLine();
            for (int at = 0; at < read.Bones.Count; at++)
            {
                BonePoseValue bone = read.Bones[at];
                if (!bone.Translation.Concat(bone.Rotation).All(IsFinite))
                {
                    return false;
                }

                made.AppendLine("Bone" + at.ToString(CultureInfo.InvariantCulture) + "{" + bone.Name);
                made.AppendLine("  " + Join(bone.Translation) + ";");
                made.AppendLine("  " + Join(bone.Rotation) + ";");
                made.AppendLine("}");
                made.AppendLine();
            }

            for (int at = 0; at < read.Morphs.Count; at++)
            {
                MorphValue morph = read.Morphs[at];
                if (!IsFinite(morph.Value))
                {
                    return false;
                }

                made.AppendLine("Morph" + at.ToString(CultureInfo.InvariantCulture) + "{" + morph.Name);
                made.AppendLine("  " + Number(morph.Value) + ";");
                made.AppendLine("}");
                made.AppendLine();
            }

            text = made.ToString();

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Join(IEnumerable<float> values)
        {
            return string.Join(",", values.Select(Number));
        }

        private static string Number(float value)
        {
            return value.ToString("R", CultureInfo.CurrentCulture);
        }
    }

    internal interface IVmdPoseSource
    {
        /// <summary>
        /// <paramref name="vmd"/> のフレーム <paramref name="frame"/> の姿勢のうち、<paramref name="bones"/> と
        /// <paramref name="morphs"/> にある名前でキーを持つものを返す。
        /// </summary>
        PoseRead Read(object vmd, ISet<string> bones, ISet<string> morphs, int frame);
    }

    internal sealed class PoseRead
    {
        private PoseRead(IList<BonePoseValue> bones, IList<MorphValue> morphs, bool notAMotion)
        {
            Bones = bones;
            Morphs = morphs;
            IsNotAMotion = notAMotion;
        }

        internal static PoseRead NotAMotion
        {
            get { return new PoseRead(new BonePoseValue[0], new MorphValue[0], true); }
        }

        internal bool IsNotAMotion { get; }

        internal IList<BonePoseValue> Bones { get; }

        internal IList<MorphValue> Morphs { get; }

        internal static PoseRead Found(IList<BonePoseValue> bones, IList<MorphValue> morphs)
        {
            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (morphs == null)
            {
                throw new ArgumentNullException(nameof(morphs));
            }

            return new PoseRead(bones, morphs, false);
        }
    }

    internal sealed class SdkVmdPoseSource : IVmdPoseSource
    {
        public PoseRead Read(object vmd, ISet<string> bones, ISet<string> morphs, int frame)
        {
            IPEVmd motion = vmd as IPEVmd;
            if (motion == null)
            {
                return PoseRead.NotAMotion;
            }

            Dictionary<string, List<BoneKeySample>> boneKeys =
                new Dictionary<string, List<BoneKeySample>>(StringComparer.Ordinal);
            Dictionary<int, string> boneNamed = Indexed(bones, motion.GetBoneIndex);
            foreach (IPEVmdBoneKey key in motion.Bone)
            {
                string name;
                if (!boneNamed.TryGetValue(key.BoneIndex, out name))
                {
                    continue;
                }

                List<BoneKeySample> keys;
                if (!boneKeys.TryGetValue(name, out keys))
                {
                    keys = new List<BoneKeySample>();
                    boneKeys.Add(name, keys);
                }

                keys.Add(new BoneKeySample(
                    ((IPEVmdFrameKey)key).FrameIndex,
                    new[] { key.Translation.X, key.Translation.Y, key.Translation.Z },
                    new[] { key.Rotation.X, key.Rotation.Y, key.Rotation.Z, key.Rotation.W },
                    Curve(key.IplX),
                    Curve(key.IplY),
                    Curve(key.IplZ),
                    Curve(key.IplR)));
            }

            Dictionary<string, List<MorphKeySample>> morphKeys =
                new Dictionary<string, List<MorphKeySample>>(StringComparer.Ordinal);
            Dictionary<int, string> morphNamed = Indexed(morphs, motion.GetMorphIndex);
            foreach (IPEVmdMorphKey key in motion.Morph)
            {
                string name;
                if (!morphNamed.TryGetValue(key.MorphIndex, out name))
                {
                    continue;
                }

                List<MorphKeySample> keys;
                if (!morphKeys.TryGetValue(name, out keys))
                {
                    keys = new List<MorphKeySample>();
                    morphKeys.Add(name, keys);
                }

                keys.Add(new MorphKeySample(((IPEVmdFrameKey)key).FrameIndex, key.Value));
            }

            List<BonePoseValue> posed = new List<BonePoseValue>();
            foreach (KeyValuePair<string, List<BoneKeySample>> pair in boneKeys.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                posed.Add(VmdFrameSampler.Bone(pair.Key, pair.Value, frame));
            }

            List<MorphValue> valued = new List<MorphValue>();
            foreach (KeyValuePair<string, List<MorphKeySample>> pair in morphKeys.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                valued.Add(VmdFrameSampler.Morph(pair.Key, pair.Value, frame));
            }

            return PoseRead.Found(posed, valued);
        }

        private static Dictionary<int, string> Indexed(ISet<string> wanted, Func<string, int> indexOf)
        {
            Dictionary<int, string> named = new Dictionary<int, string>();
            foreach (string name in wanted)
            {
                int index = indexOf(name);
                if (index >= 0 && !named.ContainsKey(index))
                {
                    named.Add(index, name);
                }
            }

            return named;
        }

        private static IplCurve Curve(IPEVmdIPL curve)
        {
            return curve == null ? IplCurve.Linear : new IplCurve(curve.X1, curve.Y1, curve.X2, curve.Y2);
        }
    }
}
