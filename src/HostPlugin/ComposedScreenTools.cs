using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKの1メンバーへ写らない、画面とリストの組み立てツールを表へ足す口。
    /// </summary>
    public static class ComposedScreenTools
    {
        /// <summary>
        /// 組み立ての画面ツールを表へ足す。<paramref name="builder"/> は、VMDやPMXを作る相手を、
        /// <paramref name="subView"/> は別窓の描画の口を返す。
        /// </summary>
        public static void AddTo(
            McpMethodTable methods,
            ComposedScreen screen,
            Func<object> builder,
            Func<object> subView)
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

            ViewSelectElements.AddTo(methods, screen);
            ViewSelectRelated.AddTo(methods, screen);
            ViewFilterDisplay.AddTo(methods, screen);
            ViewSetCameraRotateCenter.AddTo(methods, screen);
            ViewReloadModel.AddTo(methods, screen, subView);
            ViewLoadVmdView.AddTo(methods, screen, builder);
            ViewClearVmdView.AddTo(methods, screen, builder);
            SessionUpdateAllLists.AddTo(methods, screen);
            SessionSelectListsFromView.AddTo(methods, screen);
        }
    }
}
