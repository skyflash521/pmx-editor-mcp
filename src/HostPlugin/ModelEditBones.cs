using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指したボーンの整理と生成と軸の設定を行うツール。
    /// </summary>
    public static class ModelEditBones
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_bones";

        /// <summary>同じ名前のボーンを先頭の1つへまとめる。</summary>
        public const string MergeSameName = "mergeSameName";

        /// <summary>子を持たないボーンを操作できない表示にする。</summary>
        public const string HideTipBones = "hideTipBones";

        /// <summary>表示先のボーン指定を、そのボーンまでの隔たりへ移す。</summary>
        public const string TipToOffset = "tipToOffset";

        /// <summary>表示先の隔たりを、いちばん近い子のボーン指定へ移す。</summary>
        public const string OffsetToTip = "offsetToTip";

        /// <summary>親より先に子が来ないよう並びを組み直す。</summary>
        public const string RelevelHierarchy = "relevelHierarchy";

        /// <summary>親を持たないボーンの上に、1つの親を足す。</summary>
        public const string AddRootParent = "addRootParent";

        /// <summary>指したボーンの上に、同じ位置の親を足す。</summary>
        public const string AddMultiStageParent = "addMultiStageParent";

        /// <summary>指したボーンの下に、同じ位置の子を足す。</summary>
        public const string AddMultiStageChild = "addMultiStageChild";

        /// <summary>指したボーンとその親の中間へ、ボーンを足す。</summary>
        public const string AddMiddle = "addMiddle";

        /// <summary>指したボーンの上に、そのボーンを付与の元にする親を足す。</summary>
        public const string AddAppendParent = "addAppendParent";

        /// <summary>指した頂点の重心へボーンを1つ足す。</summary>
        public const string AddAtVertices = "addAtVertices";

        /// <summary>指したボーンをIKの先とするIKボーンを足す。</summary>
        public const string MakeIk = "makeIk";

        /// <summary>名前の左右が逆のボーンの位置を、鏡像へそろえる。</summary>
        public const string MirrorPosition = "mirrorPosition";

        /// <summary>軸の制限の向きを、表示先への向きにする。</summary>
        public const string FixAxisToTip = "fixAxisToTip";

        /// <summary>ローカル軸を、表示先への向きと親からの向きで決める。</summary>
        public const string SetLocalAxis = "setLocalAxis";

        /// <summary>ローカル軸の指定を外す。</summary>
        public const string ResetLocalAxis = "resetLocalAxis";

        /// <summary>PMDのボーン種別を、いまの設定から決め直す。</summary>
        public const string SetPmdBoneKind = "setPmdBoneKind";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { MergeSameName, HideTipBones, TipToOffset, OffsetToTip, RelevelHierarchy, AddRootParent, AddMultiStageParent, AddMultiStageChild, AddMiddle, AddAppendParent, AddAtVertices, MakeIk, MirrorPosition, FixAxisToTip, SetLocalAxis, ResetLocalAxis, SetPmdBoneKind };
            }
        }

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>IKが辿るリンクの数を受け取る入力の名前。</summary>
        public const string LinkCountName = "linkCount";

        /// <summary>足したボーンの位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えたボーンの数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えたボーンの数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            throw new NotImplementedException();
        }
    }
}
