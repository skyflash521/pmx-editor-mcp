using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の非対応記載を、公開シグネチャの集合として確定する。台帳の備考は後から書き足せる
    /// ので、確定した集合の側を正本にして、以後の除外がこの集合を超えて広がらないようにする。
    /// </summary>
    public static class ExcludedBaselineBuilder
    {
        public static IList<ExcludedBaselineEntry> Build(
            IList<CapabilityRecord> ledger, IList<SignatureRecord> signatures)
        {
            throw new NotImplementedException();
        }
    }
}
