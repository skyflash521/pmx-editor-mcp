// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>
    /// TransformView の視点を指した位置へ動かすツール。SDKは TransformView の視点へ触る口を持たないので、
    /// PMXView の視点をそこへ入れ、PMXView の「カメラ同期」を Shift を押した扱いで入れて写す。エディタは
    /// Shift を押して同期を入れたときだけ、その場の PMXView の視点を SubView と TransformView へ写す。
    /// SDKで入れた視点は同期で写らない。
    /// </summary>
    public static class MotionSetCameraView
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "motion_set_camera_view";

        private const string ViewForm = "PmxViewForm.PMXView";

        private const int ShiftKey = 0x10;

        private const byte Down = 0x80;

        private static readonly string[] SyncPath = { "menuStrip1", "MenuItem_View", "MenuItem_CameraSync" };

        private static readonly string[] SplitPath = { "menuStrip1", "MenuItem_View", "MenuItem_MultiView" };

        /// <summary>
        /// ツールを表へ足す。<paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ばれる。
        /// </summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen, Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            List<string> known = new List<string>
            {
                ViewCaptureImage.PositionName, ViewCaptureImage.TargetName, ViewCaptureImage.UpVectorName,
            };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, forms)));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts, Func<IEnumerable<Form>> forms)
        {
            V3 position;
            V3 target;
            V3 up;
            string code;
            string message;
            if (!ViewCaptureImage.TrySpot(context, ViewCaptureImage.PositionName, out position, out code, out message)
                || !ViewCaptureImage.TrySpot(context, ViewCaptureImage.TargetName, out target, out code, out message)
                || !ViewCaptureImage.TrySpot(context, ViewCaptureImage.UpVectorName, out up, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            Form view = UiLive.Shown(forms(), ViewForm);
            List<UiMenu> menus = new List<UiMenu>();
            ToolStripMenuItem sync = view == null ? null : UiLive.Find(view, SyncPath, menus) as ToolStripMenuItem;
            ToolStripMenuItem split = view == null ? null : UiLive.Find(view, SplitPath, menus) as ToolStripMenuItem;
            if (sync == null || split == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "PMXView の表示メニューにカメラ同期の項目が見つからない。");
            }

            if (split.Checked)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "PMXView が4画面モードのときは、同期で写る視点をSDKから入れられない。4画面モードを解いてから呼ぶ。");
            }

            if (!sync.Enabled)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, "カメラ同期の項目がいまは押せない。");
            }

            IPXPmxViewConnector pmx = (IPXPmxViewConnector)parts.View;
            bool synced = sync.Checked;
            IPEVector3 heldPosition = pmx.CameraPosition;
            IPEVector3 heldTarget = pmx.CameraTarget;
            IPEVector3 heldUp = pmx.CameraUpVector;
            try
            {
                pmx.SetCameraView(target, position, up);
                if (synced)
                {
                    sync.PerformClick();
                }

                PressWithShift(sync);
            }
            finally
            {
                try
                {
                    if (sync.Checked != synced)
                    {
                        sync.PerformClick();
                    }
                }
                finally
                {
                    pmx.SetCameraView(heldTarget, heldPosition, heldUp);
                    pmx.UpdateView();
                }
            }

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal));
        }

        /// <summary>
        /// このスレッドのキーボードの状態で Shift を押したことにして押す。エディタが見る修飾キーはこの状態から
        /// 読まれ、物理的なキーの状態にも、ほかのスレッドにも及ばない。
        /// </summary>
        private static void PressWithShift(ToolStripItem item)
        {
            byte[] held = new byte[256];
            if (!GetKeyboardState(held))
            {
                throw new InvalidOperationException("キーボードの状態を読めなかった。");
            }

            byte[] shifted = held.ToArray();
            shifted[ShiftKey] |= Down;
            if (!SetKeyboardState(shifted))
            {
                throw new InvalidOperationException("キーボードの状態を書けなかった。");
            }

            try
            {
                item.PerformClick();
            }
            finally
            {
                if (!SetKeyboardState(held))
                {
                    throw new InvalidOperationException("Shift を押した扱いにしたキーボードの状態を戻せなかった。");
                }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] state);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] state);
    }
}
