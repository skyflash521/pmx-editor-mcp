using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace PmxEditorMcp
{
    /// <summary>
    /// 値をJSONへ写す。写し方と写せる型は共通契約仕様書の値の表現が定める。表に載らない型は役割を
    /// 持つ型なので、ここでは写さず偽を返し、断る内容も持たせない。
    /// </summary>
    public static class ValueShape
    {
        private const string ListTypeName = "System.Collections.Generic.IList`1";

        private const string FamilyName = "family";

        private const string SizeName = "size";

        private const string StyleName = "style";

        private const string CombinedSeparator = ", ";

        private const int ColorScale = 255;

        private static readonly string[] XY = { "X", "Y" };

        private static readonly string[] XYZ = { "X", "Y", "Z" };

        private static readonly string[] XYZW = { "X", "Y", "Z", "W" };

        private static readonly string[] RowMajor =
        {
            "M11", "M12", "M13", "M14",
            "M21", "M22", "M23", "M24",
            "M31", "M32", "M33", "M34",
            "M41", "M42", "M43", "M44",
        };

        /// <summary>綴りが1つに決まる型。写し方は <see cref="TryFixed"/> が持つ。</summary>
        private static readonly HashSet<Type> Fixed = new HashSet<Type>
        {
            typeof(bool), typeof(byte), typeof(int), typeof(float), typeof(double),
            typeof(string), typeof(Version), typeof(Color), typeof(Size), typeof(Point),
            typeof(Rectangle), typeof(Font), typeof(Brush), typeof(Bitmap),
        };

        /// <summary>成分を並べる型と、並べる順の成分名。</summary>
        private static readonly Dictionary<string, string[]> Components =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "PEPlugin.SDX.V2", XY },
                { "PEPlugin.SDX.V3", XYZ },
                { "PEPlugin.SDX.V4", XYZW },
                { "PEPlugin.SDX.Q", XYZW },
                { "PEPlugin.SDX.M", RowMajor },
                { "PEPlugin.Pmd.IPEVector3", XYZ },
                { "PEPlugin.Pmd.IPEQuaternion", XYZW },
                { "SlimDX.Vector3", XYZ },
                { "SlimDX.Quaternion", XYZW },
                { "SlimDX.Matrix", RowMajor },
            };

        /// <summary>
        /// 値をJSONへ写す。写せない型なら偽を返して <paramref name="code"/> を持たせず、写せる型の
        /// 写せない値なら偽を返して断る内容を渡す。<paramref name="warnings"/> は空でも渡す。
        /// </summary>
        public static bool TryToJson(
            Type declared,
            object value,
            int maxLongSide,
            out object json,
            out IList<string> warnings,
            out string code,
            out string message)
        {
            if (declared == null)
            {
                throw new ArgumentNullException(nameof(declared));
            }

            List<string> collected = new List<string>();
            warnings = collected;

            return TryWrite(declared, value, maxLongSide, collected, out json, out code, out message);
        }

        private static bool TryWrite(
            Type declared,
            object value,
            int maxLongSide,
            List<string> warnings,
            out object json,
            out string code,
            out string message)
        {
            json = null;
            code = null;
            message = null;
            Type target = declared.IsByRef ? declared.GetElementType() : declared;
            if (target == typeof(void))
            {
                return true;
            }

            Type underlying = Nullable.GetUnderlyingType(target);
            bool optional = underlying != null;
            if (optional)
            {
                target = underlying;
            }

            if (!IsValue(target))
            {
                return false;
            }

            if (value == null)
            {
                return optional || !target.IsValueType;
            }

            Type element;
            if (TryElementType(target, out element))
            {
                return TrySequence(element, value, maxLongSide, warnings, out json, out code, out message);
            }

            if (target.IsEnum)
            {
                return TryEnum(target, value, out json, out code, out message);
            }

            if (target == typeof(object))
            {
                return TryLooseValue(value, out json, out code, out message);
            }

            string[] components;
            if (Components.TryGetValue(FullName(target), out components))
            {
                return TryComponents(target, components, value, out json, out code, out message);
            }

            return TryFixed(target, value, maxLongSide, warnings, out json, out code, out message);
        }

        /// <summary>
        /// 値として写せる型かどうか。要素の表現が決まらない並びも写せないので、要素まで見て決める。
        /// </summary>
        private static bool IsValue(Type target)
        {
            Type underlying = Nullable.GetUnderlyingType(target);
            if (underlying != null)
            {
                return IsValue(underlying);
            }

            Type element;
            if (TryElementType(target, out element))
            {
                return element == typeof(byte) || IsValue(element);
            }

            return target.IsEnum
                || target == typeof(object)
                || Components.ContainsKey(FullName(target))
                || Fixed.Contains(target);
        }

        private static bool TryFixed(
            Type target,
            object value,
            int maxLongSide,
            List<string> warnings,
            out object json,
            out string code,
            out string message)
        {
            json = null;
            code = null;
            message = null;
            if (target == typeof(bool) || target == typeof(byte) || target == typeof(int))
            {
                json = value;

                return true;
            }

            if (target == typeof(float))
            {
                return TryFinite(value, (float)value, out json, out code, out message);
            }

            if (target == typeof(double))
            {
                return TryFinite(value, (double)value, out json, out code, out message);
            }

            if (target == typeof(string))
            {
                json = value;

                return true;
            }

            if (target == typeof(Version))
            {
                json = ((Version)value).ToString();

                return true;
            }

            if (target == typeof(Color))
            {
                json = ColorOf((Color)value);

                return true;
            }

            if (target == typeof(Size))
            {
                Size size = (Size)value;
                json = new object[] { size.Width, size.Height };

                return true;
            }

            if (target == typeof(Point))
            {
                Point point = (Point)value;
                json = new object[] { point.X, point.Y };

                return true;
            }

            if (target == typeof(Rectangle))
            {
                Rectangle rectangle = (Rectangle)value;
                json = new object[] { rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height };

                return true;
            }

            if (target == typeof(Font))
            {
                return TryFont((Font)value, out json, out code, out message);
            }

            if (target == typeof(Brush))
            {
                return TryBrush((Brush)value, out json, out code, out message);
            }

            if (target == typeof(Bitmap))
            {
                EncodedImage encoded = ImageTransfer.Encode((Bitmap)value, maxLongSide);
                warnings.AddRange(encoded.Warnings);
                json = encoded.Base64;

                return true;
            }

            throw new ArgumentOutOfRangeException(nameof(target), target, "写し方を持たない型。");
        }

        /// <summary>一列に並ぶバイトはBase64へ詰め、それ以外は要素をひとつずつ写した配列とする。</summary>
        private static bool TrySequence(
            Type element,
            object value,
            int maxLongSide,
            List<string> warnings,
            out object json,
            out string code,
            out string message)
        {
            json = null;
            code = null;
            message = null;
            IEnumerable items = (IEnumerable)value;
            if (element == typeof(byte))
            {
                json = Convert.ToBase64String(BytesOf(items));

                return true;
            }

            List<object> written = new List<object>();
            foreach (object item in items)
            {
                object each;
                if (!TryWrite(element, item, maxLongSide, warnings, out each, out code, out message))
                {
                    return false;
                }

                written.Add(each);
            }

            json = written.ToArray();

            return true;
        }

        private static bool TryEnum(Type target, object value, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            if (Enum.IsDefined(target, value))
            {
                json = Enum.GetName(target, value);

                return true;
            }

            string spelled;
            if (target.IsDefined(typeof(FlagsAttribute), false) && TryCombined(target, value, out spelled))
            {
                json = spelled;

                return true;
            }

            return Refuse("当てはまる列挙子の名前が無い値なので、JSONで表せない。", out code, out message);
        }

        /// <summary>当てはまる列挙子の名前を値の小さい順に連ねる。当てはまらない部分が残れば偽。</summary>
        private static bool TryCombined(Type target, object value, out string spelled)
        {
            spelled = null;
            List<long> members = new List<long>();
            foreach (object member in Enum.GetValues(target))
            {
                members.Add(Convert.ToInt64(member, CultureInfo.InvariantCulture));
            }

            members.Sort();
            List<string> names = new List<string>();
            long left = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            foreach (long member in members)
            {
                if (member != 0 && (left & member) == member)
                {
                    names.Add(Enum.GetName(target, Enum.ToObject(target, member)));
                    left &= ~member;
                }
            }

            if (left != 0 || names.Count == 0)
            {
                return false;
            }

            spelled = string.Join(CombinedSeparator, names.ToArray());

            return true;
        }

        /// <summary>成分は宣言型から引く。インターフェースで受け取った値も同じ並びで写すため。</summary>
        private static bool TryComponents(
            Type target,
            string[] components,
            object value,
            out object json,
            out string code,
            out string message)
        {
            json = null;
            code = null;
            message = null;
            object[] written = new object[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                object component = ComponentOf(target, components[i], value);
                object each;
                if (!TryFinite(
                    component, Convert.ToDouble(component, CultureInfo.InvariantCulture),
                    out each, out code, out message))
                {
                    return false;
                }

                written[i] = each;
            }

            json = written;

            return true;
        }

        private static bool TryFont(Font font, out object json, out string code, out string message)
        {
            json = null;
            object style;
            if (!TryEnum(typeof(FontStyle), font.Style, out style, out code, out message))
            {
                return false;
            }

            object size;
            if (!TryFinite(font.SizeInPoints, font.SizeInPoints, out size, out code, out message))
            {
                return false;
            }

            Dictionary<string, object> written = new Dictionary<string, object>(StringComparer.Ordinal);
            written[FamilyName] = font.FontFamily.Name;
            written[SizeName] = size;
            written[StyleName] = style;
            json = written;

            return true;
        }

        private static bool TryBrush(Brush brush, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            SolidBrush solid = brush as SolidBrush;
            if (solid == null)
            {
                return Refuse("単色でないブラシなので、色として表せない。", out code, out message);
            }

            json = ColorOf(solid.Color);

            return true;
        }

        /// <summary>任意のJSON値として持つ型は、実行時の値がJSONの値へ写せるかで決まる。</summary>
        private static bool TryLooseValue(object value, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            if (value == null || value is bool || value is string)
            {
                json = value;

                return true;
            }

            if (IsNumber(value))
            {
                return TryFinite(
                    value, Convert.ToDouble(value, CultureInfo.InvariantCulture),
                    out json, out code, out message);
            }

            IDictionary pairs = value as IDictionary;
            if (pairs != null)
            {
                return TryLoosePairs(pairs, out json, out code, out message);
            }

            IEnumerable items = value as IEnumerable;
            if (items != null)
            {
                return TryLooseItems(items, out json, out code, out message);
            }

            return Refuse("JSONの値へ写せない.NETのオブジェクトである。", out code, out message);
        }

        private static bool TryLooseItems(IEnumerable items, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            List<object> written = new List<object>();
            foreach (object item in items)
            {
                object each;
                if (!TryLooseValue(item, out each, out code, out message))
                {
                    return false;
                }

                written.Add(each);
            }

            json = written.ToArray();

            return true;
        }

        private static bool TryLoosePairs(IDictionary pairs, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            Dictionary<string, object> written = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry pair in pairs)
            {
                string name = pair.Key as string;
                if (name == null)
                {
                    return Refuse("名前が文字列でない組なので、JSONで表せない。", out code, out message);
                }

                object each;
                if (!TryLooseValue(pair.Value, out each, out code, out message))
                {
                    return false;
                }

                written[name] = each;
            }

            json = written;

            return true;
        }

        private static bool IsNumber(object value)
        {
            switch (Convert.GetTypeCode(value))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryFinite(object value, double number, out object json, out string code, out string message)
        {
            json = null;
            code = null;
            message = null;
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                return Refuse("有限でない数値なので、JSONで表せない。", out code, out message);
            }

            json = value;

            return true;
        }

        private static bool Refuse(string reason, out string code, out string message)
        {
            code = ToolEnvelope.NotApplicable;
            message = reason;

            return false;
        }

        /// <summary>送り出す色は4成分とする。</summary>
        private static object[] ColorOf(Color color)
        {
            return new object[]
            {
                (float)color.R / ColorScale,
                (float)color.G / ColorScale,
                (float)color.B / ColorScale,
                (float)color.A / ColorScale,
            };
        }

        private static object ComponentOf(Type target, string name, object value)
        {
            PropertyInfo property = target.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(value, null);
            }

            FieldInfo field = target.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(value);
            }

            throw new InvalidOperationException("成分 " + name + " を " + FullName(target) + " が持たない。");
        }

        private static byte[] BytesOf(IEnumerable items)
        {
            byte[] array = items as byte[];
            if (array != null)
            {
                return array;
            }

            List<byte> bytes = new List<byte>();
            foreach (object item in items)
            {
                bytes.Add((byte)item);
            }

            return bytes.ToArray();
        }

        /// <summary>一列に並ぶ配列とリストだけを、要素を並べた形として扱う。</summary>
        private static bool TryElementType(Type target, out Type element)
        {
            element = null;
            if (target.IsArray)
            {
                if (target.GetArrayRank() != 1)
                {
                    return false;
                }

                element = target.GetElementType();

                return true;
            }

            if (!target.IsGenericType
                || !string.Equals(FullName(target.GetGenericTypeDefinition()), ListTypeName, StringComparison.Ordinal))
            {
                return false;
            }

            element = target.GetGenericArguments()[0];

            return true;
        }

        private static string FullName(Type target)
        {
            return target.FullName ?? target.Name;
        }
    }
}
