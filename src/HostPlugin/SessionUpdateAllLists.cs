using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 全部のリストの表示を作り直すツール。
    /// </summary>
    public static class SessionUpdateAllLists
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_update_all_lists";

        /// <summary>作り直したリストの数を返す項目の名前。</summary>
        public const string UpdatedName = "updated";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
