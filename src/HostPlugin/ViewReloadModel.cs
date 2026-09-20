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

        /// <summary>
        /// ツールを表へ足す。<paramref name="subView"/> は、別窓の描画の口を返す。引けないときは
        /// null を返してよい。
        /// </summary>
        public static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> subView)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (subView == null)
            {
                throw new ArgumentNullException(nameof(subView));
            }

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string>(),
                    ScreenNeeds.View,
                    (context, parts) => Run(parts, subView)));
        }

        private static ComposedEditResult Run(ScreenParts parts, Func<object> subView)
        {
            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            view.UpdateModel();
            view.UpdateView();
            IPESubViewConnector apart = subView() as IPESubViewConnector;
            if (apart != null)
            {
                apart.UpdateView();
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal));
        }
    }
}
