// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class MotionRotateBoneAboutAxis
    {
        public const string ToolName = "motion_rotate_bone_about_axis";

        public const string AxisName = "axis";

        public const string AngleName = "angle";

        public const string RotateXyzName = "rotateXyz";

        /// <param name="transformView">TransformView のコネクタを返す。UIスレッドで呼ばれる。</param>
        public static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> transformView, IModifierKeys keys)
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

            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string> { AxisName, AngleName },
                    ScreenNeeds.Pmx,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, transformView, keys)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<object> transformView, IModifierKeys keys)
        {
            V3 axis;
            string code;
            string message;
            if (!ComposedInput.TryDirection(context, AxisName, out axis, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            object given;
            float angle;
            if (!context.Params.TryGetValue(AngleName, out given) || !ValueInput.TrySingle(given, out angle))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, AngleName + " は有限の数でなければならない。");
            }

            IPETransformViewConnector view = transformView() as IPETransformViewConnector;
            if (view == null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, "TransformView のコネクタを引けない。");
            }

            int chosen = view.SelectedBoneIndex;
            if (!PreconditionGate.TryAccept(PreconditionKind.TransformedBone, chosen, keys.AnyHeld(), out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, message);
            }

            IPXPmx model = (IPXPmx)parts.Pmx;
            if (chosen >= model.Bone.Count)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "TransformView で選ばれているボーン " + chosen + " がモデルに無い。");
            }

            if (model.Bone[chosen].IsFixAxis)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView で選ばれているボーン " + chosen + " は軸固定で、固定軸のまわりにしか回らない。"
                        + "motion_bone_rotate で回す。");
            }

            // エディタは入力欄の値を Z・X・Y の順に親の軸まわりへ重ね、YawPitchRoll(Y, X, Z) と同じ行列で回る。
            V3 radians = RowMatrix
                .AroundAxis(axis.X, axis.Y, axis.Z, angle * Math.PI / 180d)
                .ToYawPitchRollAngles();
            V3 turned = new V3((float)Degrees(radians.X), (float)Degrees(radians.Y), (float)Degrees(radians.Z));
            IPEVector3 held = view.BoneRotate_XYZ;
            try
            {
                view.BoneRotate_XYZ = turned;
                view.BoneRotate();
            }
            finally
            {
                view.BoneRotate_XYZ = held;
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { RotateXyzName, new object[] { (double)turned.X, (double)turned.Y, (double)turned.Z } },
                });
        }

        private static double Degrees(double radians)
        {
            return radians * 180 / Math.PI;
        }
    }
}
