// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class ViewGetVmdViewState
    {
        public const string ToolName = "view_get_vmd_view_state";

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ぶ。</summary>
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

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string>(),
                    ScreenNeeds.None,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(forms)));
        }

        private static ComposedEditResult Run(Func<IEnumerable<Form>> forms)
        {
            VmdViewControls controls;
            string message;
            if (!VmdViewControls.TryOpen(forms(), out controls, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, message);
            }

            IDictionary<string, object> state = controls.State();
            if (state == null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, VmdViewControls.UnreadableRange);
            }

            return ComposedEditResult.Complete(state);
        }
    }
}
