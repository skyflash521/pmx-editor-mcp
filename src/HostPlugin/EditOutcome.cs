using System;

namespace PmxEditorMcp
{
    public enum EditKind
    {
        /// <summary>どちらへも作用しない。</summary>
        Read,

        /// <summary>いまの状態を複製して変え、まとめて反映する。</summary>
        DuplicateEdit,

        /// <summary>モデルのデータ・長寿命のオブジェクト・ファイルへ直に作用する。</summary>
        DirectChange,

        /// <summary>表示と設定とセッションだけを動かす。</summary>
        ViewSession,
    }

    /// <summary>反映を確定させる呼び出しを境にした、失敗した位置。</summary>
    public enum EditStage
    {
        BeforeCommit,

        AtCommit,
    }

    public enum EditState
    {
        Unchanged,

        Unknown,

        Changed,
    }

    public static class EditOutcome
    {
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

        public static EditState AfterDuplicateEditCommit()
        {
            return EditState.Changed;
        }

        public static string Describe(EditState state)
        {
            switch (state)
            {
                case EditState.Unchanged:
                    return "状態は未変更である。";

                case EditState.Unknown:
                    return "状態は結果不明で、読み戻さなければ変わったかどうか分からない。";

                case EditState.Changed:
                    return "状態は変更済みである。";

                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "知らない状態。");
            }
        }

        /// <summary>失敗を誤りとして返すか、警告を添えた成功として返すか。確定した後の失敗だけが後者になる。</summary>
        public static bool IsFailure(EditState state)
        {
            return state != EditState.Changed;
        }
    }
}
