using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    /// <summary>UIスレッドで呼ぶ。</summary>
    internal static class UiLive
    {
        /// <summary>その名前のウィンドウのうち、画面に出ている最初のもの。無ければ null。</summary>
        internal static Form Shown(IEnumerable<Form> forms, string named)
        {
            string root = RootOf(named);
            if (root == null)
            {
                return null;
            }

            foreach (Form form in forms)
            {
                if (form.Visible && string.Equals(form.Name, root, StringComparison.Ordinal))
                {
                    return form;
                }
            }

            return null;
        }

        /// <summary>
        /// 名前の連なりを、ウィンドウから1段ずつ辿った部品。辿れなければ null。<paramref name="menus"/> には、
        /// 辿った途中で開くメニューを外側から順に渡す。
        /// </summary>
        internal static object Find(Form form, IList<string> path, IList<UiMenu> menus)
        {
            object at = form;
            foreach (string step in path)
            {
                object found = Child(at, step, menus);
                if (found == null)
                {
                    return null;
                }

                at = found;
            }

            return at;
        }

        private static object Child(object at, string name, IList<UiMenu> menus)
        {
            Control control = at as Control;
            if (control != null)
            {
                foreach (Control child in control.Controls)
                {
                    if (string.Equals(child.Name, name, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }

                ToolStrip strip = control as ToolStrip;
                if (strip != null)
                {
                    foreach (ToolStripItem item in strip.Items)
                    {
                        if (string.Equals(item.Name, name, StringComparison.Ordinal))
                        {
                            return item;
                        }
                    }
                }
            }

            ToolStripDropDownItem dropping = at as ToolStripDropDownItem;
            if (dropping != null)
            {
                foreach (ToolStripItem item in dropping.DropDownItems)
                {
                    if (string.Equals(item.Name, name, StringComparison.Ordinal))
                    {
                        menus.Add(new UiMenu(dropping));

                        return item;
                    }
                }
            }

            return null;
        }

        private static string RootOf(string named)
        {
            IDictionary<string, object> window = UiStructureCatalog.Window(named);

            return UiStructureCatalog.Text(
                UiStructureCatalog.Node(window, UiStructureCatalog.RootName), UiStructureCatalog.NameName);
        }
    }

    internal sealed class UiMenu
    {
        private readonly ToolStripDropDownItem _item;

        internal UiMenu(ToolStripDropDownItem item)
        {
            _item = item;
        }

        /// <summary>メニューを開く。開くときに走るエディタの処理が、項目の押せる状態を決め直す。</summary>
        internal void Open()
        {
            _item.ShowDropDown();
        }

        internal void Close()
        {
            _item.HideDropDown();
        }
    }
}
