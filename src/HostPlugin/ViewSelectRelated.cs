using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、関連する要素で選び直すツール。
    /// </summary>
    public static class ViewSelectRelated
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_select_related";

        /// <summary>選んだ頂点だけで作られている面を選ぶ。</summary>
        public const string VerticesToFaces = "verticesToFaces";

        /// <summary>選んだ面が使う頂点を選ぶ。</summary>
        public const string FacesToVertices = "facesToVertices";

        /// <summary>選んだ面と辺を共有する面を足す。</summary>
        public const string ExpandAdjacentFaces = "expandAdjacentFaces";

        /// <summary>指した材質の面を選ぶ。</summary>
        public const string MaterialToFaces = "materialToFaces";

        /// <summary>選んだ頂点を使う材質の面を選ぶ。</summary>
        public const string VerticesToMaterials = "verticesToMaterials";

        /// <summary>選んだ面を持つ材質の面を全部選ぶ。</summary>
        public const string FacesToMaterials = "facesToMaterials";

        /// <summary>選んだ面を持つ材質の面を、選択から外す。</summary>
        public const string ExcludeFacesMaterials = "excludeFacesMaterials";

        /// <summary>どの面にも使われていない頂点を選ぶ。</summary>
        public const string UnusedVertices = "unusedVertices";

        /// <summary>エッジの倍率が1でない頂点を選ぶ。</summary>
        public const string EdgeScaleChangedVertices = "edgeScaleChangedVertices";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { VerticesToFaces, FacesToVertices, ExpandAdjacentFaces, MaterialToFaces, VerticesToMaterials, FacesToMaterials, ExcludeFacesMaterials, UnusedVertices, EdgeScaleChangedVertices };
            }
        }

        /// <summary>材質の位置を受け取る入力の名前。</summary>
        public const string MaterialIndicesName = "materialIndices";

        /// <summary>選んだ要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
