using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    internal static class UiStructureRoute
    {
        internal const string ViaName = "via";

        /// <summary>
        /// 主画面までの道筋。主画面そのものなら空を返し、届かないウィンドウなら null を返す。
        /// 得られるのは段数の最も少ない道筋で、一度通ったウィンドウは再び通らない。
        /// </summary>
        internal static IList<IDictionary<string, object>> To(string form)
        {
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            string main = UiStructureCatalog.MainForm;
            if (string.Equals(form, main, StringComparison.Ordinal))
            {
                return new List<IDictionary<string, object>>();
            }

            IDictionary<string, string[]> came = Walk(form, main);
            if (came == null)
            {
                return null;
            }

            List<IDictionary<string, object>> route = new List<IDictionary<string, object>>();
            string at = main;
            while (!string.Equals(at, form, StringComparison.Ordinal))
            {
                string[] step = came[at];
                route.Add(Hop(at, step[1]));
                at = step[0];
            }

            return route;
        }

        /// <summary>
        /// 返すのは、開く側のウィンドウから「次に開くウィンドウと、その中の
        /// 道筋の綴り」への対応である。
        /// </summary>
        private static IDictionary<string, string[]> Walk(string form, string main)
        {
            Dictionary<string, string[]> came = new Dictionary<string, string[]>(StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { form };
            Queue<string> next = new Queue<string>();
            next.Enqueue(form);

            while (next.Count > 0)
            {
                string at = next.Dequeue();
                foreach (object one in UiStructureCatalog.Array(Opened(at), UiStructureCatalog.OpenedByName))
                {
                    IDictionary<string, object> route = (IDictionary<string, object>)one;
                    string opener = UiStructureCatalog.Text(route, UiStructureCatalog.FormName);
                    if (opener == null || !seen.Add(opener))
                    {
                        continue;
                    }

                    came[opener] = new[] { at, Join(route) };
                    if (string.Equals(opener, main, StringComparison.Ordinal))
                    {
                        return came;
                    }

                    next.Enqueue(opener);
                }
            }

            return null;
        }

        /// <summary>道筋の1段。開く側のウィンドウと、そのウィンドウの中で辿る部品を並べる。</summary>
        private static IDictionary<string, object> Hop(string opener, string joined)
        {
            IDictionary<string, object> window = UiStructureCatalog.Window(opener);
            List<IDictionary<string, object>> via = new List<IDictionary<string, object>>();
            IDictionary<string, object> at = UiStructureCatalog.Node(window, UiStructureCatalog.RootName);
            string shortcut = null;

            foreach (string name in joined.Split('\n'))
            {
                at = Child(at, name);
                if (at == null)
                {
                    break;
                }

                via.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiStructureCatalog.NameName, name },
                    { UiStructureCatalog.TextName, UiStructureCatalog.Text(at, UiStructureCatalog.TextName) },
                });
                shortcut = UiStructureCatalog.Text(at, UiStructureCatalog.ShortcutName);
            }

            Dictionary<string, object> hop = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiStructureCatalog.FormName, opener },
                { ViaName, via.ToArray() },
            };
            if (shortcut != null)
            {
                hop.Add(UiStructureCatalog.ShortcutName, shortcut);
            }

            return hop;
        }

        private static IDictionary<string, object> Child(IDictionary<string, object> at, string name)
        {
            foreach (IDictionary<string, object> child in UiStructureCatalog.Children(at))
            {
                if (string.Equals(
                    UiStructureCatalog.Text(child, UiStructureCatalog.NameName), name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static IDictionary<string, object> Opened(string form)
        {
            return UiStructureCatalog.Window(form);
        }

        private static string Join(IDictionary<string, object> route)
        {
            return string.Join(
                "\n",
                new List<string>(UiStructureCatalog.Texts(route, UiStructureCatalog.PathName)).ToArray());
        }
    }
}
