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
    internal sealed class ModelShape
    {
        private const int Depth = 4;

        private FakePmx _subject;

        /// <summary>
        /// モデルの中身を1つの綴りにしたもの。変わったかどうかを見るために控える。要素の持ち物は
        /// 型から辿って残らず綴るので、綴る先を足し忘れることがない。要素どうしの繋がりは、指して
        /// いる先が並びの何番目かで綴るので、指し替えも並びの入れ替えも綴りに出る。
        /// </summary>
        public string Of(FakePmx subject)
        {
            _subject = subject;
            StringBuilder made = new StringBuilder(subject.FilePath);
            Told(made, subject.Header, 0, false);
            Told(made, subject.ModelInfo, 0, false);
            Told(made, subject.RootNode, 0, false);
            Told(made, subject.ExpressionNode, 0, false);
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
                _subject.Vertex,
                _subject.Material,
                _subject.Bone,
                _subject.Morph,
                _subject.Node,
                _subject.Body,
                _subject.Joint,
                _subject.SoftBody,
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
    }
}
