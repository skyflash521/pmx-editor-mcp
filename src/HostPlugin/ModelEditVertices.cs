using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の結合・近距離の統合・位置合わせ・鏡像を行うツール。頂点をまとめる操作では、
    /// 3つの頂点が揃わなくなった面を落とす。
    /// </summary>
    public static class ModelEditVertices
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_vertices";

        /// <summary>指した頂点を1つへまとめ、面の参照を付け替える。</summary>
        public const string Weld = "weld";

        /// <summary>指した頂点のうち、しきい値より近いものどうしをまとめる。</summary>
        public const string WeldNear = "weldNear";

        /// <summary>指した頂点の、指した軸の値を、指した頂点の平均へそろえる。</summary>
        public const string Align = "align";

        /// <summary>指した頂点を、指した軸の鏡像として複製する。</summary>
        public const string MirrorCopy = "mirrorCopy";

        /// <summary>指した頂点を、指した軸の鏡像へ移す。</summary>
        public const string MirrorModel = "mirrorModel";

        /// <summary>まとめる距離のしきい値を受け取る入力の名前。</summary>
        public const string ThresholdName = "threshold";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>X軸。</summary>
        public const string AxisX = "x";

        /// <summary>Y軸。</summary>
        public const string AxisY = "y";

        /// <summary>Z軸。</summary>
        public const string AxisZ = "z";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えた頂点の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>3つの頂点が揃わなくなって落ちた面の数を返す項目の名前。</summary>
        public const string RemovedFacesName = "removedFaces";

        /// <summary>足した頂点の位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Weld, WeldNear, Align, MirrorCopy, MirrorModel };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
