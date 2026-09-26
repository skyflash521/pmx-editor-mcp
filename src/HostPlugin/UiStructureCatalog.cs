using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 組み込んだ画面の構造の台帳を引く。台帳は初めて要るときに1度だけ解き、以後は同じものを
    /// 返す。
    ///
    /// ウィンドウの名前は型の完全名で、タイトルはフォーム自身の Text である。部品の節は、型・受け皿の名前・
    /// 文言・指したときに出る説明・ショートカット・右クリックメニューの名前・断片のクラス・
    /// 実行時に組み立てる印・開くウィンドウ・押したときの危険の区分・子を持ち、無い項目は省かれている。
    /// </summary>
    internal static class UiStructureCatalog
    {
        internal const string EditorVersionName = "editorVersion";

        internal const string MainFormName = "mainForm";

        internal const string WindowsName = "windows";

        internal const string FormName = "form";

        internal const string AssemblyName = "assembly";

        internal const string TitleName = "title";

        internal const string RootName = "root";

        internal const string OpenedByName = "openedBy";

        internal const string MessagesName = "messages";

        internal const string PathName = "path";

        internal const string HandlerName = "handler";

        internal const string TypeName = "type";

        internal const string NameName = "name";

        internal const string TextName = "text";

        internal const string ToolTipName = "toolTip";

        internal const string ShortcutName = "shortcut";

        internal const string ChildrenName = "children";

        internal const string DangerName = "danger";

        private static readonly HashSet<string> PressableTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToolStripMenuItem", "ToolStripButton", "Button", "CheckBox",
        };

        private static readonly object Gate = new object();

        private static IDictionary<string, object> _read;

        /// <summary>台帳の全体。</summary>
        internal static IDictionary<string, object> Read()
        {
            lock (Gate)
            {
                if (_read == null)
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    _read = (IDictionary<string, object>)serializer.DeserializeObject(
                        string.Concat(GeneratedUiStructure.Chunks));
                }

                return _read;
            }
        }

        internal static string EditorVersion
        {
            get { return Text(Read(), EditorVersionName); }
        }

        internal static string MainForm
        {
            get { return Text(Read(), MainFormName); }
        }

        /// <summary>ウィンドウの並び。台帳に並んだ順で返す。</summary>
        internal static IList<IDictionary<string, object>> Windows()
        {
            List<IDictionary<string, object>> windows = new List<IDictionary<string, object>>();
            foreach (object one in Array(Read(), WindowsName))
            {
                windows.Add((IDictionary<string, object>)one);
            }

            return windows;
        }

        /// <summary>型の完全名で引く。完全一致だけを返す。</summary>
        internal static IDictionary<string, object> Window(string named)
        {
            foreach (IDictionary<string, object> window in Windows())
            {
                if (string.Equals(Text(window, FormName), named, StringComparison.Ordinal))
                {
                    return window;
                }
            }

            return null;
        }

        /// <summary>タイトルが完全に一致するウィンドウ。同じタイトルのウィンドウは複数ある。</summary>
        internal static IList<IDictionary<string, object>> ByTitle(string named)
        {
            List<IDictionary<string, object>> found = new List<IDictionary<string, object>>();
            foreach (IDictionary<string, object> window in Windows())
            {
                if (string.Equals(Text(window, TitleName), named, StringComparison.Ordinal))
                {
                    found.Add(window);
                }
            }

            return found;
        }

        /// <summary>名前かタイトルに部分一致するウィンドウ。完全に一致するものが無いときの答えに使う。</summary>
        internal static IList<IDictionary<string, object>> Similar(string named)
        {
            List<IDictionary<string, object>> found = new List<IDictionary<string, object>>();
            foreach (IDictionary<string, object> window in Windows())
            {
                if (SignatureDump.TextMatch.Contains(Text(window, FormName), named)
                    || SignatureDump.TextMatch.Contains(Text(window, TitleName), named))
                {
                    found.Add(window);
                }
            }

            return found;
        }

        internal static string Text(IDictionary<string, object> held, string name)
        {
            object given;

            return held != null && held.TryGetValue(name, out given) ? given as string : null;
        }

        internal static object[] Array(IDictionary<string, object> held, string name)
        {
            object given;
            if (held == null || !held.TryGetValue(name, out given))
            {
                return new object[0];
            }

            return given as object[] ?? new object[0];
        }

        internal static IDictionary<string, object> Node(IDictionary<string, object> held, string name)
        {
            object given;

            return held != null && held.TryGetValue(name, out given)
                ? given as IDictionary<string, object>
                : null;
        }

        /// <summary>その節の子。</summary>
        internal static IList<IDictionary<string, object>> Children(IDictionary<string, object> node)
        {
            List<IDictionary<string, object>> children = new List<IDictionary<string, object>>();
            foreach (object one in Array(node, ChildrenName))
            {
                children.Add((IDictionary<string, object>)one);
            }

            return children;
        }

        /// <summary>台帳の節が、押すと何かが起きる部品か。</summary>
        internal static bool Pressable(IDictionary<string, object> node)
        {
            return PressableTypes.Contains(Text(node, TypeName) ?? string.Empty);
        }

        /// <summary>台帳の節の、その名前の子。無ければ null。</summary>
        internal static IDictionary<string, object> Child(IDictionary<string, object> node, string name)
        {
            foreach (IDictionary<string, object> child in Children(node))
            {
                if (string.Equals(Text(child, NameName), name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>文字列の並びを取り出す。</summary>
        internal static IList<string> Texts(IDictionary<string, object> held, string name)
        {
            List<string> texts = new List<string>();
            foreach (object one in Array(held, name))
            {
                texts.Add(one as string);
            }

            return texts;
        }
    }
}
