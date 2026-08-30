using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 除外一覧をJSONへ書き出す。同じ入力からは常に同じバイト列になり、1件が1行に収まるので、
    /// 行単位の差分で除外の増減を追える。
    /// </summary>
    public static class ExcludedSignatureJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(IList<ExcludedSignatureRecord> records)
        {
            throw new NotImplementedException();
        }
    }
}
