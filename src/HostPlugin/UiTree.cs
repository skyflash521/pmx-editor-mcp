using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class UiTree
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "editor_get_screen_structure";

        /// <summary>ウィンドウを選ぶ入力の名前。省くと一覧を返す。</summary>
        public const string WindowName = "window";

        /// <summary>木の根から降りる名前の連なりを受け取る入力の名前。</summary>
        public const string PathName = "path";

        /// <summary>返す深さを受け取る入力の名前。</summary>
        public const string DepthName = "depth";

        /// <summary>並びの何番目から返すかを受け取る入力の名前。</summary>
        public const string OffsetName = "offset";

        /// <summary>降りられる深さの上限。</summary>
        public const int MaxDepth = 100;

        private const string NodeName = "node";

        private const string OpenName = "open";

        private const string TotalName = "total";

        private const string NextOffsetName = "nextOffset";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <param name="forms">開いているウィンドウを返す。UIスレッドで呼ばれる。</param>
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

            methods.Add(ToolName, context => Get(context, forms));
        }

        private static object Get(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            object given;
            string wanted = context.Params.TryGetValue(WindowName, out given) ? given as string : null;
            if (given != null && wanted == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, WindowName + " は文字列でなければならない。");
            }

            IList<string> path = Steps(context);
            if (path == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, PathName + " は文字列の並びでなければならない。");
            }

            int depth;
            string message;
            if (!TryDepth(context, out depth, out message))
            {
                return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, message);
            }

            int offset;
            if (!TryOffset(context, out offset, out message))
            {
                return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, message);
            }

            bool named;
            IList<IDictionary<string, object>> found = Choose(wanted, out named);
            List<IDictionary<string, object>> chosen = new List<IDictionary<string, object>>();
            for (int at = offset; at < found.Count; at++)
            {
                chosen.Add(found[at]);
            }

            HashSet<string> collected = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);
                foreach (Form form in forms())
                {
                    if (form.Visible)
                    {
                        shown.Add(form.Name);
                    }
                }

                collected = shown;
            });
            HashSet<string> open = invocation.DidRun ? collected : null;
            List<string> warnings = new List<string>();
            if (open == null)
            {
                warnings.Add("開いているかを読めなかったので open を添えない: " + invocation.Unavailable);
            }

            List<object> windows = new List<object>();
            foreach (IDictionary<string, object> window in chosen)
            {
                IDictionary<string, object> listed = Listed(window, named);
                if (open != null)
                {
                    listed.Add(
                        OpenName,
                        open.Contains(UiStructureCatalog.Text(
                            UiStructureCatalog.Node(window, UiStructureCatalog.RootName),
                            UiStructureCatalog.NameName) ?? string.Empty));
                }

                if (named)
                {
                    IDictionary<string, object> node =
                        UiStructureCatalog.Node(window, UiStructureCatalog.RootName);
                    foreach (string step in path)
                    {
                        node = UiStructureCatalog.Child(node, step);
                        if (node == null)
                        {
                            return ToolEnvelope.Failure(
                                ToolEnvelope.InvalidArgument,
                                PathName + " に当たる部品が無い: " + step);
                        }
                    }

                    listed.Add(NodeName, Cut(node, depth));
                    object[] messages = UiStructureCatalog.Array(window, UiStructureCatalog.MessagesName);
                    if (messages.Length > 0)
                    {
                        listed.Add(UiStructureCatalog.MessagesName, messages);
                    }
                }

                windows.Add(listed);
            }

            int room = ResponseSize.ValueChars(context.BudgetChars);

            return named
                ? Whole(windows, chosen, found.Count, offset, room, warnings)
                : Some(windows, found.Count, offset, room, warnings);
        }

        /// <summary>木を付けた答え。収まらないときは絞り方を添えたエラーを返す。</summary>
        private static object Whole(
            IList<object> windows,
            IList<IDictionary<string, object>> chosen,
            int total,
            int offset,
            int room,
            IList<string> warnings)
        {
            IDictionary<string, object> value = Value(windows, total, offset);
            if (Serializer.Serialize(value).Length <= room)
            {
                return ToolEnvelope.Success(value, warnings);
            }

            if (chosen.Count > 1)
            {
                List<string> forms = new List<string>();
                foreach (IDictionary<string, object> window in chosen)
                {
                    forms.Add(UiStructureCatalog.Text(window, UiStructureCatalog.FormName));
                }

                return ToolEnvelope.Failure(
                    ToolEnvelope.ResponseTooLarge,
                    "当たったウィンドウの木が値の枠に収まらない。" + WindowName + " を型の完全名で1つに絞る: "
                        + string.Join("、", forms.ToArray()));
            }

            return ToolEnvelope.Failure(
                ToolEnvelope.ResponseTooLarge,
                "木が値の枠に収まらない。" + PathName + " で降りて取り直す。");
        }

        /// <summary>木を付けない一覧。収まるところまでを返し、残りは次の offset で取り直せる。</summary>
        private static object Some(IList<object> windows, int total, int offset, int room, IList<string> warnings)
        {
            List<object> written = new List<object>(windows);
            while (written.Count > 0
                && Serializer.Serialize(Value(written, total, offset)).Length > room)
            {
                written.RemoveAt(written.Count - 1);
            }

            if (written.Count == 0 && windows.Count > 0)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.ResponseTooLarge,
                    "ウィンドウが1件も値の枠に収まらない。応答の枠を広げる。");
            }

            return ToolEnvelope.Success(Value(written, total, offset), warnings);
        }

        private static IDictionary<string, object> Value(IList<object> windows, int total, int offset)
        {
            object[] listed = new object[windows.Count];
            for (int at = 0; at < windows.Count; at++)
            {
                listed[at] = windows[at];
            }

            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.EditorVersionName, UiStructureCatalog.EditorVersion },
                { UiStructureCatalog.MainFormName, UiStructureCatalog.MainForm },
                { TotalName, total },
                { UiStructureCatalog.WindowsName, listed },
            };
            int next = offset + windows.Count;
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static bool TryOffset(McpMethodContext context, out int offset, out string message)
        {
            offset = 0;
            message = null;
            object given;
            if (!context.Params.TryGetValue(OffsetName, out given) || given == null)
            {
                return true;
            }

            if (!ValueInput.IsNumber(given))
            {
                message = OffsetName + " は整数でなければならない。";

                return false;
            }

            double taken = Convert.ToDouble(given, CultureInfo.InvariantCulture);
            if (taken != Math.Floor(taken) || taken < 0 || taken > int.MaxValue)
            {
                message = OffsetName + " は0以上の整数である。";

                return false;
            }

            offset = (int)taken;

            return true;
        }

        /// <summary>名指しからウィンドウを選ぶ。木を付けてよいときは <paramref name="named"/> を真にする。</summary>
        private static IList<IDictionary<string, object>> Choose(string wanted, out bool named)
        {
            named = false;
            if (wanted == null)
            {
                return UiStructureCatalog.Windows();
            }

            IDictionary<string, object> exact = UiStructureCatalog.Window(wanted);
            if (exact != null)
            {
                named = true;

                return new List<IDictionary<string, object>> { exact };
            }

            IList<IDictionary<string, object>> titled = UiStructureCatalog.ByTitle(wanted);
            if (titled.Count > 0)
            {
                named = true;

                return titled;
            }

            return UiStructureCatalog.Similar(wanted);
        }

        /// <summary>ウィンドウ1つの見出し。木を付けるウィンドウには、どこから開くかも載せる。</summary>
        private static IDictionary<string, object> Listed(IDictionary<string, object> window, bool named)
        {
            Dictionary<string, object> listed = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.FormName, UiStructureCatalog.Text(window, UiStructureCatalog.FormName) },
                {
                    UiStructureCatalog.AssemblyName,
                    UiStructureCatalog.Text(window, UiStructureCatalog.AssemblyName)
                },
                { UiStructureCatalog.TitleName, UiStructureCatalog.Text(window, UiStructureCatalog.TitleName) },
            };
            if (named)
            {
                listed.Add(
                    UiStructureCatalog.OpenedByName,
                    UiStructureCatalog.Array(window, UiStructureCatalog.OpenedByName));
            }

            return listed;
        }

        /// <summary>深さの分だけ残した木。深さを渡さないときはそのまま返す。</summary>
        private static object Cut(IDictionary<string, object> node, int depth)
        {
            if (depth >= MaxDepth)
            {
                return node;
            }

            Dictionary<string, object> kept = new Dictionary<string, object>(node, StringComparer.Ordinal);
            if (depth <= 0)
            {
                kept.Remove(UiStructureCatalog.ChildrenName);

                return kept;
            }

            IList<IDictionary<string, object>> children = UiStructureCatalog.Children(node);
            if (children.Count == 0)
            {
                return kept;
            }

            object[] below = new object[children.Count];
            for (int at = 0; at < children.Count; at++)
            {
                below[at] = Cut(children[at], depth - 1);
            }

            kept[UiStructureCatalog.ChildrenName] = below;

            return kept;
        }

        /// <summary>path の名前の並び。渡されなければ空、文字列の並びでなければ null。</summary>
        internal static IList<string> Steps(McpMethodContext context)
        {
            object given;
            if (!context.Params.TryGetValue(PathName, out given) || given == null)
            {
                return new List<string>();
            }

            object[] listed = given as object[];
            if (listed == null)
            {
                return null;
            }

            List<string> steps = new List<string>();
            foreach (object one in listed)
            {
                string name = one as string;
                if (name == null)
                {
                    return null;
                }

                steps.Add(name);
            }

            return steps;
        }

        private static bool TryDepth(McpMethodContext context, out int depth, out string message)
        {
            depth = MaxDepth;
            message = null;
            object given;
            if (!context.Params.TryGetValue(DepthName, out given) || given == null)
            {
                return true;
            }

            if (!ValueInput.IsNumber(given))
            {
                message = DepthName + " は整数でなければならない。";

                return false;
            }

            double taken = Convert.ToDouble(given, CultureInfo.InvariantCulture);
            if (taken != Math.Floor(taken) || taken < 0 || taken > MaxDepth)
            {
                message = DepthName + " は0以上 " + MaxDepth + " 以下の整数である。";

                return false;
            }

            depth = (int)taken;

            return true;
        }
    }
}
