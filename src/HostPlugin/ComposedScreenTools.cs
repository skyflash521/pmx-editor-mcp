using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKの1メンバーへ写らない、画面とリストの組み立てツールを表へ足す口。
    /// </summary>
    public static class ComposedScreenTools
    {
        /// <summary>
        /// 組み立ての画面ツールを表へ足す。<paramref name="builder"/> は、VMDやPMXを作る相手を、
        /// <paramref name="subView"/> は別窓の描画の口を、<paramref name="forms"/> は開いているウィンドウを返す。
        /// </summary>
        public static void AddTo(
            McpMethodTable methods,
            ComposedScreen screen,
            Func<object> builder,
            Func<object> subView,
            Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (subView == null)
            {
                throw new ArgumentNullException(nameof(subView));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            ViewSelectElements.AddTo(methods, screen);
            ViewSelectRelated.AddTo(methods, screen);
            ViewFilterDisplay.AddTo(methods, screen);
            ViewPartsSelectWindow.AddTo(methods, screen);
            ViewSetCameraRotateCenter.AddTo(methods, screen);
            ViewCaptureImage.AddTo(methods, screen);
            ViewReloadModel.AddTo(methods, screen, subView);
            ViewLoadVmdView.AddTo(methods, screen, builder);
            ViewClearVmdView.AddTo(methods, screen, builder);
            SessionUpdateAllLists.AddTo(methods, screen);
            SessionSelectListsFromView.AddTo(methods, screen);
            MotionSetCameraView.AddTo(methods, screen, forms);
        }
    }
}
