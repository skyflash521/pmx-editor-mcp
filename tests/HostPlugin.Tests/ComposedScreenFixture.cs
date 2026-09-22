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
    /// <summary>
    /// 画面とリストへ触るツールを呼ぶための題材一式。画面の口とリストの口を1つずつ持ち、
    /// いま相手にするPMXは複製編集の題材と同じものを返す。
    /// </summary>
    internal sealed class ComposedScreenFixture : IDisposable
    {
        /// <summary>持ち物を辿る深さの上限。これより深い入れ子は綴りへ出ない。</summary>
        private const int Depth = 4;

        private readonly ComposedEditFixture _edit = new ComposedEditFixture();

        private McpMethodTable _tools;

        private string _before;

        /// <summary>現在のPMXとして複製を返す題材。</summary>
        public FakePmx Model
        {
            get { return _edit.Model; }
        }

        /// <summary>VMDやPMXを作る相手の題材。</summary>
        public FakeHostBuilder Builder { get; } = new FakeHostBuilder();

        /// <summary>3Dビューの題材。複製編集の題材と同じ口を使う。</summary>
        public FakePmxView View
        {
            get { return _edit.View; }
        }

        /// <summary>リストを持つ画面の題材。複製編集の題材と同じ口を使う。</summary>
        public FakeFormConnector Form
        {
            get { return _edit.Form; }
        }

        public FakePartsSelect Parts { get; } = new FakePartsSelect();

        /// <summary>ビューの表示の設定の題材。</summary>
        public FakeViewSetting Setting { get; } = new FakeViewSetting();

        public FakeSubView SubView { get; } = new FakeSubView();

        /// <summary>
        /// 後片付けで、モデルの中身が呼び出しの前と同じままかを確かめる。画面へ触るツールはモデルを
        /// 変えないので、この題材を使うテストはすべてこの不変条件を通る。
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_before != null && !string.Equals(_before, Shape(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("画面のツールがモデルの中身を変えた。");
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
            _before = Shape();
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
                    () => SubView);
            }

            McpMethod method;
            if (!_tools.TryGet(tool, out method))
            {
                throw new InvalidOperationException("登録されていないツール: " + tool);
            }

            return _edit.Call(method, arguments);
        }

        /// <summary>
        /// モデルの中身を1つの綴りにしたもの。変わったかどうかを見るために控える。要素の持ち物は
        /// 型から辿って残らず綴るので、綴る先を足し忘れることがない。要素どうしの繋がりは、指して
        /// いる先が並びの何番目かで綴るので、指し替えも並びの入れ替えも綴りに出る。
        /// </summary>
        private string Shape()
        {
            StringBuilder made = new StringBuilder(Model.FilePath);
            Told(made, Model.Header, 0, false);
            Told(made, Model.ModelInfo, 0, false);
            Told(made, Model.RootNode, 0, false);
            Told(made, Model.ExpressionNode, 0, false);
            foreach (IEnumerable held in Lists())
            {
                foreach (object item in held)
                {
                    Told(made, item, 0, false);
                }
            }

            return made.ToString();
        }

        /// <summary>モデルが持つ並びのすべて。要素はこの中の位置で指し合う。</summary>
        private IList<IEnumerable> Lists()
        {
            return new IEnumerable[]
            {
                Model.Vertex,
                Model.Material,
                Model.Bone,
                Model.Morph,
                Model.Node,
                Model.Body,
                Model.Joint,
                Model.SoftBody,
            };
        }

        /// <summary>
        /// その値を綴りへ足す。持ち物として現れた並びの要素は位置だけで綴り、それ以外は持ち物を
        /// 辿って綴る。並びそのものを辿るときは <paramref name="linked"/> を偽にして、位置ではなく
        /// 中身を綴らせる。
        /// </summary>
        private void Told(StringBuilder made, object held, int depth, bool linked)
        {
            if (held == null)
            {
                made.Append("-;");

                return;
            }

            if (linked)
            {
                int at = At(held);
                if (at >= 0)
                {
                    made.Append('#').Append(at).Append(';');

                    return;
                }
            }

            if (Spelled(made, held) || depth >= Depth)
            {
                return;
            }

            IEnumerable items = held as IEnumerable;
            if (items != null)
            {
                made.Append('[');
                foreach (object item in items)
                {
                    Told(made, item, depth + 1, true);
                }

                made.Append(']');

                return;
            }

            foreach (PropertyInfo property in Readable(held))
            {
                made.Append(property.Name).Append('=');
                Told(made, property.GetValue(held, null), depth + 1, true);
            }
        }

        /// <summary>そのまま書ける値なら綴りへ足す。書けない値では偽を返す。</summary>
        private static bool Spelled(StringBuilder made, object held)
        {
            V2 pair = held as V2;
            if (pair != null)
            {
                made.Append(pair.X).Append(',').Append(pair.Y).Append(';');

                return true;
            }

            V3 spot = held as V3;
            if (spot != null)
            {
                made.Append(spot.X).Append(',').Append(spot.Y).Append(',')
                    .Append(spot.Z).Append(';');

                return true;
            }

            V4 colour = held as V4;
            if (colour != null)
            {
                made.Append(colour.X).Append(',').Append(colour.Y).Append(',')
                    .Append(colour.Z).Append(',').Append(colour.W).Append(';');

                return true;
            }

            if (!(held is string) && !(held is ValueType))
            {
                return false;
            }

            made.Append(Convert.ToString(held, CultureInfo.InvariantCulture)).Append(';');

            return true;
        }

        /// <summary>その値が持つ、引数を取らない読める持ち物。名前の順で並ぶ。</summary>
        private static IEnumerable<PropertyInfo> Readable(object held)
        {
            List<PropertyInfo> found = new List<PropertyInfo>();
            foreach (PropertyInfo property in held.GetType().GetProperties())
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    found.Add(property);
                }
            }

            found.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

            return found;
        }

        /// <summary>その値がモデルの並びに居るなら、その位置。居なければ空を表す位置。</summary>
        private int At(object held)
        {
            foreach (IEnumerable list in Lists())
            {
                int at = 0;
                foreach (object item in list)
                {
                    if (ReferenceEquals(item, held))
                    {
                        return at;
                    }

                    at++;
                }
            }

            return -1;
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
