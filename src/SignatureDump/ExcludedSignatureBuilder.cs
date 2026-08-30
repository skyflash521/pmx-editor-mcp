using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 提供対象から除く公開シグネチャを一元に決める。生成側も対応表側もこの一覧だけを見るので、
    /// 除外の判断が二重にならない。
    /// </summary>
    public static class ExcludedSignatureBuilder
    {
        public static IList<ExcludedSignatureRecord> Build(
            IList<ExcludedBaselineEntry> baseline, InventoryRecord inventory)
        {
            throw new NotImplementedException();
        }
    }
}
