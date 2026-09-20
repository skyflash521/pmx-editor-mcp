using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// VMDViewのモーションを捨てるツール。
    /// </summary>
    public static class ViewClearVmdView
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_clear_vmd_view";

        /// <summary>捨てる持ち物を受け取る入力の名前。</summary>
        public const string PartsName = "parts";

        /// <summary>再生だけを止める。</summary>
        public const string MotionOnly = "motionOnly";

        /// <summary>再生を止め、読み込んだモデルも外す。</summary>
        public const string ModelAndMotion = "modelAndMotion";

        /// <summary>受け取れる持ち物。スキーマが並べる順。</summary>
        public static IList<string> Parts
        {
            get
            {
                return new[] { MotionOnly, ModelAndMotion };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
