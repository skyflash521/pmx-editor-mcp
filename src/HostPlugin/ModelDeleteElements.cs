using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した要素を並びから消し、それを指したままの口を片付けるツール。<c>related</c> が
    /// <c>cascade</c> のときは、消した要素だけが使っていた要素も同じ呼び出しで消える。
    /// </summary>
    public static class ModelDeleteElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_delete_elements";

        /// <summary>消した件数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>つられて消えた件数を、種類の名前ごとに返す項目の名前。</summary>
        public const string FollowingName = "following";

        /// <summary>直した口の数を返す項目の名前。</summary>
        public const string RepairedName = "repaired";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
