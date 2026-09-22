// 画面がいま選んでいるものを、対象の指定として渡す。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Form;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>1つの種類について、画面の選択を読む口と、その選択を書き換えるツールの名前。</summary>
    public sealed class ScreenPick
    {
        private readonly Func<IList<int>> _taken;

        public ScreenPick(Func<IList<int>> taken, string picking)
        {
            if (taken == null)
            {
                throw new ArgumentNullException(nameof(taken));
            }

            if (string.IsNullOrWhiteSpace(picking))
            {
                throw new ArgumentException("空にできない。", nameof(picking));
            }

            _taken = taken;
            Picking = picking;
        }

        /// <summary>その選択を書き換えるツールの名前。</summary>
        public string Picking { get; }

        /// <summary>画面がいま選んでいる位置。</summary>
        public IList<int> Taken()
        {
            return _taken() ?? new int[0];
        }
    }

    /// <summary>
    /// 画面が選んでいる位置を種類ごとに読む。3Dビューが選ぶ種類はビューの口から、材質はリストを
    /// 持つ画面の口から読む。
    /// </summary>
    public sealed class ScreenTargets
    {
        /// <summary>画面の選択を読めない段。読む相手を持たないところが使う。</summary>
        public static readonly ScreenTargets None = new ScreenTargets(() => null, () => null);

        private readonly Func<object> _view;

        private readonly Func<object> _form;

        public ScreenTargets(Func<object> view, Func<object> form)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            _view = view;
            _form = form;
        }

        /// <summary>その型の要素を画面が選べるか。</summary>
        public static bool Selectable(Type element)
        {
            return element != null && Kind(element) != null;
        }

        /// <summary>画面が選べる種類。スキーマが並べる順。</summary>
        public static IList<string> Kinds
        {
            get
            {
                return new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Face,
                    ElementKinds.Bone,
                    ElementKinds.Body,
                    ElementKinds.Joint,
                    ElementKinds.Material,
                };
            }
        }

        /// <summary>その種類の要素を画面が選べるか。</summary>
        public static bool Selects(string kind)
        {
            return kind != null && Kinds.Contains(kind, StringComparer.Ordinal);
        }

        /// <summary>その道が並べる要素を画面が選べるか。親を辿る道は当たらない。</summary>
        public static bool Selects(ToolAccess access)
        {
            return access != null
                && access.Kind == ToolAccessKind.Element
                && access.Parents.Count == 0
                && access.Owner == null
                && Selectable(access.Element);
        }

        /// <summary>その種類の選択を書き換えるツールの名前。画面が選べない種類では null。</summary>
        public static string Picking(string kind)
        {
            if (!Selects(kind))
            {
                return null;
            }

            return string.Equals(kind, ElementKinds.Material, StringComparison.Ordinal)
                ? "session_set_selected_material_indices"
                : "view_set_selected_" + kind + "_indices_pmd_view_connector";
        }

        /// <summary>
        /// その種類の選択を読む口。画面が選べない種類では null。
        /// </summary>
        public ScreenPick Pick(string kind, int count)
        {
            return Selects(kind) ? new ScreenPick(() => Taken(kind, count), Picking(kind)) : null;
        }

        /// <summary>その型の要素の選択を読む口。画面が選べない型では null。</summary>
        public ScreenPick Pick(Type element, int count)
        {
            return Pick(Kind(element), count);
        }

        /// <summary>
        /// その型の要素のうち、画面がいま選んでいる位置。選んだ順のまま渡し、いまのリストに
        /// 並んでいない位置は外す。画面の口を引けなければ空を渡す。
        /// </summary>
        public IList<int> Taken(Type element, int count)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "0以上でなければならない。");
            }

            return Taken(Kind(element), count);
        }

        /// <summary>
        /// その種類の要素のうち、画面がいま選んでいる位置。画面が選べない種類では空を渡す。
        /// </summary>
        public IList<int> Taken(string kind, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "0以上でなければならない。");
            }

            if (!Selects(kind))
            {
                return new int[0];
            }

            if (string.Equals(kind, ElementKinds.Material, StringComparison.Ordinal))
            {
                IPEFormConnector form = _form() as IPEFormConnector;

                return form == null ? new int[0] : Inside(form.GetSelectedMaterialIndices(), count);
            }

            object view = _view();

            return view == null ? new int[0] : ViewSelection.Taken(view, kind, count);
        }

        private static string Kind(Type element)
        {
            if (element == typeof(IPXMaterial))
            {
                return ElementKinds.Material;
            }

            if (element == typeof(IPXVertex))
            {
                return ElementKinds.Vertex;
            }

            if (element == typeof(IPXFace))
            {
                return ElementKinds.Face;
            }

            if (element == typeof(IPXBone))
            {
                return ElementKinds.Bone;
            }

            if (element == typeof(IPXBody))
            {
                return ElementKinds.Body;
            }

            if (element == typeof(IPXJoint))
            {
                return ElementKinds.Joint;
            }

            return null;
        }

        private static IList<int> Inside(IEnumerable<int> held, int count)
        {
            return (held ?? new int[0]).Where(at => at >= 0 && at < count).ToList();
        }
    }
}
