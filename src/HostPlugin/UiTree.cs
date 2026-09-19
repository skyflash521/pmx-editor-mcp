using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 窓の一覧、または名指しした窓の部品の木を返すツール。答えは組み込んだ台帳だけから作り、
    /// エディタが起きていなくても、窓が閉じていても同じ答えを返す。
    ///
    /// 名指しが1つの窓に定まらなくてもエラーにしない。型の完全名で当たればその窓だけ、当たらなければ題が
    /// 一致する窓すべて、それも無ければ名前か題に部分一致する窓すべてを返し、どれも当たらなければ
    /// 空で返す。木を付けるのは、型の完全名か題が完全に一致した窓に限る。
    ///
    /// 木が値の枠に収まらないときは、深さを縮めずにエラーを返し、どう絞れば収まるかを添える。
    /// 木を付けない一覧は、収まらなければ入るところまでを返す。当たった数を total で示し、残りが
    /// あれば次に渡す offset を nextOffset で示す。
    /// </summary>
    public static class UiTree
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_get_ui_structure";

        /// <summary>窓を選ぶ入力の名前。省くと一覧を返す。</summary>
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

        private const string TotalName = "total";

        private const string NextOffsetName = "nextOffset";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            methods.Add(ToolName, Get);
        }

        private static object Get(McpMethodContext context)
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

            List<object> windows = new List<object>();
            foreach (IDictionary<string, object> window in chosen)
            {
                IDictionary<string, object> listed = Listed(window, named);
                if (named)
                {
                    IDictionary<string, object> node =
                        UiStructureCatalog.Node(window, UiStructureCatalog.RootName);
                    foreach (string step in path)
                    {
                        node = Child(node, step);
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
                ? Whole(windows, chosen, found.Count, offset, room)
                : Some(windows, found.Count, offset, room);
        }

        /// <summary>木を付けた答え。収まらないときは絞り方を添えたエラーを返す。</summary>
        private static object Whole(
            IList<object> windows,
            IList<IDictionary<string, object>> chosen,
            int total,
            int offset,
            int room)
        {
            IDictionary<string, object> value = Value(windows, total, offset);
            if (Serializer.Serialize(value).Length <= room)
            {
                return ToolEnvelope.Success(value);
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
                    "当たった窓の木が値の枠に収まらない。" + WindowName + " を型の完全名で1つに絞る: "
                        + string.Join("、", forms.ToArray()));
            }

            return ToolEnvelope.Failure(
                ToolEnvelope.ResponseTooLarge,
                "木が値の枠に収まらない。" + PathName + " で降りて取り直す。");
        }

        /// <summary>木を付けない一覧。収まるところまでを返し、残りは次の offset で取り直せる。</summary>
        private static object Some(IList<object> windows, int total, int offset, int room)
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
                    "窓が1件も値の枠に収まらない。応答の枠を広げる。");
            }

            return ToolEnvelope.Success(Value(written, total, offset));
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

        /// <summary>名指しから窓を選ぶ。木を付けてよいときは <paramref name="named"/> を真にする。</summary>
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

        /// <summary>窓1つの見出し。木を付ける窓には、どこから開くかも載せる。</summary>
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

        private static IList<string> Steps(McpMethodContext context)
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
