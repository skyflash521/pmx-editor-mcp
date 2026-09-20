using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の法線の平均化・面法線化・正規化・反転を行うツール。
    /// </summary>
    public static class ModelEditNormals
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_normals";

        /// <summary>指した頂点の法線を、その平均へそろえる。</summary>
        public const string Average = "average";

        /// <summary>しきい値より近い頂点どうしで法線を平均する。</summary>
        public const string AverageNear = "averageNear";

        /// <summary>その頂点を使う面の法線の平均を、頂点の法線にする。</summary>
        public const string FromFaces = "fromFaces";

        /// <summary>法線の長さを1にそろえる。</summary>
        public const string Normalize = "normalize";

        /// <summary>法線の向きを逆にする。</summary>
        public const string Flip = "flip";

        /// <summary>平均する距離のしきい値を受け取る入力の名前。</summary>
        public const string ThresholdName = "threshold";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Average, AverageNear, FromFaces, Normalize, Flip };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
