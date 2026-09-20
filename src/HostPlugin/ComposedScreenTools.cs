using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKの1メンバーへ写らない、画面とリストの組み立てツールを表へ足す口。
    /// </summary>
    public static class ComposedScreenTools
    {
        /// <summary>
        /// 組み立ての画面ツールを表へ足す。<paramref name="motion"/> は空のVMDを1つ作って返す。
        /// </summary>
        public static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> motion)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            ViewSelectElements.AddTo(methods, screen);
            ViewSelectRelated.AddTo(methods, screen);
            ViewFilterDisplay.AddTo(methods, screen);
            ViewSetCameraRotateCenter.AddTo(methods, screen);
            ViewReloadModel.AddTo(methods, screen);
            ViewLoadVmdView.AddTo(methods, screen, motion);
            ViewClearVmdView.AddTo(methods, screen, motion);
            SessionUpdateAllLists.AddTo(methods, screen);
            SessionSelectListsFromView.AddTo(methods, screen);
        }
    }
}
