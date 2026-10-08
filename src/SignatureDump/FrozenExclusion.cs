using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 凍結した除外の組の1能力。凍結した時点で台帳がその能力をどう記していたかと、どのシグネチャを
    /// 選ぶかを持つ。行キーも型も挙げない能力は、台帳のまとめて指す書き方が選ぶ。
    /// </summary>
    public sealed class FrozenExclusion
    {
        public FrozenExclusion(
            string capabilityId,
            CapabilityStatus status,
            string target,
            string remarks,
            IList<string> signatures,
            IList<string> types)
        {
            PropertyRecord.RequireText(capabilityId, nameof(capabilityId));
            PropertyRecord.RequireText(target, nameof(target));
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            CapabilityId = capabilityId;
            Status = status;
            Target = target;
            Remarks = remarks;
            Signatures = new ReadOnlyCollection<string>(signatures);
            Types = new ReadOnlyCollection<string>(types);
        }

        public string CapabilityId { get; }

        public CapabilityStatus Status { get; }

        public string Target { get; }

        /// <summary>分類が提供の能力だけが持ち、ほかは null。</summary>
        public string Remarks { get; }

        /// <summary>行キーで挙げたシグネチャ。</summary>
        public IList<string> Signatures { get; }

        /// <summary>挙げた型。その型が宣言する全メンバーを選ぶ。</summary>
        public IList<string> Types { get; }
    }
}
