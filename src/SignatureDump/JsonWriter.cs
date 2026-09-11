using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 組み立てたJSONを書き出す。並べた順をそのまま書くので、同じ入力からは同じ綴りが出る
    /// ——指紋を取る相手なので、順が動くと中身が同じでも別物になる。
    /// </summary>
    public sealed class JsonObjectText
    {
        private readonly List<KeyValuePair<string, string>> _members =
            new List<KeyValuePair<string, string>>();

        /// <summary>組を綴る。空の組は波括弧だけになる。</summary>
        public string Text
        {
            get
            {
                StringBuilder text = new StringBuilder("{");
                for (int i = 0; i < _members.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(',');
                    }

                    text.Append(JsonText.Quote(_members[i].Key)).Append(':').Append(_members[i].Value);
                }

                return text.Append('}').ToString();
            }
        }

        public bool IsEmpty
        {
            get { return _members.Count == 0; }
        }

        public JsonObjectText Add(string name, string json)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            _members.Add(new KeyValuePair<string, string>(name, json));

            return this;
        }

        public JsonObjectText AddText(string name, string value)
        {
            return Add(name, JsonText.Quote(value));
        }

        public JsonObjectText AddNumber(string name, double value)
        {
            return Add(name, JsonWriter.Number(value));
        }

        public JsonObjectText AddBoolean(string name, bool value)
        {
            return Add(name, value ? "true" : "false");
        }
    }

    /// <summary>JSONの値を綴る。</summary>
    public static class JsonWriter
    {
        /// <summary>整数で表せる数値は小数点を付けずに綴る。</summary>
        public static string Number(double value)
        {
            if (value == Math.Floor(value) && !double.IsInfinity(value))
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>並びを綴る。</summary>
        public static string Array(IEnumerable<string> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            StringBuilder text = new StringBuilder("[");
            bool first = true;
            foreach (string item in items)
            {
                if (!first)
                {
                    text.Append(',');
                }

                text.Append(item);
                first = false;
            }

            return text.Append(']').ToString();
        }

        /// <summary>文字列の並びを綴る。</summary>
        public static string TextArray(IEnumerable<string> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<string> quoted = new List<string>();
            foreach (string value in values)
            {
                quoted.Add(JsonText.Quote(value));
            }

            return Array(quoted);
        }
    }
}
