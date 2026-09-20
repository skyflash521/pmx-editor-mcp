using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、全選択・反転・拡張・縮小・子の連なり・半モデルで置き換えるツール。
    /// </summary>
    public static class ViewSelectElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_select_elements";

        /// <summary>その種類の要素を全部選ぶ。</summary>
        public const string All = "all";

        /// <summary>いま選んでいるものと選んでいないものを入れ替える。</summary>
        public const string Invert = "invert";

        /// <summary>いまの選択に、面で隣り合う要素を足す。</summary>
        public const string Expand = "expand";

        /// <summary>いまの選択から、選んでいない要素と面で隣り合うものを外す。</summary>
        public const string Reduce = "reduce";

        /// <summary>いま選んでいるボーンの子孫を足す。</summary>
        public const string ChildChain = "childChain";

        /// <summary>指した軸の片側にある要素だけを選ぶ。</summary>
        public const string HalfModel = "halfModel";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { All, Invert, Expand, Reduce, ChildChain, HalfModel };
            }
        }

        /// <summary>選ぶ要素の種類を受け取る入力の名前。</summary>
        public const string KindName = "kind";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>選んだ要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
