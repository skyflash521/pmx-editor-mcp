using System;
using System.Collections.Generic;

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

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
