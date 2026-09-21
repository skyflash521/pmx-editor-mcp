using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;
using PmxEditorMcp.SignatureDump;

namespace PmxEditorMcp
{
    /// <summary>
    /// 文言で画面の部品を探し、主画面からその部品までの道筋を返すツール。答えは組み込んだ台帳
    /// だけから作り、エディタが起きていなくても、窓が閉じていても同じ答えを返す。
    ///
    /// 当たりを探すのは、部品の文言と指したときに出る説明、窓の題、そのクラスが出す確認・報告の
    /// 文言である。確認・報告の文言に当たったものは、道筋の代わりにその文言を持つ。
    ///
    /// 当たった数を total で示し、返しきれなかった残りがあれば次に渡す offset を nextOffset で示す。
    /// </summary>
    public static class UiFind
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "editor_find_operation";

        /// <summary>探す文言を受け取る入力の名前。</summary>
        public const string TextName = "text";

        /// <summary>返す件数の上限を受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>並びの何番目から返すかを受け取る入力の名前。</summary>
        public const string OffsetName = "offset";

        /// <summary>上限を省いたときに返す件数。</summary>
        public const int DefaultLimit = 50;

        /// <summary>1回で返せる件数の上限。</summary>
        public const int MaxLimit = 500;

        private const string TotalName = "total";

        private const string MatchesName = "matches";

        private const string RouteName = "route";

        private const string UnreachableName = "unreachable";

        private const string MessageName = "message";

        private const string NextOffsetName = "nextOffset";

        /// <summary>並びの中で、当たり1件の手前に置く区切りの文字数。</summary>
        private const int SeparatorChars = 1;

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            methods.Add(ToolName, Find);
        }

        private static object Find(McpMethodContext context)
        {
            object given;
            string wanted = context.Params.TryGetValue(TextName, out given) ? given as string : null;
            if (string.IsNullOrEmpty(wanted))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, TextName + " は1文字以上の文字列でなければならない。");
            }

            int limit;
            string message;
            if (!TryLimit(context, out limit, out message))
            {
                return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, message);
            }

            int offset;
            if (!TryCount(context, OffsetName, 0, int.MaxValue, 0, out offset, out message))
            {
                return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, message);
            }

            IList<IDictionary<string, object>> found = Matches(wanted);
            int room = ResponseSize.ValueChars(context.BudgetChars) - Wrapper(found.Count);
            List<IDictionary<string, object>> written = new List<IDictionary<string, object>>();
            int used = 0;
            for (int at = offset; at < found.Count && written.Count < limit; at++)
            {
                int size = Serializer.Serialize(found[at]).Length + SeparatorChars;
                if (used + size > room)
                {
                    break;
                }

                used += size;
                written.Add(found[at]);
            }

            if (written.Count == 0 && offset < found.Count)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.ResponseTooLarge,
                    "当たりが1件も値の枠に収まらない。" + TextName + " を絞るか、応答の枠を広げる。");
            }

            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.EditorVersionName, UiStructureCatalog.EditorVersion },
                { TotalName, found.Count },
                { MatchesName, written.ToArray() },
            };
            int next = offset + written.Count;
            if (next < found.Count)
            {
                value.Add(NextOffsetName, next);
            }

            return ToolEnvelope.Success(value);
        }

        /// <summary>
        /// 当たりを包む分の文字数。並びを空にして、数の項目を採りうるいちばん長い値で書いたものを
        /// 採る。
        /// </summary>
        private static int Wrapper(int total)
        {
            return Serializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.EditorVersionName, UiStructureCatalog.EditorVersion },
                { TotalName, total },
                { MatchesName, new object[0] },
                { NextOffsetName, int.MaxValue },
            }).Length;
        }

        private static IList<IDictionary<string, object>> Matches(string wanted)
        {
            List<IDictionary<string, object>> found = new List<IDictionary<string, object>>();
            foreach (IDictionary<string, object> window in UiStructureCatalog.Windows())
            {
                string form = UiStructureCatalog.Text(window, UiStructureCatalog.FormName);
                string title = UiStructureCatalog.Text(window, UiStructureCatalog.TitleName);
                if (TextMatch.Contains(title, wanted))
                {
                    found.Add(Match(form, title, new List<IDictionary<string, object>>(), null));
                }

                Walk(
                    UiStructureCatalog.Node(window, UiStructureCatalog.RootName),
                    new List<IDictionary<string, object>>(),
                    form,
                    title,
                    wanted,
                    found);

                foreach (string said in UiStructureCatalog.Texts(window, UiStructureCatalog.MessagesName))
                {
                    if (TextMatch.Contains(said, wanted))
                    {
                        found.Add(Match(form, title, null, said));
                    }
                }
            }

            return found;
        }

        private static void Walk(
            IDictionary<string, object> node,
            IList<IDictionary<string, object>> path,
            string form,
            string title,
            string wanted,
            IList<IDictionary<string, object>> found)
        {
            foreach (IDictionary<string, object> child in UiStructureCatalog.Children(node))
            {
                List<IDictionary<string, object>> below =
                    new List<IDictionary<string, object>>(path) { Step(child) };
                if (TextMatch.Contains(
                        UiStructureCatalog.Text(child, UiStructureCatalog.TextName), wanted)
                    || TextMatch.Contains(
                        UiStructureCatalog.Text(child, UiStructureCatalog.ToolTipName), wanted))
                {
                    found.Add(Match(form, title, below, null));
                }

                Walk(child, below, form, title, wanted, found);
            }
        }

        private static IDictionary<string, object> Step(IDictionary<string, object> node)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.TypeName, UiStructureCatalog.Text(node, UiStructureCatalog.TypeName) },
                { UiStructureCatalog.NameName, UiStructureCatalog.Text(node, UiStructureCatalog.NameName) },
                { UiStructureCatalog.TextName, UiStructureCatalog.Text(node, UiStructureCatalog.TextName) },
            };
        }

        private static IDictionary<string, object> Match(
            string form, string title, IList<IDictionary<string, object>> path, string said)
        {
            Dictionary<string, object> match = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.FormName, form },
            };
            if (title != null)
            {
                match.Add(UiStructureCatalog.TitleName, title);
            }

            if (said != null)
            {
                match.Add(MessageName, said);
            }
            else
            {
                match.Add(UiStructureCatalog.PathName, Listed(path));
            }

            IList<IDictionary<string, object>> route = UiStructureRoute.To(form);
            if (route == null)
            {
                match.Add(UnreachableName, true);
            }
            else
            {
                match.Add(RouteName, Listed(route));
            }

            return match;
        }

        private static object[] Listed(IList<IDictionary<string, object>> given)
        {
            object[] listed = new object[given.Count];
            for (int at = 0; at < given.Count; at++)
            {
                listed[at] = given[at];
            }

            return listed;
        }

        private static bool TryLimit(McpMethodContext context, out int limit, out string message)
        {
            return TryCount(context, LimitName, 1, MaxLimit, DefaultLimit, out limit, out message);
        }

        /// <summary>範囲の中の整数を1つ読む。渡されていなければ既定を採る。</summary>
        private static bool TryCount(
            McpMethodContext context,
            string name,
            int least,
            int most,
            int fallback,
            out int count,
            out string message)
        {
            count = fallback;
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given) || given == null)
            {
                return true;
            }

            if (!ValueInput.IsNumber(given))
            {
                message = name + " は整数でなければならない。";

                return false;
            }

            double taken = Convert.ToDouble(given, CultureInfo.InvariantCulture);
            if (taken != Math.Floor(taken) || taken < least || taken > most)
            {
                message = name + " は" + least.ToString(CultureInfo.InvariantCulture) + "以上 "
                    + most.ToString(CultureInfo.InvariantCulture) + " 以下の整数である。";

                return false;
            }

            count = (int)taken;

            return true;
        }
    }
}
