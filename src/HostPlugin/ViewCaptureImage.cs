// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class ViewCaptureImage
    {
        public const string ToolName = "view_capture_image";

        public const string PositionName = "position";

        public const string TargetName = "target";

        public const string UpVectorName = "upVector";

        public const string PerspectiveName = "perspective";

        private const float PerspectiveLeast = 0.1f;

        private const float PerspectiveMost = 179f;

        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            List<string> known = new List<string>
            {
                PositionName, TargetName, UpVectorName, PerspectiveName,
            };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Setting | ScreenNeeds.ModelUntouched,
                    ScreenRefreshKind.None,
                    Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            string[] names = { PositionName, TargetName, UpVectorName };
            int given = names.Count(context.Params.ContainsKey);
            string code;
            string message;
            if (given != 0 && given != names.Length)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    "視点を指すなら " + string.Join("・", names) + " の3つをそろえて渡す。");
            }

            float perspective = 0f;
            bool narrowed = context.Params.ContainsKey(PerspectiveName);
            if (narrowed
                && (!ValueInput.TrySingle(context.Params[PerspectiveName], out perspective)
                    || perspective < PerspectiveLeast
                    || perspective > PerspectiveMost))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    PerspectiveName + " は " + PerspectiveLeast + " 以上 " + PerspectiveMost
                        + " 以下の数でなければならない。");
            }

            V3 position = new V3(0f, 0f, 0f);
            V3 target = position;
            V3 up = position;
            if (given != 0
                && (!ComposedInput.TrySpot(context, PositionName, out position, out code, out message)
                    || !ComposedInput.TrySpot(context, TargetName, out target, out code, out message)
                    || !ComposedInput.TrySpot(context, UpVectorName, out up, out code, out message)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            IPEViewSettingConnector setting = (IPEViewSettingConnector)parts.Setting;
            Bitmap shot;
            if (given == 0 && !narrowed)
            {
                shot = view.GetClientImage();
            }
            else
            {
                IPEVector3 heldPosition = view.CameraPosition;
                IPEVector3 heldTarget = view.CameraTarget;
                IPEVector3 heldUp = view.CameraUpVector;
                float heldPerspective = narrowed ? setting.Perspective : 0f;
                try
                {
                    if (narrowed)
                    {
                        setting.Perspective = perspective;
                    }

                    if (given != 0)
                    {
                        view.SetCameraView(target, position, up);
                    }

                    view.UpdateView();
                    shot = view.GetClientImage();
                }
                finally
                {
                    if (narrowed)
                    {
                        setting.Perspective = heldPerspective;
                    }

                    view.SetCameraView(heldTarget, heldPosition, heldUp);
                    view.UpdateView();
                }
            }

            if (shot == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed, "エディタがPmxViewの画像を撮れなかった。");
            }

            object json;
            IList<string> warnings;
            bool packed;
            using (shot)
            {
                packed = ValueShape.TryToJson(
                    typeof(Bitmap),
                    shot,
                    ImageTransfer.SoleValueMaxLongSide,
                    out json,
                    out warnings,
                    out code,
                    out message);
            }

            return packed
                ? ComposedEditResult.Complete(json, warnings)
                : ComposedEditResult.Refuse(code, message);
        }
    }
}
