using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点のウェイトの平均化・平滑化・近いボーンからの設定・鏡像・正規化・修復を行うツール。
    /// </summary>
    public static class ModelEditWeights
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_weights";

        /// <summary>指した頂点のウェイトを、その平均へそろえる。</summary>
        public const string Average = "average";

        /// <summary>隣り合う頂点のウェイトの平均へ、強さのぶんだけ寄せる。</summary>
        public const string Smooth = "smooth";

        /// <summary>いちばん近いボーンへ全部のウェイトを振る。</summary>
        public const string FromNearestBonePosition = "fromNearestBonePosition";

        /// <summary>ボーンの線分へいちばん近いボーンへ全部のウェイトを振る。</summary>
        public const string FromNearestBoneAxis = "fromNearestBoneAxis";

        /// <summary>指した軸の鏡像の位置にある頂点から、左右を入れ替えたウェイトを写す。</summary>
        public const string FromMirror = "fromMirror";

        /// <summary>ウェイトの合計を1にそろえる。</summary>
        public const string Normalize = "normalize";

        /// <summary>並びに居ないボーンを指すウェイトを直す。</summary>
        public const string RepairMissingBone = "repairMissingBone";

        /// <summary>平滑化の強さを受け取る入力の名前。</summary>
        public const string StrengthName = "strength";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Average, Smooth, FromNearestBonePosition, FromNearestBoneAxis, FromMirror, Normalize, RepairMissingBone };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
