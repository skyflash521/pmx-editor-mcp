using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKの1メンバーへ写らない、画面とリストの組み立てツールを表へ足す口。
    /// </summary>
    public static class ComposedScreenTools
    {
        /// <summary>組み立ての画面ツールを表へ足す。</summary>
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

            throw new NotImplementedException();
        }
    }
}
