using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.View;
using PEPlugin.Vmd;

namespace PmxEditorMcp
{
    /// <summary>
    /// VMDViewへモデルとモーションを読み込むツール。
    /// </summary>
    public static class ViewLoadVmdView
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_load_vmd_view";

        /// <summary>読み込む持ち物を受け取る入力の名前。</summary>
        public const string PartsName = "parts";

        /// <summary>モデルだけを読み込む。</summary>
        public const string ModelOnly = "modelOnly";

        /// <summary>モデルとモーションを読み込み、再生を始める。</summary>
        public const string ModelAndMotion = "modelAndMotion";

        /// <summary>受け取れる持ち物。スキーマが並べる順。</summary>
        public static IList<string> Parts
        {
            get
            {
                return new[] { ModelOnly, ModelAndMotion };
            }
        }

        /// <summary>読み込むモーションのファイルの道を受け取る入力の名前。</summary>
        public const string MotionPathName = "motionPath";

        /// <summary>VMDViewが立ち上がっているかを返す項目の名前。</summary>
        public const string BootedName = "booted";

        /// <summary>ツールを表へ足す。<paramref name="motion"/> は空のVMDを1つ作って返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen, Func<object> motion)
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

            List<string> known = new List<string> { PartsName, MotionPathName };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Pmx,
                    (context, parts) => Run(context, parts, motion)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<object> motion)
        {
            string wanted;
            string path;
            string code;
            string message;
            if (!ComposedInput.TryChoice(context, PartsName, Parts, out wanted, out code, out message)
                || !ComposedInput.TryText(
                    context,
                    MotionPathName,
                    wanted,
                    new[] { ModelAndMotion },
                    out path,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool playing = string.Equals(wanted, ModelAndMotion, StringComparison.Ordinal);
            IPEVmd held = null;
            if (playing)
            {
                held = (IPEVmd)motion();
                held.FromFile(path);
            }

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            view.BootupVmdView((IPXPmx)parts.Pmx, held);
            if (playing)
            {
                view.PlayVmdView();
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { BootedName, view.IsVmdViewBootup },
                });
        }
    }
}
