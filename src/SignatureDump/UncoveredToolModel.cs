using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>そのツールが実機の検査に覆われない理由。判定はこの値を導き直して突き合わせる。</summary>
    public enum UncoveredReason
    {
        /// <summary>呼び先まで届いた事例が無い。</summary>
        NoCase,

        /// <summary>
        /// 呼び先まで届いた事例は在るが、そのツールが呼ぶ行のうち、宣言した効果を確かめる事例を
        /// 持たない行がある。1つでも欠ければ当たる。
        /// </summary>
        NoEffectCheck,
    }

    /// <summary>実機の検査に覆われないツール1件。</summary>
    public sealed class UncoveredToolRecord
    {
        public UncoveredToolRecord(string tool, UncoveredReason reason)
        {
            PropertyRecord.RequireText(tool, nameof(tool));

            Tool = tool;
            Reason = reason;
        }

        /// <summary>そのツールの名前。</summary>
        public string Tool { get; }

        /// <summary>覆われない理由。</summary>
        public UncoveredReason Reason { get; }
    }

    /// <summary>
    /// 実機の検査に覆われないツールの正本。覆えないことを1か所へ集めて見えるようにするもので、
    /// 検査を緩めるものではない——載っているツールが覆われるようになったら、判定はそれを食い違いと
    /// して落とす。載せるのは名前と理由だけで、言葉で述べた事情は持たない。判定はその2つを導き直して
    /// 突き合わせるが、言葉は突き合わせる相手を持たないので、書いても本当かどうかを誰も確かめない。
    /// </summary>
    public sealed class UncoveredToolTable
    {
        public UncoveredToolTable(IList<UncoveredToolRecord> tools)
        {
            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            Tools = new ReadOnlyCollection<UncoveredToolRecord>(tools);
        }

        public IList<UncoveredToolRecord> Tools { get; }
    }
}
