using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した要素を、1つ上・1つ下・先頭・末尾・指した位置へ動かすツール。動かすのは並びだけで、
    /// 要素を指す参照はオブジェクトのまま変わらない。指した要素どうしの前後は保たれる。
    /// </summary>
    public static class ModelReorderElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_reorder_elements";

        /// <summary>どこへ動かすかを受け取る入力の名前。</summary>
        public const string MoveName = "move";

        /// <summary>動かす先の位置を受け取る入力の名前。</summary>
        public const string ToIndexName = "toIndex";

        /// <summary>1つ上へ動かす。</summary>
        public const string Up = "up";

        /// <summary>1つ下へ動かす。</summary>
        public const string Down = "down";

        /// <summary>先頭へ動かす。</summary>
        public const string Top = "top";

        /// <summary>末尾へ動かす。</summary>
        public const string Bottom = "bottom";

        /// <summary>指した位置へ動かす。</summary>
        public const string To = "to";

        /// <summary>動いた後の位置を返す項目の名前。</summary>
        public const string IndicesName = "indices";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
