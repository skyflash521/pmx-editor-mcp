using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力をツール化するかどうかの分類。</summary>
    public enum CapabilityStatus
    {
        Provided,
        NotSupported,
        NeedsInvestigation,
    }

    /// <summary>能力を担当するツール契約の区分。</summary>
    public enum CapabilityOwner
    {
        /// <summary>担当を持たない(ツール化しない能力)。</summary>
        None,
        Model,
        Session,
        View,
        MotionTransform,
    }

    /// <summary>対象の列の書き方。指している先が何かではなく、どう書かれているかを表す。</summary>
    public enum CapabilityTargetKind
    {
        /// <summary>名前を1つだけ書いたもの。</summary>
        Single,

        /// <summary>同じ理由でまとめた複数の名前を並べたもの。</summary>
        Group,

        /// <summary>名前空間などをまとめて指す書き方。個々の名前へは機械的に展開できない。</summary>
        Pattern,
    }

    /// <summary>能力台帳の1行。</summary>
    public sealed class CapabilityRecord
    {
        public CapabilityRecord(
            string id,
            string category,
            string target,
            CapabilityTargetKind targetKind,
            IList<string> targetNames,
            CapabilityStatus status,
            CapabilityOwner owner,
            string remarks)
        {
            Id = id;
            Category = category;
            Target = target;
            TargetKind = targetKind;
            TargetNames = targetNames;
            Status = status;
            Owner = owner;
            Remarks = remarks;
        }

        public string Id { get; }

        public string Category { get; }

        /// <summary>対象の列に書かれていた文字列そのもの。</summary>
        public string Target { get; }

        public CapabilityTargetKind TargetKind { get; }

        /// <summary>
        /// 対象が挙げている名前を書かれた順に並べたもの。総称型の型引数の数を表す接尾辞だけを
        /// 取り除き、名前空間や入れ子の区切りはそのまま残す。まとめて指す書き方では空。
        ///
        /// 各名前が型を指すのかメンバーを指すのかは、ここでは決めない。区切りの点は名前空間の
        /// 区切りにも入れ子の型の区切りにもメンバーの区切りにもなるため、公開APIの一覧と
        /// 突き合わせて初めて決まる。字面だけで決めると、入れ子の型を型とメンバーに読み違える。
        /// </summary>
        public IList<string> TargetNames { get; }

        public CapabilityStatus Status { get; }

        public CapabilityOwner Owner { get; }

        public string Remarks { get; }
    }
}
