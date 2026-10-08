using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace PmxEditorMcp.SignatureDump
{
    public sealed class JsonMember
    {
        internal JsonMember(string name, JsonForm form, bool required)
        {
            PropertyRecord.RequireText(name, nameof(name));
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            Name = name;
            Form = form;
            Required = required;
        }

        public string Name { get; }

        public JsonForm Form { get; }

        /// <summary>偽なら、組が持たなくてもよい。持たない項目は読めた表に入らない。</summary>
        public bool Required { get; }
    }

    /// <summary>
    /// 正本の形の宣言。読めた値は、組なら名前から値へ引く表、並びなら値の並び、それ以外はその値に
    /// なる。
    /// </summary>
    public abstract class JsonForm
    {
        /// <summary>空でも空白だけでもない文字列。</summary>
        public static JsonForm Text()
        {
            return new TextForm();
        }

        /// <summary>空でもよい文字列。</summary>
        public static JsonForm TextOrEmpty()
        {
            return new TextOrEmptyForm();
        }

        /// <summary>1以上の整数。</summary>
        public static JsonForm Count()
        {
            return new CountForm();
        }

        /// <summary>数。読めた値は整数か小数になる。</summary>
        public static JsonForm Number()
        {
            return new NumberForm();
        }

        /// <summary>読むときに <paramref name="form"/> が返す形。</summary>
        public static JsonForm Deferred(Func<JsonForm> form)
        {
            return new DeferredForm(form);
        }

        /// <summary>真偽。</summary>
        public static JsonForm Flag()
        {
            return new FlagForm();
        }

        /// <summary>
        /// 形を問わない値。読めた値は、組なら名前から値へ引く表、並びなら値の並び、数なら整数か
        /// 小数、そのほかはその値か null になる。
        /// </summary>
        public static JsonForm Any()
        {
            return new AnyForm();
        }

        /// <summary>その形か、null。</summary>
        public static JsonForm OrNull(JsonForm inner)
        {
            return new NullableForm(inner);
        }

        /// <summary>並べた項目だけを持つ組。知らない項目も、欠けた必須の項目も形が違う。</summary>
        public static JsonForm Object(params JsonMember[] members)
        {
            return new ObjectForm(members);
        }

        /// <summary>
        /// 名前を問わない項目の組で、どの値も <paramref name="value"/> の形を持つ。読めた値は名前から
        /// 値へ引く表になる。<paramref name="allowEmpty"/> を真にすると、項目の無い組も受け付ける。
        /// </summary>
        public static JsonForm Map(JsonForm value, bool allowEmpty = false)
        {
            return new MapForm(value, allowEmpty);
        }

        /// <summary>
        /// 1件以上の並び。鍵の名前を渡すと、その項目が二度現れないことと、序数の昇順に並ぶことを
        /// 併せて求める。<paramref name="allowEmpty"/> を真にすると、1件も無い並びも受け付ける
        /// ——載せるものが無いこと自体が正しい表がある。
        /// </summary>
        public static JsonForm Array(JsonForm element, string key = null, bool allowEmpty = false)
        {
            return new ArrayForm(element, key, allowEmpty);
        }

        public static JsonMember Member(string name, JsonForm form)
        {
            return new JsonMember(name, form, true);
        }

        public static JsonMember Optional(string name, JsonForm form)
        {
            return new JsonMember(name, form, false);
        }

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public object Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object parsed;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    parsed = Plain(document.RootElement, string.Empty);
                }
            }
            catch (JsonException exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }

            return Read(parsed, string.Empty);
        }

        private static object Plain(JsonElement element, string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> members =
                        new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        string where = Where(path, property.Name);
                        if (members.ContainsKey(property.Name))
                        {
                            throw new FormatException("同じ項目が二度現れる: " + where);
                        }

                        members.Add(property.Name, Plain(property.Value, where));
                    }

                    return members;
                case JsonValueKind.Array:
                    return element.EnumerateArray()
                        .Select((item, at) => Plain(
                            item, Where(path, at.ToString(CultureInfo.InvariantCulture))))
                        .ToArray();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    int narrow;
                    if (element.TryGetInt32(out narrow))
                    {
                        return narrow;
                    }

                    long wide;
                    if (element.TryGetInt64(out wide))
                    {
                        return wide;
                    }

                    return element.GetDecimal();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        internal abstract object Read(object value, string path);

        internal static string Where(string path, string name)
        {
            return path.Length == 0 ? name : path + "." + name;
        }

        internal static FormatException Wrong(string path, string expected)
        {
            return new FormatException(
                (path.Length == 0 ? "根" : path) + " は" + expected + "でなければならない。");
        }

        private sealed class TextForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                string text = value as string;
                if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                {
                    throw Wrong(path, "空でない文字列");
                }

                return text;
            }
        }

        private sealed class AnyForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                return value;
            }
        }

        private sealed class TextOrEmptyForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                if (!(value is string))
                {
                    throw Wrong(path, "文字列");
                }

                return value;
            }
        }

        private sealed class CountForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                if (!(value is int) || (int)value < 1)
                {
                    throw Wrong(path, "1以上の整数");
                }

                return value;
            }
        }

        private sealed class NumberForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                if (!(value is int || value is long || value is decimal))
                {
                    throw Wrong(path, "数");
                }

                return value;
            }
        }

        private sealed class DeferredForm : JsonForm
        {
            private readonly Func<JsonForm> _form;

            internal DeferredForm(Func<JsonForm> form)
            {
                if (form == null)
                {
                    throw new ArgumentNullException(nameof(form));
                }

                _form = form;
            }

            internal override object Read(object value, string path)
            {
                return _form().Read(value, path);
            }
        }

        private sealed class FlagForm : JsonForm
        {
            internal override object Read(object value, string path)
            {
                if (!(value is bool))
                {
                    throw Wrong(path, "真偽");
                }

                return value;
            }
        }

        private sealed class NullableForm : JsonForm
        {
            private readonly JsonForm _inner;

            internal NullableForm(JsonForm inner)
            {
                if (inner == null)
                {
                    throw new ArgumentNullException(nameof(inner));
                }

                _inner = inner;
            }

            internal override object Read(object value, string path)
            {
                return value == null ? null : _inner.Read(value, path);
            }
        }

        private sealed class ObjectForm : JsonForm
        {
            private readonly IList<JsonMember> _members;

            internal ObjectForm(JsonMember[] members)
            {
                if (members == null || members.Length == 0)
                {
                    throw new ArgumentException("項目を1つ以上並べる。", nameof(members));
                }

                _members = members;
            }

            internal override object Read(object value, string path)
            {
                Dictionary<string, object> source = value as Dictionary<string, object>;
                if (source == null)
                {
                    throw Wrong(path, "項目の組");
                }

                Dictionary<string, object> read =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (JsonMember member in _members)
                {
                    object held;
                    if (!source.TryGetValue(member.Name, out held))
                    {
                        if (!member.Required)
                        {
                            continue;
                        }

                        throw new FormatException(
                            "項目が無い: " + Where(path, member.Name));
                    }

                    read.Add(member.Name, member.Form.Read(held, Where(path, member.Name)));
                }

                foreach (string name in source.Keys
                    .Where(n => !_members.Any(m => string.Equals(m.Name, n, StringComparison.Ordinal))))
                {
                    throw new FormatException("知らない項目がある: " + Where(path, name));
                }

                return read;
            }
        }

        private sealed class MapForm : JsonForm
        {
            private readonly JsonForm _value;

            private readonly bool _allowEmpty;

            internal MapForm(JsonForm value, bool allowEmpty)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _value = value;
                _allowEmpty = allowEmpty;
            }

            internal override object Read(object value, string path)
            {
                Dictionary<string, object> source = value as Dictionary<string, object>;
                if (source == null)
                {
                    throw Wrong(path, "項目の組");
                }

                if (source.Count == 0 && !_allowEmpty)
                {
                    throw new FormatException(
                        (path.Length == 0 ? "根" : path) + " は1件以上でなければならない。");
                }

                Dictionary<string, object> read =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> member in source)
                {
                    read.Add(member.Key, _value.Read(member.Value, Where(path, member.Key)));
                }

                return read;
            }
        }

        private sealed class ArrayForm : JsonForm
        {
            private readonly JsonForm _element;

            private readonly string _key;

            private readonly bool _allowEmpty;

            internal ArrayForm(JsonForm element, string key, bool allowEmpty)
            {
                if (element == null)
                {
                    throw new ArgumentNullException(nameof(element));
                }

                _element = element;
                _key = key;
                _allowEmpty = allowEmpty;
            }

            internal override object Read(object value, string path)
            {
                object[] items = value as object[];
                if (items == null)
                {
                    throw Wrong(path, "項目の並び");
                }

                if (items.Length == 0 && !_allowEmpty)
                {
                    throw new FormatException(
                        (path.Length == 0 ? "根" : path) + " は1件以上でなければならない。");
                }

                List<object> read = new List<object>();
                string previous = null;
                for (int at = 0; at < items.Length; at++)
                {
                    string where = Where(path, at.ToString(CultureInfo.InvariantCulture));
                    object item = _element.Read(items[at], where);
                    read.Add(item);
                    if (_key == null)
                    {
                        continue;
                    }

                    string current = (string)((Dictionary<string, object>)item)[_key];
                    if (previous != null && string.CompareOrdinal(previous, current) >= 0)
                    {
                        throw new FormatException(
                            "序数の昇順で並んでいないか二度現れる: "
                                + Where(where, _key) + "(" + current + ")");
                    }

                    previous = current;
                }

                return read.ToArray();
            }
        }
    }
}
