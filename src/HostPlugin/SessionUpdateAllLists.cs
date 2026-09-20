using System;
using System.Collections.Generic;
using PEPlugin.Form;
using PEPlugin.Pmd;

namespace PmxEditorMcp
{
    /// <summary>
    /// 全部のリストの表示を作り直すツール。
    /// </summary>
    public static class SessionUpdateAllLists
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_update_all_lists";

        /// <summary>
        /// 作り直しを頼んだリストの区分の数を返す項目の名前。この数はいつも1で、全部の区分をまとめて
        /// 1回で頼んだことを表す。
        /// </summary>
        public const string UpdatedName = "updated";

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

            methods.Add(
                ToolName, screen.Method(new List<string>(), ScreenNeeds.Form, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            ((IPEFormConnector)parts.Form).UpdateList(UpdateObject.All);

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UpdatedName, 1 },
                });
        }
    }
}
