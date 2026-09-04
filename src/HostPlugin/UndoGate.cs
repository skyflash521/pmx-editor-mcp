using System;

namespace PmxEditorMcp
{
    /// <summary>Undoの抑止を頼まれた呼び出しと、戻せていない記録が残る呼び出しの扱いを決める。</summary>
    public static class UndoGate
    {
        /// <summary>
        /// 抑止を頼める呼び出しかを見る。頼めない分類が頼んでいれば偽を返し、断る内容を渡す。
        /// 真を返したときは <paramref name="code"/> も <paramref name="message"/> も持たない。
        /// </summary>
        public static bool TryAcceptSuppress(
            EditKind kind, bool suppressUndo, out string code, out string message)
        {
            code = null;
            message = null;
            switch (kind)
            {
                case EditKind.DuplicateEdit:
                    return true;

                case EditKind.Read:
                case EditKind.DirectChange:
                case EditKind.ViewSession:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない分類。");
            }

            if (!suppressUndo)
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = "suppressUndo を頼めるのは複製編集型の呼び出しだけである。"
                + "まとめて反映する呼び出しを持たないので、頼んでも何も起きない。";

            return false;
        }

        /// <summary>
        /// 戻せていない記録が残ったまま呼び出すときの扱い。真を返したときは
        /// <paramref name="warning"/> だけを、偽を返したときは <paramref name="code"/> と
        /// <paramref name="message"/> だけを渡す。
        /// </summary>
        public static bool TryProceedWithLeftover(
            EditKind kind, out string code, out string message, out string warning)
        {
            code = null;
            message = null;
            warning = "Undoの記録を止めたまま戻せていない。";
            switch (kind)
            {
                case EditKind.Read:
                case EditKind.ViewSession:
                    return true;

                case EditKind.DuplicateEdit:
                case EditKind.DirectChange:
                    warning = null;
                    code = ToolEnvelope.OperationFailed;
                    message = "Undoの記録を止めたまま戻せていないので、実行しない。"
                        + "この状態で変えると、その変更をどれも戻せなくなる。状態は未変更である。";

                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない分類。");
            }
        }
    }
}
