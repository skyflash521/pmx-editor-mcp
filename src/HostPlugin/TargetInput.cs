using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要求が持ってきた項目から、要素の集合の指定を読み取る。読み取るだけで、範囲へ収まるかは
    /// <see cref="TargetSelection"/> が見る。位置の配列・始まりと件数の組・全要素・ハンドルの
    /// 配列のどれを持ってきていても、同じ断り方になるようここへ集める。
    /// </summary>
    public static class TargetInput
    {
        /// <summary>
        /// 1つの集合の指定を読む。<paramref name="handles"/> が偽なら、ハンドルの配列は読まない。
        /// 値の形が違えば偽で、断る内容を渡す。
        /// </summary>
        public static bool TryTake(
            IDictionary<string, object> parameters,
            TargetNames names,
            bool handles,
            out TargetRequest request,
            out string code,
            out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 受け取る名前だけが来ているかを見る。知らない名前が1つでもあれば偽で、断る内容を渡す。
        /// </summary>
        public static bool TryOnlyKnown(
            IDictionary<string, object> parameters,
            IList<string> known,
            out string code,
            out string message)
        {
            throw new NotImplementedException();
        }
    }
}
