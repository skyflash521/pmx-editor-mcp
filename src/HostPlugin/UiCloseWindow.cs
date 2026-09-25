using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class UiCloseWindow
    {
        public const string ToolName = "editor_close_window";

        public const string WindowName = "window";

        public const string AlreadyClosedName = "alreadyClosed";

        public const string ShutdownToolName = "session_close";

        public const string MessagesName = "messages";

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ばれる。</summary>
        public static void AddTo(McpMethodTable methods, Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            methods.Add(ToolName, context => Close(context, forms));
        }

        private static object Close(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            object given;
            string named = context.Params.TryGetValue(WindowName, out given) ? given as string : null;
            if (string.IsNullOrEmpty(named) || UiStructureCatalog.Window(named) == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument,
                    WindowName + " はウィンドウの型の完全名で与える。名前は " + UiTree.ToolName + " の form で分かる。");
            }

            if (string.Equals(named, UiStructureCatalog.MainForm, StringComparison.Ordinal))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    named + "(Pmx編集)はエディタの主画面で、閉じるとエディタが終了するので閉じない。"
                        + "エディタを終了するなら " + ShutdownToolName + " を使う。");
            }

            bool shown = false;
            bool reopenable = false;
            bool closed = false;
            string waiting = null;
            UiAnswering answered = null;
            Exception failure = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                List<Form> open = new List<Form>(forms());
                Form form = UiLive.Shown(open, named);
                if (form == null)
                {
                    return;
                }

                shown = true;
                reopenable = UiOpenWindow.Hops(named, opener => UiLive.Shown(open, opener) != null) != null;
                if (!reopenable)
                {
                    return;
                }

                waiting = UiAnswering.Waiting();
                if (waiting != null)
                {
                    return;
                }

                answered = UiAnswering.Around(() =>
                {
                    try
                    {
                        form.Close();
                        closed = !form.Visible;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (!shown)
            {
                return Closed(true, new string[0]);
            }

            if (!reopenable)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "閉じると " + UiOpenWindow.ToolName + " で開き直せないので、閉じない: " + named);
            }

            if (waiting != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "エディタが人の応答を待つ表示を出しているので閉じない: " + waiting + "。表示が消えたかは "
                        + EditorPrompt.ToolName + " で確かめる。");
            }

            if (failure != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.OperationFailed, answered.Thrown(failure));
            }

            if (answered.Failure != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.OperationFailed, UiAnswering.Told(answered.Failure, answered.Notices));
            }

            if (!closed)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.OperationFailed,
                    UiAnswering.Told(
                        "閉じる処理をエディタが取りやめ、ウィンドウは開いたままである: " + named, answered.Notices));
            }

            return Closed(false, answered.Notices);
        }

        private static IDictionary<string, object> Closed(bool already, IList<string> notices)
        {
            return ToolEnvelope.Success(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { AlreadyClosedName, already },
                { MessagesName, notices.ToArray() },
            });
        }
    }
}
