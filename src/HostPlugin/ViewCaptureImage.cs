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
    /// <summary>指した視点からPMXビューの画像を撮るツール。</summary>
    public static class ViewCaptureImage
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_capture_image";

        /// <summary>視点の位置を受け取る入力の名前。</summary>
        public const string PositionName = "position";

        /// <summary>視点が見る先を受け取る入力の名前。</summary>
        public const string TargetName = "target";

        /// <summary>視点の上の向きを受け取る入力の名前。</summary>
        public const string UpVectorName = "upVector";

        private const int Components = 3;

        /// <summary>ツールを表へ足す。</summary>
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

            List<string> known = new List<string> { PositionName, TargetName, UpVectorName };
            methods.Add(
                ToolName,
                screen.Method(known, ScreenNeeds.View, ScreenRefreshKind.None, Run));
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

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            Bitmap shot;
            if (given == 0)
            {
                shot = view.GetClientImage();
            }
            else
            {
                V3 position;
                V3 target;
                V3 up;
                if (!TrySpot(context, PositionName, out position, out code, out message)
                    || !TrySpot(context, TargetName, out target, out code, out message)
                    || !TrySpot(context, UpVectorName, out up, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                IPEVector3 heldPosition = view.CameraPosition;
                IPEVector3 heldTarget = view.CameraTarget;
                IPEVector3 heldUp = view.CameraUpVector;
                try
                {
                    view.SetCameraView(target, position, up);
                    view.UpdateView();
                    shot = view.GetClientImage();
                }
                finally
                {
                    view.SetCameraView(heldTarget, heldPosition, heldUp);
                    view.UpdateView();
                }
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

        private static bool TrySpot(
            McpMethodContext context, string name, out V3 spot, out string code, out string message)
        {
            spot = new V3(0f, 0f, 0f);
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(name, out given);
            object[] items = given as object[];
            float[] taken = new float[Components];
            if (items == null || items.Length != taken.Length)
            {
                message = name + " は3つの数の並びでなければならない。";

                return false;
            }

            for (int at = 0; at < taken.Length; at++)
            {
                if (!ValueInput.TrySingle(items[at], out taken[at]))
                {
                    message = name + " は3つの有限の数の並びでなければならない。";

                    return false;
                }
            }

            code = null;
            spot = new V3(taken[0], taken[1], taken[2]);

            return true;
        }
    }
}
