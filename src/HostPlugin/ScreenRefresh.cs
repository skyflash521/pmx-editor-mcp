// エディタのビューは、描き直しを頼まれたときだけ描く。SDKの口は中身を変えるだけで描き直さない
// ので、変えた側が頼まないと画面は前のまま残る。一覧も同じで、作り直しを頼むまで古い値を並べる。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Form;
using PEPlugin.Pmd;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>変えた中身を画面へ映すのに要ること。</summary>
    public enum ScreenRefreshKind
    {
        /// <summary>画面に映るものは変わっていない。</summary>
        None,

        /// <summary>ビューを描き直す。</summary>
        Drawn,

        /// <summary>リストとビューのモデルを作り直してから描き直す。</summary>
        Rebuilt,
    }

    /// <summary>
    /// 変えた中身をエディタの画面へ映す段。エディタがCプラグイン連携の呼び出しに対して自分で
    /// 行っているのと同じ手順を、ホストの呼び出しにも行う。
    /// </summary>
    public sealed class ScreenRefresh
    {
        /// <summary>画面へ映せなかったときに添える知らせ。</summary>
        public const string NotShownWarning =
            "エディタの画面を映し直せなかった。呼び出しは済んでいるが、画面とリストは呼び出しの"
                + "前の中身を映したままになっている。";

        /// <summary>書くとビューの見た目が変わるのに、描き直しを伴わない行。</summary>
        private static readonly HashSet<string> Draws =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedVertexIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedFaceIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedBoneIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedBodyIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedJointIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SetVertexIndices(System.Int32[])",
                "PEPlugin.View.IPEPMDViewConnector.SelectedBodyIndex()",
                "PEPlugin.View.IPEPMDViewConnector.SelectedJointIndex()",
                "PEPlugin.View.IPEPMDViewConnector.EnableHandleEdit()",
                "PEPlugin.View.IPEPMDViewConnector.CameraPosition()",
                "PEPlugin.View.IPEPMDViewConnector.CameraTarget()",
                "PEPlugin.View.IPEPMDViewConnector.CameraUpVector()",
                "PEPlugin.View.IPEPMDViewConnector.CameraRotateCenter()",
                "PEPlugin.View.IPEPMDViewConnector.SetCameraView("
                    + "PEPlugin.Pmd.IPEVector3,PEPlugin.Pmd.IPEVector3,PEPlugin.Pmd.IPEVector3)",
            };

        /// <summary>モデルの中身そのものを変えるのに、リストの作り直しを伴わない行。</summary>
        private static readonly HashSet<string> Rebuilds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PEPlugin.View.IPEPMDViewConnector.UpdateModelSize(System.Single)",
                "PEPlugin.View.IPEPMDViewConnector.UpdateModelSize(System.Single,System.Boolean)",
                "PEPlugin.View.IPEVertexEditConnector.Move()",
                "PEPlugin.View.IPEVertexEditConnector.Move(PEPlugin.Pmd.IPEVector3)",
                "PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis()",
                "PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis(System.Single)",
                "PEPlugin.View.IPEVertexEditConnector.Rotate()",
                "PEPlugin.View.IPEVertexEditConnector.Rotate(PEPlugin.Pmd.IPEVector3)",
                "PEPlugin.View.IPEVertexEditConnector.RotateNormal()",
                "PEPlugin.View.IPEVertexEditConnector.RotateNormal(PEPlugin.Pmd.IPEVector3)",
                "PEPlugin.View.IPEVertexEditConnector.Scaling()",
                "PEPlugin.View.IPEVertexEditConnector.Scaling(PEPlugin.Pmd.IPEVector3)",
            };

        /// <summary>ビューでは、その区分だけを作り直せば映るリストの行。</summary>
        private static readonly Dictionary<string, Action<IPXPmxViewConnector>> Remakes =
            new Dictionary<string, Action<IPXPmxViewConnector>>(StringComparer.Ordinal)
            {
                { "PEPlugin.Pmx.IPXPmx.Bone()", view => view.UpdateModel_Bone() },
                { "PEPlugin.Pmx.IPXPmx.Body()", view => view.UpdateModel_Body() },
                { "PEPlugin.Pmx.IPXPmx.Joint()", view => view.UpdateModel_Joint() },
            };

        /// <summary>
        /// ビューでは、要素の数を変えずに中身だけを書き換えたとき、その区分だけを作り直せば映る
        /// 要素の種類。
        /// </summary>
        private static readonly Dictionary<string, Action<IPXPmxViewConnector>> Rewrites =
            new Dictionary<string, Action<IPXPmxViewConnector>>(StringComparer.Ordinal)
            {
                { ElementKinds.Vertex, view => view.UpdateModel_Vertex() },
                { ElementKinds.Bone, view => view.UpdateModel_Bone() },
                { ElementKinds.Body, view => view.UpdateModel_Body() },
                { ElementKinds.Joint, view => view.UpdateModel_Joint() },
                { WeightKind, view => view.UpdateModel_Weight() },
            };

        /// <summary>
        /// 頂点のウェイトと変形方式だけを書き換えたことを表す区分の名前。頂点の区分と同じく頂点の
        /// バッファを作り直し、ウェイトの表示も作り直す。
        /// </summary>
        public const string WeightKind = "weight";

        /// <summary>
        /// 何も映さない段。映し直しをエディタ自身が行う経路が、ホストからは何もしないために使う。
        /// </summary>
        public static readonly ScreenRefresh Idle = new ScreenRefresh(() => null, () => null);

        private readonly Func<object> _view;

        private readonly Func<object> _form;

        /// <summary>
        /// 3Dビューの口とリストの口を引く手立てを与えて生成する。引けないときは null を返してよく、
        /// その相手への手順は飛ばす。
        /// </summary>
        public ScreenRefresh(Func<object> view, Func<object> form)
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

        /// <summary>
        /// 映せなかったことを知らせへ足す。映せた呼び出しでは渡された知らせをそのまま返す。
        /// </summary>
        public static IList<string> Noted(IList<string> warnings, bool shown)
        {
            if (shown)
            {
                return warnings;
            }

            List<string> all = new List<string>(warnings ?? new string[0]) { NotShownWarning };

            return all;
        }

        /// <summary>
        /// その呼び出しの後に、呼び出しを包む側が行う映し直し。読み取りは何も要らない——同じ行で
        /// 読みと書きを兼ねるプロパティは、読む側の呼び出しがこの分類で分かれる。複製編集は反映の
        /// 側が自分で映すので、ここでは何も要らない。
        /// </summary>
        public static ScreenRefreshKind Needed(EditKind edit, IEnumerable<string> rowKeys)
        {
            if (rowKeys == null)
            {
                throw new ArgumentNullException(nameof(rowKeys));
            }

            if (edit == EditKind.Read || edit == EditKind.DuplicateEdit)
            {
                return ScreenRefreshKind.None;
            }

            ScreenRefreshKind needed = ScreenRefreshKind.None;
            foreach (string rowKey in rowKeys)
            {
                if (Rebuilds.Contains(rowKey))
                {
                    return ScreenRefreshKind.Rebuilt;
                }

                if (Draws.Contains(rowKey))
                {
                    needed = ScreenRefreshKind.Drawn;
                }
            }

            return needed;
        }

        /// <summary>
        /// 映し直す。映せたら真で、映せなければ偽を返す——映し直しの失敗は呼び出しそのものの失敗
        /// ではないので、断りへ変えずに知らせだけを渡す。エディタの画面へ触るので、UIスレッドへ
        /// 委譲した中で呼ぶ。引けない口は飛ばす——片方が引けないことで、もう片方まで古いまま
        /// にしない。
        /// </summary>
        public bool Apply(ScreenRefreshKind kind)
        {
            if (kind == ScreenRefreshKind.None)
            {
                return true;
            }

            try
            {
                Show(kind);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 複製を反映した後に映し直す。<paramref name="listRow"/> は反映で変えたリストの行で、
        /// 分からなければ null。
        /// </summary>
        public bool ApplyReflected(string listRow)
        {
            Action<IPXPmxViewConnector> remake;
            if (listRow == null || !Remakes.TryGetValue(listRow, out remake))
            {
                return Apply(ScreenRefreshKind.Rebuilt);
            }

            return Remake(new[] { remake });
        }

        /// <summary>
        /// 要素の数を変えずに、<paramref name="kinds"/> の種類の中身だけを書き換えた複製を反映した
        /// 後に映し直す。種類は <see cref="ElementKinds"/> の名前で渡す。区分だけの作り直しを
        /// 持たない種類が混じれば、ビューのモデルを丸ごと作り直す。
        /// </summary>
        public bool ApplyRewritten(IList<string> kinds)
        {
            if (kinds == null)
            {
                throw new ArgumentNullException(nameof(kinds));
            }

            if (!kinds.All(Rewrites.ContainsKey))
            {
                return Apply(ScreenRefreshKind.Rebuilt);
            }

            return Remake(kinds.Select(kind => Rewrites[kind]).ToList());
        }

        /// <summary>リストを作り直し、ビューは渡した区分だけを作り直してから描き直す。</summary>
        private bool Remake(IList<Action<IPXPmxViewConnector>> remakes)
        {
            try
            {
                IPEFormConnector form = _form() as IPEFormConnector;
                if (form != null)
                {
                    form.UpdateList(UpdateObject.All);
                }

                IPXPmxViewConnector view = _view() as IPXPmxViewConnector;
                if (view != null)
                {
                    foreach (Action<IPXPmxViewConnector> remake in remakes)
                    {
                        remake(view);
                    }

                    view.UpdateView();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Show(ScreenRefreshKind kind)
        {

            if (kind == ScreenRefreshKind.Rebuilt)
            {
                IPEFormConnector form = _form() as IPEFormConnector;
                if (form != null)
                {
                    form.UpdateList(UpdateObject.All);
                }
            }

            IPXPmxViewConnector view = _view() as IPXPmxViewConnector;
            if (view == null)
            {
                return;
            }

            if (kind == ScreenRefreshKind.Rebuilt)
            {
                view.UpdateModel();
            }

            view.UpdateView();
        }

    }
}
