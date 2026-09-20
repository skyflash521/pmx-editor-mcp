using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した面の向き・対角線・押し出し・共有頂点の分離を行うツール。
    /// </summary>
    public static class ModelEditFaces
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_faces";

        /// <summary>面の表と裏を入れ替える。</summary>
        public const string Flip = "flip";

        /// <summary>辺を共有する2つの面が作る四角の対角線を引き直す。</summary>
        public const string SwapDiagonal = "swapDiagonal";

        /// <summary>
        /// 面を法線の向きへ押し出し、側面を作る。指した面をひとまとまりとして扱い、使っている頂点は
        /// 1つにつき1つだけ複製する。側面を張るのは、指した面のうち1つにしか使われていない辺だけで、
        /// 辺の向きは元の面の巻き順のまま、辺の始まりと終わりと、それぞれを押し出した先で2つの面を作る。
        /// </summary>
        public const string Extrude = "extrude";

        /// <summary>指した面が他の面と共有する頂点を複製して分ける。</summary>
        public const string SeparateSharedVertices = "separateSharedVertices";

        /// <summary>押し出す長さを受け取る入力の名前。</summary>
        public const string DistanceName = "distance";

        /// <summary>変えた面の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>足した頂点の数を返す項目の名前。</summary>
        public const string AddedVerticesName = "addedVertices";

        /// <summary>足した面の数を返す項目の名前。</summary>
        public const string AddedFacesName = "addedFaces";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Flip, SwapDiagonal, Extrude, SeparateSharedVertices };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
