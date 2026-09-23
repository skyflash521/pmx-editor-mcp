using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class UiOpenWindow
    {
        public const string ToolName = "editor_open_window";

        public const string WindowName = "window";

        public const string AlreadyOpenName = "alreadyOpen";

        public const string TitleName = "title";

        private static readonly HashSet<string> Pressable = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToolStripMenuItem", "ToolStripButton", "Button",
        };

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

            methods.Add(ToolName, context => Open(context, forms));
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
                at = Child(at, step);
                if (at == null
                    || string.Equals(
                        UiStructureCatalog.Text(at, UiStructureCatalog.TypeName), "ContextMenuStrip",
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            IList<string> opens = UiStructureCatalog.Texts(at, "opens");

            return Pressable.Contains(UiStructureCatalog.Text(at, UiStructureCatalog.TypeName) ?? string.Empty)
                && opens.Count == 1
                && string.Equals(opens[0], named, StringComparison.Ordinal);
        }

        private static IDictionary<string, object> Child(IDictionary<string, object> node, string name)
        {
            foreach (IDictionary<string, object> child in UiStructureCatalog.Children(node))
            {
                if (string.Equals(
                    UiStructureCatalog.Text(child, UiStructureCatalog.NameName), name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static object Open(McpMethodContext context, Func<IEnumerable<Form>> forms)
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

                hops = Hops(named, opener => UiLive.Shown(open, opener) != null);
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (title != null)
            {
                return Opened(title, true);
            }

            if (hops == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "いま開いているウィンドウから、そのウィンドウだけを開くメニュー項目かボタンを辿って開ける道が無い: " + named);
            }

            foreach (KeyValuePair<string, IList<string>> hop in hops)
            {
                string refused = null;
                Exception failure = null;
                invocation = context.Ui.TryInvokeOnUi(() =>
                {
                    try
                    {
                        refused = Press(forms, hop.Key, hop.Value);
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });
                if (!invocation.DidRun)
                {
                    return ToolFailure.Unavailable(invocation);
                }

                if (failure != null)
                {
                    return ToolEnvelope.Failure(ToolEnvelope.OperationFailed, failure.Message);
                }

                if (refused != null)
                {
                    return ToolEnvelope.Failure(ToolEnvelope.NotApplicable, refused);
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
                    ToolEnvelope.OperationFailed, "押したが、そのウィンドウは開かなかった: " + named);
            }

            return Opened(title, false);
        }

        /// <summary>部品を押す。押せなければその事情を返し、押したら null。</summary>
        private static string Press(Func<IEnumerable<Form>> forms, string opener, IList<string> path)
        {
            Form form = UiLive.Shown(forms(), opener);
            List<UiMenu> menus = new List<UiMenu>();
            object part = form == null ? null : UiLive.Find(form, path, menus);
            if (part == null)
            {
                return "開く道筋の部品が見つからない: " + opener + " の " + string.Join("/", path);
            }

            bool enabled;
            try
            {
                foreach (UiMenu menu in menus)
                {
                    menu.Open();
                }

                ToolStripItem item = part as ToolStripItem;
                enabled = item != null ? item.Enabled && item.Available : ((Control)part).Enabled;
            }
            finally
            {
                for (int at = menus.Count - 1; at >= 0; at--)
                {
                    menus[at].Close();
                }
            }

            if (!enabled)
            {
                return "開く道筋の部品がいまは押せない: " + opener + " の " + string.Join("/", path);
            }

            ToolStripItem pressed = part as ToolStripItem;
            if (pressed != null)
            {
                pressed.PerformClick();
            }
            else
            {
                ((Button)part).PerformClick();
            }

            return null;
        }

        private static IDictionary<string, object> Opened(string title, bool already)
        {
            return ToolEnvelope.Success(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TitleName, title },
                { AlreadyOpenName, already },
            });
        }
    }
}
