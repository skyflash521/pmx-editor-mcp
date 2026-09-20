using System;
using System.Collections.Generic;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>
    /// テクスチャを読み直し、描画を作り直すツール。
    /// </summary>
    public static class ViewReloadModel
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_reload_model";

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
                ToolName, screen.Method(new List<string>(), ScreenNeeds.View, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            view.UpdateModel();
            view.UpdateView();

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal));
        }
    }
}
