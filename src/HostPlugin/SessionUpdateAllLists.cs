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
        public const string ToolName = "session_update_all_lists";

        public const string UpdatedName = "updated";

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
                ToolName,
                screen.Method(
                    new List<string>(), ScreenNeeds.Form, ScreenRefreshKind.None, Run));
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
