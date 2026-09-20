using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の変形方式を変え、SDEFの値を整えるツール。
    /// </summary>
    public static class ModelSetDeformType
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_set_deform_type";

        /// <summary>
        /// 指した変形方式へ変える。重みが正の枠を重い順に、方式が使う数まで残し、残した重みの合計が
        /// 1になるようそろえ、余った枠は空にする。正の枠が1つも無ければ、その頂点が指している
        /// ボーンのうち先頭のものへ重み1を振り、ボーンを1つも指していなければ枠を空のままにする。
        /// SDEFの指定は残った枠が2つのときだけ、QDEFの指定は残った枠が1つ以上のときだけ立て、
        /// もう一方の指定は落とす。
        /// </summary>
        public const string Convert = "convert";

        /// <summary>SDEFのC値を、ウェイトの2つのボーンの位置から決め直す。</summary>
        public const string NormalizeSdefC = "normalizeSdefC";

        /// <summary>2つのボーンを保てないSDEFの頂点から、SDEFの指定を落とす。</summary>
        public const string RepairInvalidSdef = "repairInvalidSdef";

        /// <summary>変える先の変形方式を受け取る入力の名前。</summary>
        public const string DeformName = "deform";

        /// <summary>1つのボーンだけで変形する。</summary>
        public const string Bdef1 = "bdef1";

        /// <summary>2つのボーンで変形する。</summary>
        public const string Bdef2 = "bdef2";

        /// <summary>4つのボーンで変形する。</summary>
        public const string Bdef4 = "bdef4";

        /// <summary>2つのボーンと補間の値で変形する。</summary>
        public const string Sdef = "sdef";

        /// <summary>4つのボーンを二重四元数で混ぜて変形する。</summary>
        public const string Qdef = "qdef";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Convert, NormalizeSdefC, RepairInvalidSdef };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
