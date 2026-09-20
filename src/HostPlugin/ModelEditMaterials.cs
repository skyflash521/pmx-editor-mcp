using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した材質の結合・単一化・分割・取り出し・複製・色の丸めを行うツール。
    /// </summary>
    public static class ModelEditMaterials
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_materials";

        /// <summary>指した材質を、先頭の材質へまとめる。</summary>
        public const string Merge = "merge";

        /// <summary>モデルの全部の材質を1つへまとめる。</summary>
        public const string MergeAll = "mergeAll";

        /// <summary>色の許容幅の中で設定が同じ材質どうしをまとめる。</summary>
        public const string MergeSame = "mergeSame";

        /// <summary>指した面を新しい材質へ移す。</summary>
        public const string ExtractFaces = "extractFaces";

        /// <summary>材質の持ち物を複製して新しい材質にする。</summary>
        public const string DuplicateParts = "duplicateParts";

        /// <summary>材質の色の各成分を0以上1以下へ丸める。</summary>
        public const string ClampColor = "clampColor";

        /// <summary>同じ材質と見なす色の許容幅を受け取る入力の名前。</summary>
        public const string ColorToleranceName = "colorTolerance";

        /// <summary>複製する持ち物を受け取る入力の名前。</summary>
        public const string PartsName = "parts";

        /// <summary>面だけを複製する。</summary>
        public const string FacesOnly = "facesOnly";

        /// <summary>面と頂点を複製する。</summary>
        public const string WithVertices = "withVertices";

        /// <summary>面と頂点と関連するモーフを複製する。</summary>
        public const string WithMorphs = "withMorphs";

        /// <summary>材質の中の面を位置の配列で指す入力の名前。</summary>
        public const string FaceIndicesName = "faceIndices";

        /// <summary>材質の中の面を始まりと件数で指す入力の名前。</summary>
        public const string FaceRangeName = "faceRange";

        /// <summary>材質の中の面を全部指す入力の名前。</summary>
        public const string FaceAllName = "faceAll";

        /// <summary>変えた材質の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えた材質の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>足した材質の位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Merge, MergeAll, MergeSame, ExtractFaces, DuplicateParts, ClampColor };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
