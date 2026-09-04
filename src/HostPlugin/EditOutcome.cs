using System;

namespace PmxEditorMcp
{
    /// <summary>反映を確定させる呼び出しを境にした、失敗した位置。</summary>
    public enum EditStage
    {
        /// <summary>確定させる呼び出しへ入る前。</summary>
        BeforeCommit,

        /// <summary>確定させる呼び出しそのもの。</summary>
        AtCommit,
    }

    /// <summary>失敗したときにエディタの状態がどうなっているか。</summary>
    public enum EditState
    {
        /// <summary>変わっていない。</summary>
        Unchanged,

        /// <summary>変わったかどうか分からない。</summary>
        Unknown,

        /// <summary>変わっている。</summary>
        Changed,
    }

    /// <summary>
    /// 失敗した位置から、エディタの状態がどうなっているかを決める。呼び出し側が状態を言い当てるので
    /// はなくここで一度に決めることで、ツールごとに言い分がばらつかない。
    /// </summary>
    public static class EditOutcome
    {
        /// <summary>確定させる呼び出しまでの間に失敗したときの状態。</summary>
        public static EditState Resolve(EditStage stage)
        {
            switch (stage)
            {
                case EditStage.BeforeCommit:
                    return EditState.Unchanged;

                case EditStage.AtCommit:
                    return EditState.Unknown;

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, "知らない位置。");
            }
        }

        /// <summary>
        /// 複製編集型の呼び出しが、反映を確定させたあとの段で失敗したときの状態。この段を持つのは
        /// 複製編集型だけなので、ほかの分類のための入口は無い。
        /// </summary>
        public static EditState AfterDuplicateEditCommit()
        {
            return EditState.Changed;
        }

        /// <summary>
        /// 失敗を誤りとして返すか、警告を添えた成功として返すか。確定した後の失敗だけが後者になる
        /// ——反映は済んでいるので、失敗を理由に取り消すことはできない。
        /// </summary>
        public static bool IsFailure(EditState state)
        {
            return state != EditState.Changed;
        }
    }
}
