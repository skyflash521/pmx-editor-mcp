using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class UiOpenWindow
    {
        public const string ToolName = "editor_open_window";

        public const string WindowName = "window";

        public const string AlreadyOpenName = "alreadyOpen";

        public const string TitleName = "title";

        public const string MessagesName = "messages";

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ばれる。</summary>
        public static void AddTo(McpMethodTable methods, Func<IEnumerable<Form>> forms)
        {
            AddTo(methods, forms, named => null);
        }

        /// <summary>
        /// <paramref name="windows"/> はウィンドウの型の完全名から、そのウィンドウを開閉する SDK のコネクタを返す。コネクタの無い
        /// ウィンドウでは null を返す。UIスレッドで呼ばれる。
        /// </summary>
        public static void AddTo(
            McpMethodTable methods, Func<IEnumerable<Form>> forms, Func<string, IPEBaseWindowConnector> windows)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            methods.Add(ToolName, context => Open(context, forms, windows));
        }

        /// <summary>
        /// 開いているウィンドウから <paramref name="named"/> を開くまでに押す部品を、押す順に並べる。各段は
        /// 押す側のウィンドウの名前と、そのウィンドウの根から部品までの名前の連なり。段数の最も少ないものを採る。
        /// 開けないときは null。
        /// </summary>
        internal static IList<KeyValuePair<string, IList<string>>> Hops(string named, Func<string, bool> open)
        {
            Dictionary<string, KeyValuePair<string, IList<string>>> came =
                new Dictionary<string, KeyValuePair<string, IList<string>>>(StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { named };
            Queue<string> next = new Queue<string>();
            next.Enqueue(named);
            while (next.Count > 0)
            {
                string at = next.Dequeue();
                foreach (object one in UiStructureCatalog.Array(
                    UiStructureCatalog.Window(at), UiStructureCatalog.OpenedByName))
                {
                    IDictionary<string, object> route = (IDictionary<string, object>)one;
                    string opener = UiStructureCatalog.Text(route, UiStructureCatalog.FormName);
                    IList<string> path = UiStructureCatalog.Texts(route, UiStructureCatalog.PathName);
                    if (opener == null || !OpensOnly(opener, path, at) || !seen.Add(opener))
                    {
                        continue;
                    }

                    came[opener] = new KeyValuePair<string, IList<string>>(at, path);
                    if (open(opener))
                    {
                        List<KeyValuePair<string, IList<string>>> hops =
                            new List<KeyValuePair<string, IList<string>>>();
                        string from = opener;
                        while (!string.Equals(from, named, StringComparison.Ordinal))
                        {
                            KeyValuePair<string, IList<string>> step = came[from];
                            hops.Add(new KeyValuePair<string, IList<string>>(from, step.Value));
                            from = step.Key;
                        }

                        return hops;
                    }

                    next.Enqueue(opener);
                }
            }

            return null;
        }

        private static bool OpensOnly(string opener, IList<string> path, string named)
        {
            IDictionary<string, object> at = UiStructureCatalog.Node(
                UiStructureCatalog.Window(opener), UiStructureCatalog.RootName);
            foreach (string step in path)
            {
                at = UiStructureCatalog.Child(at, step);
                if (at == null
                    || string.Equals(
                        UiStructureCatalog.Text(at, UiStructureCatalog.TypeName), "ContextMenuStrip",
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            IList<string> opens = UiStructureCatalog.Texts(at, "opens");

            return UiStructureCatalog.Pressable(at)
                && opens.Count == 1
                && string.Equals(opens[0], named, StringComparison.Ordinal);
        }

        private static object Open(
            McpMethodContext context, Func<IEnumerable<Form>> forms, Func<string, IPEBaseWindowConnector> windows)
        {
            object given;
            string named = context.Params.TryGetValue(WindowName, out given) ? given as string : null;
            if (string.IsNullOrEmpty(named) || UiStructureCatalog.Window(named) == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument,
                    WindowName + " はウィンドウの型の完全名で与える。名前は " + UiTree.ToolName + " の form で分かる。");
            }

            IList<KeyValuePair<string, IList<string>>> hops = null;
            IPEBaseWindowConnector connector = null;
            string title = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                List<Form> open = new List<Form>(forms());
                Form shown = UiLive.Shown(open, named);
                if (shown != null)
                {
                    title = shown.Text;

                    return;
                }

                connector = windows(named);
                if (connector == null)
                {
                    hops = Hops(named, opener => UiLive.Shown(open, opener) != null);
                }
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (title != null)
            {
                return Opened(title, true, new string[0]);
            }

            if (connector == null && hops == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "いま開いているウィンドウから、そのウィンドウだけを開くメニュー項目かボタンを辿って開ける道が無い: " + named);
            }

            List<string> notices = new List<string>();
            IList<Func<string>> steps = connector != null
                ? new Func<string>[] { () => Show(connector) }
                : hops.Select(hop => (Func<string>)(() =>
                    UiLive.Press(UiLive.Shown(forms(), hop.Key), hop.Key, hop.Value))).ToList();
            foreach (Func<string> step in steps)
            {
                string waiting = null;
                string refused = null;
                Exception failure = null;
                UiAnswering answered = null;
                invocation = context.Ui.TryInvokeOnUi(() =>
                {
                    waiting = UiAnswering.Waiting();
                    if (waiting != null)
                    {
                        return;
                    }

                    answered = UiAnswering.Around(() =>
                    {
                        try
                        {
                            refused = step();
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

                if (waiting != null)
                {
                    return ToolEnvelope.Failure(
                        ToolEnvelope.NotApplicable,
                        UiAnswering.Told(
                            "エディタが人の応答を待つ表示を出しているので開かない: " + waiting + "。表示が消えたかは "
                                + EditorPrompt.ToolName + " で確かめる。",
                            notices));
                }

                if (failure != null)
                {
                    return ToolEnvelope.Failure(
                        ToolEnvelope.OperationFailed,
                        UiAnswering.Told(answered.Thrown(failure), notices));
                }

                notices.AddRange(answered.Notices);

                if (refused != null)
                {
                    return ToolEnvelope.Failure(ToolEnvelope.NotApplicable, UiAnswering.Told(refused, notices));
                }

                if (answered.Failure != null)
                {
                    return ToolEnvelope.Failure(
                        ToolEnvelope.OperationFailed, UiAnswering.Told(answered.Failure, notices));
                }
            }

            invocation = context.Ui.TryInvokeOnUi(() =>
            {
                Form shown = UiLive.Shown(forms(), named);
                title = shown == null ? null : shown.Text;
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (title == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.OperationFailed,
                    UiAnswering.Told(
                        (connector != null
                            ? "SDK のコネクタで表示を指示したが、そのウィンドウは開かなかった: "
                            : "押したが、そのウィンドウは開かなかった: ") + named,
                        notices));
            }

            return Opened(title, false, notices);
        }

        private static string Show(IPEBaseWindowConnector connector)
        {
            connector.Visible = true;

            return null;
        }

        private static IDictionary<string, object> Opened(string title, bool already, IList<string> notices)
        {
            return ToolEnvelope.Success(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TitleName, title },
                { AlreadyOpenName, already },
                { MessagesName, notices.ToArray() },
            });
        }
    }
}
