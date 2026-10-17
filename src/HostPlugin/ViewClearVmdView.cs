using System;
using System.Collections.Generic;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.View;
using PEPlugin.Vmd;

namespace PmxEditorMcp
{
    /// <summary>
    /// VMDViewのモーションを捨てるツール。
    /// </summary>
    public static class ViewClearVmdView
    {
        public const string ToolName = "view_clear_vmd_view";

        public const string PartsName = "parts";

        public const string MotionOnly = "motionOnly";

        public const string ModelAndMotion = "modelAndMotion";

        public const string BootedName = "booted";

        /// <summary>受け取れる持ち物。スキーマが並べる順。</summary>
        public static IList<string> Parts
        {
            get
            {
                return new[] { MotionOnly, ModelAndMotion };
            }
        }

        /// <summary><paramref name="builder"/> はVMDを作る相手を返す。</summary>
        public static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> builder)
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

            List<string> known = new List<string> { PartsName };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Pmx,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<object> builder)
        {
            string wanted;
            string code;
            string message;
            if (!ComposedInput.TryChoice(context, PartsName, Parts, out wanted, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            view.StopVmdView();
            if (string.Equals(wanted, ModelAndMotion, StringComparison.Ordinal))
            {
                view.BootupVmdView((IPXPmx)parts.Pmx, ((IPEBuilder)builder()).CreateVmd());
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { BootedName, view.IsVmdViewBootup },
                });
        }
    }
}
