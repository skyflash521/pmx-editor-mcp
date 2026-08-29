using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の表を機械可読な行の並びへ写す。台帳と公開APIの一覧を突き合わせる側が名前を
    /// 解決できるよう、対象の列を書き方で分けて、挙げられている名前を取り出しておく。名前が
    /// 型かメンバーかまでは決めない(<see cref="CapabilityRecord.TargetNames"/> を見よ)。
    /// </summary>
    public static class LedgerParser
    {
        /// <summary>台帳の本文から能力の行を取り出す。表の行以外は読み飛ばす。</summary>
        public static IList<CapabilityRecord> Parse(string markdown)
        {
            throw new NotImplementedException();
        }
    }
}
