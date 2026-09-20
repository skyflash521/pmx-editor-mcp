using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// Undoの記録まわりの前置きを済ませてからツールを呼ぶ包み。止めることを頼めない分類が頼んで
    /// いれば断り、止めたまま戻せていないものがあれば、まず戻しにいき、戻らなければ分類ごとの
    /// 決まりで断るか警告を添える。
    /// </summary>
    public sealed class UndoBarrier
    {
        /// <summary>抑止を頼む共通引数の名前。</summary>
        public const string SuppressName = "suppressUndo";

        private readonly UndoRecovery _recovery;

        /// <summary>止めたままの記録を戻しにいく窓口を与えて生成する。</summary>
        public UndoBarrier(UndoRecovery recovery)
        {
            if (recovery == null)
            {
                throw new ArgumentNullException(nameof(recovery));
            }

            _recovery = recovery;
        }

        /// <summary>抑止を頼まれているかを読む。値の形が違えば偽で、断る内容を渡す。</summary>
        public static bool TrySuppress(
            McpMethodContext context, out bool suppress, out string code, out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            code = null;
            message = null;
            suppress = false;
            object value;
            if (!context.Params.TryGetValue(SuppressName, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = SuppressName + " は真偽でなければならない。";

                return false;
            }

            suppress = (bool)value;

            return true;
        }

        /// <summary>
        /// 包みへ知らせを載せる。成功した呼び出しには警告として足し、失敗した呼び出しには誤りの
        /// 説明へ足す。
        /// </summary>
        public static object Noted(object answered, IList<string> notices)
        {
            if (notices == null)
            {
                throw new ArgumentNullException(nameof(notices));
            }

            IDictionary<string, object> envelope = answered as IDictionary<string, object>;
            if (envelope == null)
            {
                return answered;
            }

            Dictionary<string, object> written =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> member in envelope)
            {
                written.Add(member.Key, member.Value);
            }

            object failed;
            IDictionary<string, object> error =
                written.TryGetValue(ToolEnvelope.ErrorName, out failed)
                    ? failed as IDictionary<string, object>
                    : null;
            if (error != null)
            {
                Dictionary<string, object> explained =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> member in error)
                {
                    explained.Add(member.Key, member.Value);
                }

                object said;
                explained[ToolEnvelope.MessageName] =
                    (explained.TryGetValue(ToolEnvelope.MessageName, out said) ? (string)said : null)
                        + string.Concat(notices.Select(n => " " + n));
                written[ToolEnvelope.ErrorName] = explained;

                return written;
            }

            List<string> all = new List<string>();
            object listed;
            if (written.TryGetValue(ToolEnvelope.WarningsName, out listed) && listed is object[])
            {
                all.AddRange(((object[])listed).Select(w => (string)w));
            }

            all.AddRange(notices.Where(n => !all.Contains(n, StringComparer.Ordinal)));
            written[ToolEnvelope.WarningsName] = all.Cast<object>().ToArray();

            return written;
        }

        /// <summary>前置きを済ませてから <paramref name="inner"/> を呼ぶ呼び出しにする。</summary>
        public McpMethod Guard(EditKind kind, McpMethod inner)
        {
            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }

            return context =>
            {
                bool suppress;
                string code;
                string message;
                if (!TrySuppress(context, out suppress, out code, out message)
                    || !UndoGate.TryAcceptSuppress(
                        kind,
                        context.Params.ContainsKey(PmxSession.HandleName),
                        suppress,
                        out code,
                        out message))
                {
                    return ToolEnvelope.Failure(code, message);
                }

                List<string> notices = new List<string>();
                string warning;
                if (!_recovery.TryRecover(context.Ui)
                    && !UndoGate.TryProceedWithLeftover(kind, out code, out message, out warning))
                {
                    return ToolEnvelope.Failure(code, message);
                }

                if (_recovery.TryTakeNotice())
                {
                    notices.Add(UndoGate.RecoveredWarning);
                }

                object answered = inner(context);
                if (_recovery.HasLeftover)
                {
                    notices.Add(UndoGate.LeftoverWarning);
                }

                return notices.Count == 0 ? answered : Noted(answered, notices);
            };
        }
    }
}
