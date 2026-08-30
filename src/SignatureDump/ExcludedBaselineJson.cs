using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 凍結した除外の組をJSONへ書き出す。同じ入力からは常に同じバイト列になり、配列の要素は
    /// 1行ずつに分かれるので、行単位の差分で変化を追える。
    /// </summary>
    public static class ExcludedBaselineJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(IList<ExcludedBaselineEntry> entries)
        {
            throw new NotImplementedException();
        }
    }
}
