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

        public string Tool { get; }

        public UncoveredReason Reason { get; }
    }

    /// <summary>
    /// 実機の検査に覆われないツールの正本。載っているツールが覆われるようになったら、判定はそれを
    /// 食い違いとして落とす。載せるのは名前と理由だけで、言葉で述べた事情は持たない。
    /// </summary>
    public sealed class UncoveredToolTable
    {
        public UncoveredToolTable(IList<UncoveredToolRecord> tools)
        {
            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            for (int at = 1; at < tools.Count; at++)
            {
                int order = string.CompareOrdinal(tools[at - 1].Tool, tools[at].Tool);
                if (order == 0)
                {
                    throw new ArgumentException("同じツールが二度現れる: " + tools[at].Tool, nameof(tools));
                }

                if (order > 0)
                {
                    throw new ArgumentException("序数の昇順で並んでいない: " + tools[at].Tool, nameof(tools));
                }
            }

            Tools = new ReadOnlyCollection<UncoveredToolRecord>(tools);
        }

        public IList<UncoveredToolRecord> Tools { get; }
    }
}
