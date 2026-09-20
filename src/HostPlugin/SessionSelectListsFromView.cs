using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、リストの選択へ写すツール。
    /// </summary>
    public static class SessionSelectListsFromView
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_select_lists_from_view";

        /// <summary>写す種類を受け取る入力の名前。</summary>
        public const string KindsName = "kinds";

        /// <summary>材質のリスト。</summary>
        public const string Material = "material";

        /// <summary>ボーンのリスト。</summary>
        public const string Bone = "bone";

        /// <summary>受け取れる種類。スキーマが並べる順。</summary>
        public static IList<string> Kinds
        {
            get
            {
                return new[] { Material, Bone };
            }
        }

        /// <summary>写した要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
