using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>凍結した除外の組をJSONから読み取る。</summary>
    public static class ExcludedBaselineJsonReader
    {
        /// <summary>
        /// 能力IDの昇順、その中は行キーの昇順で返す。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static IList<ExcludedBaselineEntry> Read(string json)
        {
            throw new NotImplementedException();
        }
    }
}
