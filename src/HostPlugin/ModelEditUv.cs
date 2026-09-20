using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点のUVの反転・写し取り・視線方向からの貼り直しを行うツール。
    /// </summary>
    public static class ModelEditUv
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_uv";

        /// <summary>UのUVを反転する。</summary>
        public const string FlipU = "flipU";

        /// <summary>VのUVを反転する。</summary>
        public const string FlipV = "flipV";

        /// <summary>UとVの両方を反転する。</summary>
        public const string FlipUV = "flipUV";

        /// <summary>写す元の頂点のUVを、指した頂点へ写す。</summary>
        public const string Copy = "copy";

        /// <summary>
        /// 指した向きから見た位置でUVを貼り直す。Uの向きは、上の向きと見る向きの外積を正規化した
        /// もので、上の向きには0,1,0を採り、それが見る向きと平行なときだけ0,0,1を採る。Vの向きは
        /// 見る向きとUの向きの外積を正規化したもので、UVは頂点の位置をこの2つの向きへ落とした値に
        /// する。長さの無い向きは受け取らない。
        /// </summary>
        public const string ProjectFromView = "projectFromView";

        /// <summary>UVを写す元の頂点の位置を受け取る入力の名前。</summary>
        public const string SourceName = "source";

        /// <summary>見る向きを受け取る入力の名前。</summary>
        public const string DirectionName = "direction";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { FlipU, FlipV, FlipUV, Copy, ProjectFromView };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
