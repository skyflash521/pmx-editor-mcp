using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using PEPlugin.SDX;

namespace PmxEditorMcp.Tests
{
    internal sealed class ComposedScreenFixture : IDisposable
    {
        private readonly ModelStamps _stamps = new ModelStamps();

        private readonly ComposedEditFixture _edit;

        private McpMethodTable _tools;

        private string _before;

        private readonly ModelShape _shape = new ModelShape();

        public ComposedScreenFixture()
        {
            _edit = new ComposedEditFixture(_stamps);
            View.Model = Model;
            Parts.Model = Model;
            TransformView = new FakeTransformView(() => Model.Bone.Count, () => Model.Morph.Count);
        }

        public FakePmx Model
        {
            get { return _edit.Model; }
        }

        public int Clones
        {
            get { return _edit.Clones; }
        }

        /// <summary>VMDやPMXを作る相手の題材。</summary>
        public FakeHostBuilder Builder { get; } = new FakeHostBuilder();

        public FakePmxView View
        {
            get { return _edit.View; }
        }

        public FakeFormConnector Form
        {
            get { return _edit.Form; }
        }

        public FakePartsSelect Parts { get; } = new FakePartsSelect();

        /// <summary>ビューの表示の設定の題材。</summary>
        public FakeViewSetting Setting { get; } = new FakeViewSetting();

        public FakeSubView SubView { get; } = new FakeSubView();

        public FakeTransformView TransformView { get; }

        internal IVmdPoseSource Poses { get; set; } = new SdkVmdPoseSource();

        public HandleLedger Handles
        {
            get { return _edit.Handles; }
        }

        public SwitchedModifierKeys Keys { get; } = new SwitchedModifierKeys();

        /// <summary>開いているウィンドウとしてツールへ渡す一覧。</summary>
        public List<System.Windows.Forms.Form> Forms { get; } = new List<System.Windows.Forms.Form>();

        /// <summary>
        /// 後片付けで、モデルの中身が呼び出しの前と同じままかを確かめる。画面へ触るツールはモデルを
        /// 変えないので、この題材を使うテストはすべてこの不変条件を通る。
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_before != null && !string.Equals(_before, _shape.Of(Model), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("画面のツールがモデルの中身を変えた。");
                }

                if (_edit.LastClone != null
                    && !string.Equals(_shape.Of(Model), _shape.Of(_edit.LastClone), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("画面のツールが得た複製の中身を変えた。");
                }
            }
            finally
            {
                _edit.Dispose();
            }
        }

        /// <summary>いまのモデルの中身を、後片付けで突き合わせる相手として控える。</summary>
        public void Watch()
        {
            _before = _shape.Of(Model);
        }

        /// <summary>載せたツールを名前で呼ぶ。表はここで初めて組み立てる。</summary>
        public IDictionary<string, object> Call(
            string tool, IDictionary<string, object> arguments)
        {
            if (_tools == null)
            {
                Watch();
                _tools = new McpMethodTable();
                ComposedScreenTools.AddTo(
                    _tools,
                    new ComposedScreen(
                        _edit.Session(),
                        () => View,
                        () => Form,
                        () => Parts,
                        new ScreenRefresh(() => View, () => Form),
                        () => Setting),
                    () => Builder,
                    () => SubView,
                    () => new List<System.Windows.Forms.Form>(Forms),
                    () => TransformView,
                    Keys,
                    Poses);
            }

            McpMethod method;
            if (!_tools.TryGet(tool, out method))
            {
                throw new InvalidOperationException("登録されていないツール: " + tool);
            }

            return _edit.Call(method, arguments);
        }

        /// <summary>項目の組を作る。</summary>
        public static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] given)
        {
            return ComposedEditFixture.Arguments(given);
        }

        /// <summary>項目1つ。</summary>
        public static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedEditFixture.Given(name, value);
        }

        /// <summary>包みが添えた知らせ。添えていなければ空。</summary>
        public static IList<string> Warnings(IDictionary<string, object> envelope)
        {
            object held;

            return envelope.TryGetValue("warnings", out held)
                ? ((IEnumerable<string>)held).ToList()
                : new List<string>();
        }

        /// <summary>包みが持つ誤りの符号。</summary>
        public static string Code(IDictionary<string, object> envelope)
        {
            return ComposedEditFixture.Code(envelope);
        }

        /// <summary>包みが持つ中身。成功でなければ止まる。</summary>
        public static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            return ComposedEditFixture.Value(envelope);
        }
    }
}
