using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 新しい要素か、指した要素の複製を、並びの指した位置へ入れるツール。位置を省くと末尾へ足す。
    /// 複製は指した要素を写したもので、写した先が指す参照は元と同じ要素を指す。
    /// </summary>
    public static class ModelInsertElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_insert_elements";

        /// <summary>どちらを入れるかを受け取る入力の名前。</summary>
        public const string OperationName = "operation";

        /// <summary>新しい要素を入れる。</summary>
        public const string New = "new";

        /// <summary>指した要素の複製を入れる。</summary>
        public const string Clone = "clone";

        /// <summary>入れる位置を受け取る入力の名前。</summary>
        public const string AtName = "at";

        /// <summary>新しい要素をいくつ入れるかを受け取る入力の名前。</summary>
        public const string CountName = "count";

        /// <summary>入った位置を返す項目の名前。</summary>
        public const string IndicesName = "indices";

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            throw new NotImplementedException();
        }
    }
}
