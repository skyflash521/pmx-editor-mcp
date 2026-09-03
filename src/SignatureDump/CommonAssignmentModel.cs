using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>共通契約が受け持つシグネチャを、どこへ割り当てるか。</summary>
    public enum CommonAssignmentKind
    {
        /// <summary>ツールが受け持つ。対象名はツールの名前。</summary>
        Tool,

        /// <summary>ツールの引数として現れる。対象名は共通引数の名前。</summary>
        CommonArg,

        /// <summary>ホストが自分で呼ぶ。対象名は内部フローの名前。</summary>
        InternalFlow,
    }

    /// <summary>戻り値・入力引数・レシーバーの束縛先。</summary>
    public enum BindingSlot
    {
        /// <summary>現在のPMXの複製。</summary>
        PmxClone,

        /// <summary>反映する対象の種別。</summary>
        UpdateKind,

        /// <summary>反映する対象の位置。</summary>
        UpdateIndices,

        /// <summary>反映のときにUndoを積むかどうか。</summary>
        UndoLock,

        /// <summary>接続の根の複製。</summary>
        RunArgsClone,

        /// <summary>接続の根の複製を求めるときに渡すホストプラグイン自身の位置。</summary>
        ModulePath,

        /// <summary>ホストが常駐期間中保持するコネクタ。</summary>
        ResidentObject,

        /// <summary>解放・破棄の対象。</summary>
        TargetHandle,

        /// <summary>呼び出しを受けるコネクタ・親オブジェクト。</summary>
        OwningObject,

        /// <summary>ツールのスキーマへ公開せず、ホストが注入する常駐コネクタ。</summary>
        InjectedConnector,
    }

    /// <summary>シグネチャ1件の束縛。戻り値とレシーバーは持たないことがある。</summary>
    public sealed class SlotBinding
    {
        public SlotBinding(
            BindingSlot? returned,
            BindingSlot? receiver,
            IDictionary<string, BindingSlot> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            Returned = returned;
            Receiver = receiver;
            Parameters = new ReadOnlyDictionary<string, BindingSlot>(
                new Dictionary<string, BindingSlot>(parameters, StringComparer.Ordinal));
        }

        /// <summary>戻り値の束縛。戻り値を持たないシグネチャでは null。</summary>
        public BindingSlot? Returned { get; }

        /// <summary>レシーバーの束縛。静的なシグネチャでは null。</summary>
        public BindingSlot? Receiver { get; }

        /// <summary>入力引数の名前から引く束縛。</summary>
        public IDictionary<string, BindingSlot> Parameters { get; }

        /// <summary>同じ束縛か。読む順に依らず中身で比べる。</summary>
        public bool SameAs(SlotBinding other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (Returned != other.Returned || Receiver != other.Receiver
                || Parameters.Count != other.Parameters.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, BindingSlot> pair in Parameters)
            {
                BindingSlot slot;
                if (!other.Parameters.TryGetValue(pair.Key, out slot) || slot != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>読み手の誤りを指す文にするための書き出し。</summary>
        public override string ToString()
        {
            List<string> parts = new List<string>();
            if (Returned.HasValue)
            {
                parts.Add("戻り値=" + Returned.Value);
            }

            if (Receiver.HasValue)
            {
                parts.Add("レシーバー=" + Receiver.Value);
            }

            foreach (KeyValuePair<string, BindingSlot> pair in Parameters)
            {
                parts.Add(pair.Key + "=" + pair.Value);
            }

            return parts.Count == 0 ? "無し" : string.Join("・", parts);
        }
    }

    /// <summary>共通契約割当の正本の項目1件。</summary>
    public sealed class CommonAssignmentRecord
    {
        public CommonAssignmentRecord(
            string signatureKey,
            CommonAssignmentKind assignment,
            string target,
            SlotBinding slotBinding,
            string basis)
        {
            PropertyRecord.RequireText(signatureKey, nameof(signatureKey));
            PropertyRecord.RequireText(target, nameof(target));
            PropertyRecord.RequireText(basis, nameof(basis));
            if (slotBinding == null)
            {
                throw new ArgumentNullException(nameof(slotBinding));
            }

            SignatureKey = signatureKey;
            Assignment = assignment;
            Target = target;
            SlotBinding = slotBinding;
            Basis = basis;
        }

        public string SignatureKey { get; }

        public CommonAssignmentKind Assignment { get; }

        /// <summary>割当の対象名。</summary>
        public string Target { get; }

        public SlotBinding SlotBinding { get; }

        /// <summary>そう割り当てた根拠の一文。</summary>
        public string Basis { get; }
    }

    /// <summary>共通契約割当の正本。</summary>
    public sealed class CommonAssignmentTable
    {
        public CommonAssignmentTable(IList<CommonAssignmentRecord> assignments)
        {
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            Assignments = new ReadOnlyCollection<CommonAssignmentRecord>(assignments);
        }

        public IList<CommonAssignmentRecord> Assignments { get; }
    }
}
