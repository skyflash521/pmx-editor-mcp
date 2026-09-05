using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace PmxEditorMcp
{
    /// <summary>
    /// JSONから値を組み立てる。写せる型と綴りの判定は <see cref="ValueShape"/> が持ち、こちらは
    /// その逆向きだけを持つ。形が合わない値は、共通契約仕様書の値の表現のとおり断る。
    /// </summary>
    public static class ValueInput
    {
        private const string FamilyName = "family";

        private const string SizeName = "size";

        private const string StyleName = "style";

        private const string CombinedSeparator = ", ";

        private const int ColorScale = 255;

        private const int ColorWithoutAlpha = 3;

        private const int ColorWithAlpha = 4;

        /// <summary>書体の組が持てる項目の名前。</summary>
        private static readonly string[] FontNames = { FamilyName, SizeName, StyleName };

        /// <summary>成分を並べるインターフェースと、その値として組み立てる型。</summary>
        private static readonly Dictionary<string, Type> Concretes =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { "PEPlugin.Pmd.IPEVector3", typeof(PEPlugin.SDX.V3) },
                { "PEPlugin.Pmd.IPEQuaternion", typeof(PEPlugin.SDX.Q) },
            };

        /// <summary>
        /// JSONから値を組み立てる。値として写せない型なら偽を返して <paramref name="code"/> を
        /// 持たせず、形が合わない値なら偽を返して断る内容を渡す。組み立てたものが使い捨てを要るとき
        /// (画像・書体・ブラシ)、手放すのは受け取った側である。
        /// </summary>
        public static bool TryFromJson(
            Type declared, object json, out object value, out string code, out string message)
        {
            if (declared == null)
            {
                throw new ArgumentNullException(nameof(declared));
            }

            value = null;
            code = null;
            message = null;
            Type target = declared.IsByRef ? declared.GetElementType() : declared;
            if (target == typeof(void) || !ValueShape.IsValue(target))
            {
                return false;
            }

            Type underlying = Nullable.GetUnderlyingType(target);
            bool optional = underlying != null;
            if (optional)
            {
                target = underlying;
            }

            if (json == null)
            {
                return optional || !target.IsValueType
                    || Invalid("値が無い。この綴りは値が要る。", out code, out message);
            }

            return TryRead(target, json, out value, out code, out message);
        }

        private static bool TryRead(Type target, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            Type element;
            if (ValueShape.TryElementTypeOf(target, out element))
            {
                return TrySequence(target, element, json, out value, out code, out message);
            }

            if (target.IsEnum)
            {
                return TryEnum(target, json, out value, out code, out message);
            }

            if (target == typeof(object))
            {
                value = json;

                return IsLooseValue(json, out code, out message) || Clear(out value);
            }

            IList<string> components;
            if (ValueShape.TryComponentNames(target, out components))
            {
                return TryComponents(target, components, json, out value, out code, out message);
            }

            return TryFixed(target, json, out value, out code, out message);
        }

        private static bool TryFixed(Type target, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            if (target == typeof(bool))
            {
                return json is bool
                    ? Take(json, out value)
                    : Invalid("真偽でない。", out code, out message);
            }

            if (target == typeof(byte) || target == typeof(int)
                || target == typeof(float) || target == typeof(double))
            {
                return TryNumber(target, json, out value, out code, out message);
            }

            if (target == typeof(string))
            {
                return json is string
                    ? Take(json, out value)
                    : Invalid("文字列でない。", out code, out message);
            }

            if (target == typeof(Version))
            {
                return TryVersion(json, out value, out code, out message);
            }

            if (target == typeof(Color))
            {
                Color color;
                if (!TryColor(json, out color, out code, out message))
                {
                    return false;
                }

                value = color;

                return true;
            }

            if (target == typeof(Size) || target == typeof(Point) || target == typeof(Rectangle))
            {
                object[] whole;
                int length = target == typeof(Rectangle) ? 4 : 2;
                if (!TryNumbers(typeof(int), json, length, out whole, out code, out message))
                {
                    return false;
                }

                value = target == typeof(Size) ? (object)new Size((int)whole[0], (int)whole[1])
                    : target == typeof(Point) ? (object)new Point((int)whole[0], (int)whole[1])
                    : new Rectangle((int)whole[0], (int)whole[1], (int)whole[2], (int)whole[3]);

                return true;
            }

            if (target == typeof(Font))
            {
                return TryFont(json, out value, out code, out message);
            }

            if (target == typeof(Brush))
            {
                Color color;
                if (!TryColor(json, out color, out code, out message))
                {
                    return false;
                }

                value = new SolidBrush(color);

                return true;
            }

            if (target == typeof(Bitmap))
            {
                return TryImage(json, out value, out code, out message);
            }

            throw new ArgumentOutOfRangeException(nameof(target), target, "組み立て方を持たない型。");
        }

        /// <summary>一列に並ぶバイトはBase64の文字列から解き、それ以外は要素をひとつずつ読む。</summary>
        private static bool TrySequence(
            Type target, Type element, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            if (element == typeof(byte))
            {
                byte[] bytes;
                if (!TryBytes(json, out bytes, out code, out message))
                {
                    return false;
                }

                value = target.IsArray ? (object)bytes : new List<byte>(bytes);

                return true;
            }

            object[] items = json as object[];
            if (items == null)
            {
                return Invalid("要素を並べた配列でない。", out code, out message);
            }

            Array read = Array.CreateInstance(element, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                object each;
                if (!TryFromJson(element, items[i], out each, out code, out message))
                {
                    Release(read);

                    return false;
                }

                read.SetValue(each, i);
            }

            value = target.IsArray ? (object)read : ListOf(element, read);

            return true;
        }

        private static bool TryEnum(Type target, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            string spelled = json as string;
            if (spelled == null)
            {
                return Invalid("列挙子の名前は文字列で渡す。", out code, out message);
            }

            string[] parts = spelled.Split(new[] { CombinedSeparator }, StringSplitOptions.None);
            if (parts.Length > 1 && !target.IsDefined(typeof(FlagsAttribute), false))
            {
                return Invalid("組み合わせを許さない列挙に、名前を並べた綴りを渡した。", out code, out message);
            }

            long bits = 0;
            foreach (string part in parts)
            {
                if (Array.IndexOf(Enum.GetNames(target), part) < 0)
                {
                    return Invalid("当てはまる列挙子の無い名前 " + part + " を渡した。", out code, out message);
                }

                bits |= Convert.ToInt64(Enum.Parse(target, part), CultureInfo.InvariantCulture);
            }

            value = Enum.ToObject(target, bits);

            return true;
        }

        private static bool TryComponents(
            Type target, IList<string> components, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            object[] numbers;
            if (!TryNumbers(typeof(float), json, components.Count, out numbers, out code, out message))
            {
                return false;
            }

            Type concrete;
            if (!Concretes.TryGetValue(FullName(target), out concrete))
            {
                concrete = target;
            }

            object built = Activator.CreateInstance(concrete);
            for (int i = 0; i < components.Count; i++)
            {
                SetComponent(concrete, components[i], built, (float)numbers[i]);
            }

            value = built;

            return true;
        }

        private static bool TryFont(object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            IDictionary pairs = json as IDictionary;
            if (pairs == null)
            {
                return Invalid("書体は組で渡す。", out code, out message);
            }

            foreach (object name in pairs.Keys)
            {
                if (Array.IndexOf(FontNames, name as string) < 0)
                {
                    return Invalid("書体の組が知らない項目を持つ。", out code, out message);
                }
            }

            string family = pairs[FamilyName] as string;
            if (family == null || family.Length == 0)
            {
                return Invalid("書体名が無い。", out code, out message);
            }

            // 名前だけで組み立てると、知らない書体は黙って別の書体に置き換わる。
            try
            {
                new FontFamily(family).Dispose();
            }
            catch (ArgumentException)
            {
                return Invalid("導入されていない書体名 " + family + " を渡した。", out code, out message);
            }

            object size;
            if (!TryNumber(typeof(float), pairs[SizeName], out size, out code, out message))
            {
                return false;
            }

            if ((float)size <= 0f)
            {
                return Invalid("書体の大きさが0以下である。", out code, out message);
            }

            object style;
            if (!TryEnum(typeof(FontStyle), pairs[StyleName], out style, out code, out message))
            {
                return false;
            }

            try
            {
                value = new Font(family, (float)size, (FontStyle)style, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                return Invalid(
                    "書体 " + family + " をその大きさとスタイルで組み立てられない。", out code, out message);
            }

            return true;
        }

        private static bool TryImage(object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            string packed = json as string;
            if (packed == null)
            {
                return Invalid("画像はBase64の文字列で渡す。", out code, out message);
            }

            Bitmap image;
            string reason;
            if (!ImageTransfer.TryDecode(packed, out image, out reason))
            {
                return Invalid(reason, out code, out message);
            }

            value = image;

            return true;
        }

        /// <summary>任意のJSON値は、入れ子の隅までJSONの値の形をしていればそのまま持つ。</summary>
        private static bool IsLooseValue(object json, out string code, out string message)
        {
            code = null;
            message = null;
            if (json == null || json is bool || json is string || IsNumber(json))
            {
                return true;
            }

            IDictionary pairs = json as IDictionary;
            if (pairs != null)
            {
                foreach (DictionaryEntry pair in pairs)
                {
                    if (!(pair.Key is string))
                    {
                        return Invalid("名前が文字列でない組である。", out code, out message);
                    }

                    if (!IsLooseValue(pair.Value, out code, out message))
                    {
                        return false;
                    }
                }

                return true;
            }

            object[] items = json as object[];
            if (items == null)
            {
                return Invalid("JSONの値の形をしていない。", out code, out message);
            }

            foreach (object item in items)
            {
                if (!IsLooseValue(item, out code, out message))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryNumber(Type target, object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            if (!IsNumber(json))
            {
                return Invalid("数値でない。", out code, out message);
            }

            double number = Convert.ToDouble(json, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                return Invalid("有限でない数値である。", out code, out message);
            }

            if (target == typeof(float))
            {
                float narrowed = (float)number;

                return float.IsInfinity(narrowed)
                    ? Invalid("単精度で持てる範囲を超えている。", out code, out message)
                    : Take(narrowed, out value);
            }

            if (target == typeof(double))
            {
                value = number;

                return true;
            }

            if (number != Math.Floor(number))
            {
                return Invalid("整数でない。", out code, out message);
            }

            if (target == typeof(byte))
            {
                return number < byte.MinValue || number > byte.MaxValue
                    ? Invalid("バイトで持てる範囲を超えている。", out code, out message)
                    : Take((byte)number, out value);
            }

            return number < int.MinValue || number > int.MaxValue
                ? Invalid("整数で持てる範囲を超えている。", out code, out message)
                : Take((int)number, out value);
        }

        /// <summary>決まった数の成分を、その綴りが持つ型で読む。</summary>
        private static bool TryNumbers(
            Type element, object json, int length, out object[] numbers, out string code, out string message)
        {
            numbers = null;
            code = null;
            message = null;
            object[] items = json as object[];
            if (items == null || items.Length != length)
            {
                return Invalid(
                    "成分を " + length.ToString(CultureInfo.InvariantCulture) + " つ並べた配列でない。",
                    out code, out message);
            }

            object[] read = new object[length];
            for (int i = 0; i < length; i++)
            {
                if (!TryNumber(element, items[i], out read[i], out code, out message))
                {
                    return false;
                }
            }

            numbers = read;

            return true;
        }

        /// <summary>受け取る色は3成分と4成分のどちらも許し、3成分は不透明として読む。</summary>
        private static bool TryColor(object json, out Color color, out string code, out string message)
        {
            color = default(Color);
            code = null;
            message = null;
            object[] items = json as object[];
            if (items == null || (items.Length != ColorWithoutAlpha && items.Length != ColorWithAlpha))
            {
                return Invalid("色は成分を3つか4つ並べた配列で渡す。", out code, out message);
            }

            object[] numbers;
            if (!TryNumbers(typeof(float), json, items.Length, out numbers, out code, out message))
            {
                return false;
            }

            foreach (object number in numbers)
            {
                if ((float)number < 0f || (float)number > 1f)
                {
                    return Invalid("色の成分が0以上1以下でない。", out code, out message);
                }
            }

            int alpha = numbers.Length == ColorWithAlpha ? Round(numbers[ColorWithoutAlpha]) : ColorScale;
            color = Color.FromArgb(alpha, Round(numbers[0]), Round(numbers[1]), Round(numbers[2]));

            return true;
        }

        private static bool TryBytes(object json, out byte[] bytes, out string code, out string message)
        {
            bytes = null;
            code = null;
            message = null;
            string packed = json as string;
            if (packed == null)
            {
                return Invalid("一列に並ぶバイトはBase64の文字列で渡す。", out code, out message);
            }

            try
            {
                bytes = Convert.FromBase64String(packed);
            }
            catch (FormatException)
            {
                return Invalid("Base64として読めない。", out code, out message);
            }

            return true;
        }

        private static bool TryVersion(object json, out object value, out string code, out string message)
        {
            value = null;
            code = null;
            message = null;
            string spelled = json as string;
            Version version;
            if (spelled == null || !Version.TryParse(spelled, out version))
            {
                return Invalid("版の標準の表記でない。", out code, out message);
            }

            value = version;

            return true;
        }

        private static object ListOf(Type element, Array read)
        {
            object list = Activator.CreateInstance(typeof(List<>).MakeGenericType(element), read.Length);
            MethodInfo add = list.GetType().GetMethod("Add");
            foreach (object item in read)
            {
                add.Invoke(list, new[] { item });
            }

            return list;
        }

        private static void SetComponent(Type target, string name, object built, float number)
        {
            PropertyInfo property = target.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(built, number, null);

                return;
            }

            FieldInfo field = target.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("成分 " + name + " を " + FullName(target) + " が持たない。");
            }

            field.SetValue(built, number);
        }

        /// <summary>JSONの値が数値かどうか。文字列や真偽値を変換で数値へ化けさせないために先に見る。</summary>
        internal static bool IsNumber(object json)
        {
            switch (Convert.GetTypeCode(json))
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

        private static int Round(object number)
        {
            return (int)Math.Round((float)number * ColorScale, MidpointRounding.AwayFromZero);
        }

        /// <summary>途中で断るときに、それまでに組み立てた使い捨てのものを手放す。</summary>
        private static void Release(Array read)
        {
            foreach (object each in read)
            {
                IDisposable disposable = each as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }

        private static bool Clear(out object value)
        {
            value = null;

            return false;
        }

        private static bool Take(object read, out object value)
        {
            value = read;

            return true;
        }

        private static bool Invalid(string reason, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = reason;

            return false;
        }

        private static string FullName(Type target)
        {
            return target.FullName ?? target.Name;
        }
    }
}
