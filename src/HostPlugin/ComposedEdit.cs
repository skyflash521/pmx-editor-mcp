using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>組み立てたツールの中身が返す結末。値を返すか、断る内容を返すかのどちらか。</summary>
    public sealed class ComposedEditResult
    {
        private ComposedEditResult(bool done, object value, string code, string message)
        {
            IsDone = done;
            Value = value;
            Code = code;
            Message = message;
        }

        /// <summary>済んだか。偽なら断っている。</summary>
        public bool IsDone { get; }

        /// <summary>済んだときに返す中身。</summary>
        public object Value { get; }

        /// <summary>断ったときの誤りの符号。</summary>
        public string Code { get; }

        /// <summary>断ったときの説明。</summary>
        public string Message { get; }

        /// <summary>済んだ結末を作る。</summary>
        public static ComposedEditResult Complete(object value)
        {
            return new ComposedEditResult(true, value, null, null);
        }

        /// <summary>断る結末を作る。</summary>
        public static ComposedEditResult Refuse(string code, string message)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new ComposedEditResult(false, null, code, message);
        }
    }

    /// <summary>
    /// 1メンバーへ写らない組み立てのツールを、生成したツールと同じ複製編集の経路へ乗せる枠。
    /// どのPMXを相手にするかの解決・UIスレッドへの委譲・まとめての反映・失敗したときの状態の
    /// 言い方はここが引き受ける。
    /// </summary>
    public sealed class ComposedEdit
    {
        private readonly PmxSession _session;

        private readonly UndoBarrier _barrier;

        /// <summary>複製編集の流れと、Undoの前置きの包みを与えて生成する。</summary>
        public ComposedEdit(PmxSession session, UndoBarrier barrier)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (barrier == null)
            {
                throw new ArgumentNullException(nameof(barrier));
            }

            _session = session;
            _barrier = barrier;
        }

        /// <summary>
        /// <paramref name="body"/> を複製編集の経路へ乗せた呼び出しにする。
        /// <paramref name="known"/> はそのツールが受け取る項目の名前で、どのPMXを相手にするかの
        /// 指定とUndoの抑止の頼みはここが足す。<paramref name="body"/> はUIスレッドの上で、
        /// 相手にするPMXを受け取って呼ばれる。
        /// </summary>
        public McpMethod Method(
            IList<string> known, Func<McpMethodContext, object, ComposedEditResult> body)
        {
            if (known == null)
            {
                throw new ArgumentNullException(nameof(known));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            List<string> names = new List<string>(known)
            {
                UndoBarrier.SuppressName,
                PmxSession.HandleName,
            };

            return _barrier.Guard(EditKind.DuplicateEdit, context => Run(context, names, body));
        }

        private object Run(
            McpMethodContext context,
            IList<string> names,
            Func<McpMethodContext, object, ComposedEditResult> body)
        {
            string code;
            string message;
            long? handle;
            bool suppress;
            if (!TargetInput.TryOnlyKnown(context.Params, names, out code, out message)
                || !TryHandle(context, out handle, out code, out message)
                || !UndoBarrier.TrySuppress(context, out suppress, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            ComposedEditResult answered = null;
            string refusedCode = null;
            string refusedMessage = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception caught = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                try
                {
                    PmxTarget target;
                    if (!_session.TryTake(
                        handle, context.Handles, out target, out refusedCode, out refusedMessage))
                    {
                        return;
                    }

                    stage = target.Current ? EditStage.BeforeCommit : EditStage.AtCommit;
                    ComposedEditResult made = body(context, target.Pmx);
                    if (!made.IsDone)
                    {
                        refusedCode = made.Code;
                        refusedMessage = made.Message;

                        return;
                    }

                    if (target.Current)
                    {
                        stage = EditStage.AtCommit;
                    }

                    if (_session.TryCommit(target, suppress, out refusedCode, out refusedMessage))
                    {
                        answered = made;
                    }
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });

            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (caught != null)
            {
                return ToolFailure.Failed(caught, stage);
            }

            return answered == null
                ? ToolEnvelope.Failure(refusedCode, refusedMessage)
                : ToolEnvelope.Success(answered.Value);
        }

        private static bool TryHandle(
            McpMethodContext context, out long? handle, out string code, out string message)
        {
            code = null;
            message = null;
            handle = null;
            object value;
            if (!context.Params.TryGetValue(PmxSession.HandleName, out value))
            {
                return true;
            }

            long taken;
            if (!ValueInput.TryInteger(value, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = PmxSession.HandleName + " は整数でなければならない。";

                return false;
            }

            handle = taken;

            return true;
        }
    }
}
