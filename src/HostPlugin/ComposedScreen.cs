// 画面とリストへ触る組み立てのツールが通る枠。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>画面へ触るツールが要る相手。要ると言った相手だけが引かれる。</summary>
    [Flags]
    public enum ScreenNeeds
    {
        None = 0,

        View = 1,

        Form = 2,

        Pmx = 4,

        Parts = 8,

        Setting = 16,

        ReadsPmx = Pmx | 32,

        ModelUntouched = 64,
    }

    /// <summary>画面へ触るツールの中身が受け取る相手。要らないと言った相手は空になる。</summary>
    public sealed class ScreenParts
    {
        public ScreenParts(object view, object form, object parts, object pmx, object setting = null)
        {
            View = view;
            Form = form;
            Parts = parts;
            Pmx = pmx;
            Setting = setting;
        }

        public object View { get; }

        public object Form { get; }

        public object Parts { get; }

        public object Pmx { get; }

        public object Setting { get; }
    }

    /// <summary>
    /// 画面とリストへ触る組み立てのツールを、UIスレッドの上で呼ぶ枠。まとめての反映も取り消しの
    /// 抑止も通さず、どのPMXを相手にするかの指定も受け取らない。
    /// </summary>
    public sealed class ComposedScreen
    {
        private readonly PmxSession _session;

        private readonly Func<object> _view;

        private readonly Func<object> _form;

        private readonly Func<object> _parts;

        private readonly Func<object> _setting;

        private readonly ScreenRefresh _refresh;

        public ComposedScreen(
            PmxSession session,
            Func<object> view,
            Func<object> form,
            Func<object> parts,
            ScreenRefresh refresh,
            Func<object> setting = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            if (refresh == null)
            {
                throw new ArgumentNullException(nameof(refresh));
            }

            _refresh = refresh;
            _session = session;
            _view = view;
            _form = form;
            _parts = parts;
            _setting = setting ?? (() => null);
        }

        /// <summary>
        /// <paramref name="body"/> を、画面のコネクタとPMXを受け取る呼び出しにする。
        /// <paramref name="known"/> はそのツールが受け取る項目の名前、<paramref name="needs"/> は
        /// そのツールが要る相手で、引けない相手が1つでもあればそのツールだけを断る。
        /// <paramref name="refresh"/> は、済んだあとに画面へ映すのに要ることで、断った呼び出しでは
        /// 行わない。<paramref name="body"/> はUIスレッドの上で呼ばれる。
        /// </summary>
        public McpMethod Method(
            IList<string> known,
            ScreenNeeds needs,
            ScreenRefreshKind refresh,
            Func<McpMethodContext, ScreenParts, ComposedEditResult> body)
        {
            if (known == null)
            {
                throw new ArgumentNullException(nameof(known));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            return context => Run(context, known, needs, body, refresh);
        }

        private object Run(
            McpMethodContext context,
            IList<string> names,
            ScreenNeeds needs,
            Func<McpMethodContext, ScreenParts, ComposedEditResult> body,
            ScreenRefreshKind refresh)
        {
            string code;
            string message;
            if (!TargetInput.TryOnlyKnown(context.Params, names, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            ComposedEditResult answered = null;
            string refusedCode = null;
            string refusedMessage = null;
            bool shown = true;
            Exception caught = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                try
                {
                    if (Wanted(needs, ScreenNeeds.ModelUntouched))
                    {
                        _session.NoteRead();
                    }

                    object view = Wanted(needs, ScreenNeeds.View) ? _view() : null;
                    object form = Wanted(needs, ScreenNeeds.Form) ? _form() : null;
                    object parts = Wanted(needs, ScreenNeeds.Parts) ? _parts() : null;
                    object setting = Wanted(needs, ScreenNeeds.Setting) ? _setting() : null;
                    if (view == null && Wanted(needs, ScreenNeeds.View))
                    {
                        Refuse("3Dビューを取得できない。", out refusedCode, out refusedMessage);

                        return;
                    }

                    if (form == null && Wanted(needs, ScreenNeeds.Form))
                    {
                        Refuse("リストのフォームを取得できない。", out refusedCode, out refusedMessage);

                        return;
                    }

                    if (parts == null && Wanted(needs, ScreenNeeds.Parts))
                    {
                        Refuse("絞込みのウィンドウを取得できない。", out refusedCode, out refusedMessage);

                        return;
                    }

                    if (setting == null && Wanted(needs, ScreenNeeds.Setting))
                    {
                        Refuse("ビューの表示の設定を取得できない。", out refusedCode, out refusedMessage);

                        return;
                    }

                    object pmx = null;
                    PmxTarget target;
                    if (Wanted(needs, ScreenNeeds.Pmx))
                    {
                        if (!_session.TryTake(
                            null,
                            context.Handles,
                            out target,
                            out refusedCode,
                            out refusedMessage,
                            reading: Wanted(needs, ScreenNeeds.ReadsPmx)))
                        {
                            return;
                        }

                        pmx = target.Pmx;
                    }

                    ComposedEditResult made =
                        body(context, new ScreenParts(view, form, parts, pmx, setting));
                    if (!made.IsDone)
                    {
                        refusedCode = made.Code;
                        refusedMessage = made.Message;

                        return;
                    }

                    answered = made;
                    shown = _refresh.Apply(refresh);
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
                return ToolFailure.Failed(caught, EditStage.BeforeCommit);
            }

            return answered == null
                ? ToolEnvelope.Failure(refusedCode, refusedMessage)
                : ToolEnvelope.Success(answered.Value, ScreenRefresh.Noted(answered.Warnings, shown));
        }

        private static bool Wanted(ScreenNeeds needs, ScreenNeeds one)
        {
            return (needs & one) == one;
        }

        private static void Refuse(string said, out string code, out string message)
        {
            code = ToolEnvelope.NotApplicable;
            message = said;
        }
    }
}
