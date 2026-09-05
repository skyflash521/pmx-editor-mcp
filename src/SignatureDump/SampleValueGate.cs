using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>1つの列挙が持つ列挙子と、それらを組み合わせた値を許すかどうか。</summary>
    public sealed class EnumMemberSet
    {
        public EnumMemberSet(ISet<string> names, bool isCombinable)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            Names = names;
            IsCombinable = isCombinable;
        }

        public ISet<string> Names { get; }

        /// <summary>列挙子の名前を連ねた綴りを受け取れるか。</summary>
        public bool IsCombinable { get; }
    }

    /// <summary>
    /// 型ごとのサンプル値の表を、値を写す型の集合と、型ごとの表現へ照合する。表に載らない型が
    /// 残ると、その型を取る行の検査で渡す値が決まらない。
    /// </summary>
    public static class SampleValueGate
    {
        /// <summary>成分を並べる表現の綴り。</summary>
        private const string NumberArrayShape = "number_array";

        /// <summary>値を持てない表現の綴り。渡せる値が無いので、サンプル値も持たない。</summary>
        private const string NullShape = "null_value";

        /// <summary>書体の飾りを写す列挙の名前。</summary>
        private const string FontStyleTypeName = "System.Drawing.FontStyle";

        /// <summary>標準の表記で書く型の名前。</summary>
        private const string VersionTypeName = "System.Version";

        /// <summary>組み合わせを許す列挙で、列挙子を連ねる区切り。</summary>
        private static readonly string[] MemberSeparator = { ", " };

        /// <summary>合わなければ <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            SampleValueTable table,
            IList<ValueShapeRow> shapes,
            IDictionary<string, int> components,
            IDictionary<string, EnumMemberSet> enumMembers)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (shapes == null)
            {
                throw new ArgumentNullException(nameof(shapes));
            }

            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            if (enumMembers == null)
            {
                throw new ArgumentNullException(nameof(enumMembers));
            }

            IDictionary<string, string> byType = Shapes(shapes);
            RequireSameComponents(byType, components);
            RequireSameTypes(table, byType);
            foreach (SampleValueRow row in table.Types.OrderBy(r => r.TypeName, StringComparer.Ordinal))
            {
                RequireDifferent(row);
                string shape = byType[row.TypeName];
                Require(row, row.First, "default", shape, components, enumMembers);
                Require(row, row.Second, "second", shape, components, enumMembers);
            }
        }

        /// <summary>
        /// 成分の数の表が、成分を並べる表現の型と過不足なく対応することを求める。片方にしか無い型は、
        /// 並びの長さを誰も決めていないか、決めた長さが誰にも掛からないかのどちらかになる。
        /// </summary>
        private static void RequireSameComponents(
            IDictionary<string, string> byType, IDictionary<string, int> components)
        {
            HashSet<string> arrays = new HashSet<string>(
                byType.Where(p => string.Equals(p.Value, NumberArrayShape, StringComparison.Ordinal))
                    .Select(p => p.Key),
                StringComparer.Ordinal);

            string missing = arrays.Except(components.Keys, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("成分の数を持たない型がある: " + missing);
            }

            string extra = components.Keys.Except(arrays, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("成分を並べない型の成分の数がある: " + extra);
            }
        }

        /// <summary>
        /// 表の型が、値を写す型のうちサンプル値を持てるものと過不足なく対応することを求める。
        /// </summary>
        private static void RequireSameTypes(
            SampleValueTable table, IDictionary<string, string> byType)
        {
            HashSet<string> wanted = new HashSet<string>(
                byType.Where(p => p.Value != null
                        && !string.Equals(p.Value, NullShape, StringComparison.Ordinal))
                    .Select(p => p.Key),
                StringComparer.Ordinal);
            HashSet<string> listed = new HashSet<string>(
                table.Types.Select(r => r.TypeName), StringComparer.Ordinal);
            if (listed.Count != table.Types.Count)
            {
                throw new InvalidOperationException("同じ型が二度現れる。");
            }

            string missing = wanted.Except(listed, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("サンプル値を持たない型がある: " + missing);
            }

            string extra = listed.Except(wanted, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("値を写す型でないもののサンプル値がある: " + extra);
            }
        }

        /// <summary>2件が違う値であることを求める。同じでは、書き換えたかどうかを見分けられない。</summary>
        private static void RequireDifferent(SampleValueRow row)
        {
            if (string.Equals(Written(row.First), Written(row.Second), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("2件のサンプル値が同じ: " + row.TypeName);
            }
        }

        private static void Require(
            SampleValueRow row,
            object value,
            string name,
            string shape,
            IDictionary<string, int> components,
            IDictionary<string, EnumMemberSet> enumMembers)
        {
            string place = row.TypeName + " の " + name;
            switch (shape)
            {
                case "boolean":
                    RequireThat(value is bool, place + " は真偽でなければならない。");
                    return;

                case "number":
                    RequireNumber(row.TypeName, value, place);
                    return;

                case "text":
                    RequireText(row.TypeName, value, place);
                    return;

                case "json":
                    RequireThat(value != null, place + " は値を持たなければならない。");
                    return;

                case "enum_name":
                    RequireEnumName(row.TypeName, value, place, enumMembers);
                    return;

                case "image":
                    RequireImage(value, place);
                    return;

                case NumberArrayShape:
                    RequireNumbers(value, components[row.TypeName], place);
                    return;

                case "color":
                case "brush":
                    RequireColor(value, place);
                    return;

                case "size":
                case "point":
                    RequireNumbers(value, 2, place);
                    return;

                case "rectangle":
                    RequireNumbers(value, 4, place);
                    return;

                case "font":
                    RequireFont(value, place, enumMembers);
                    return;

                default:
                    throw new InvalidOperationException("サンプル値を確かめられない綴り: " + shape);
            }
        }

        private static void RequireEnumName(
            string typeName,
            object value,
            string place,
            IDictionary<string, EnumMemberSet> enumMembers)
        {
            string name = value as string;
            RequireThat(name != null, place + " は列挙子の名前でなければならない。");

            EnumMemberSet members;
            if (!enumMembers.TryGetValue(typeName, out members))
            {
                throw new InvalidOperationException("列挙子を引けない型がある: " + typeName);
            }

            string[] parts = name.Split(MemberSeparator, StringSplitOptions.None);
            RequireThat(
                parts.Length == 1 || members.IsCombinable,
                place + " が、組み合わせを許さない列挙へ名前を連ねている。");
            foreach (string part in parts)
            {
                RequireThat(members.Names.Contains(part), place + " に無い列挙子を書いている: " + part);
            }
        }

        private static void RequireImage(object value, string place)
        {
            string text = value as string;
            RequireThat(text != null, place + " はBase64の文字列でなければならない。");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(text);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(place + " がBase64として読めない。");
            }

            RequireThat(bytes.Length != 0, place + " が空である。");
        }

        private static void RequireNumbers(object value, int count, string place)
        {
            object[] items = value as object[];
            RequireThat(
                items != null && items.Length == count,
                place + " は数値 " + count.ToString(CultureInfo.InvariantCulture) + " 個の並びでなければならない。");
            foreach (object item in items)
            {
                RequireThat(IsNumber(item), place + " の成分が数値でない。");
            }
        }

        private static void RequireColor(object value, string place)
        {
            object[] items = value as object[];
            RequireThat(
                items != null && (items.Length == 3 || items.Length == 4),
                place + " は数値3個か4個の並びでなければならない。");
            foreach (object item in items)
            {
                RequireThat(IsNumber(item), place + " の成分が数値でない。");
                double component = Convert.ToDouble(item, CultureInfo.InvariantCulture);
                RequireThat(
                    component >= 0 && component <= 1, place + " の成分が0以上1以下でない。");
            }
        }

        private static void RequireNumber(string typeName, object value, string place)
        {
            RequireThat(IsNumber(value), place + " は数値でなければならない。");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            RequireThat(
                !double.IsNaN(number) && !double.IsInfinity(number),
                place + " が有限でない。");

            switch (typeName)
            {
                case "System.Byte":
                    RequireWhole(number, byte.MinValue, byte.MaxValue, place);
                    return;

                case "System.Int32":
                    RequireWhole(number, int.MinValue, int.MaxValue, place);
                    return;

                case "System.Single":
                    RequireThat(
                        !float.IsInfinity((float)number), place + " がその型の範囲を超えている。");
                    return;

                case "System.Double":
                    return;

                default:
                    throw new InvalidOperationException(
                        "持てる範囲を確かめられない型がある: " + typeName);
            }
        }

        private static void RequireWhole(double number, double least, double most, string place)
        {
            RequireThat(number == Math.Floor(number), place + " が整数でない。");
            RequireThat(number >= least && number <= most, place + " がその型の範囲を超えている。");
        }

        private static void RequireText(string typeName, object value, string place)
        {
            string text = value as string;
            RequireThat(text != null, place + " は文字列でなければならない。");

            if (string.Equals(typeName, VersionTypeName, StringComparison.Ordinal))
            {
                Version version;
                RequireThat(Version.TryParse(text, out version), place + " が版の表記でない。");
            }
        }

        private static void RequireFont(
            object value, string place, IDictionary<string, EnumMemberSet> enumMembers)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            RequireThat(members != null, place + " は組でなければならない。");
            RequireThat(
                members.Count == 3 && members.ContainsKey("family") && members.ContainsKey("size")
                    && members.ContainsKey("style"),
                place + " は family と size と style だけを持たなければならない。");
            RequireThat(members["family"] is string, place + " の family が文字列でない。");
            RequireThat(IsNumber(members["size"]), place + " の size が数値でない。");
            RequireThat(
                Convert.ToDouble(members["size"], CultureInfo.InvariantCulture) > 0,
                place + " の size が0より大きくない。");
            RequireEnumName(
                FontStyleTypeName, members["style"], place + " の style", enumMembers);
        }

        private static IDictionary<string, string> Shapes(IList<ValueShapeRow> shapes)
        {
            Dictionary<string, string> byType = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ValueShapeRow row in shapes)
            {
                if (byType.ContainsKey(row.TypeName))
                {
                    throw new InvalidOperationException("同じ型が二度現れる: " + row.TypeName);
                }

                byType.Add(row.TypeName, row.Shape);
            }

            return byType;
        }

        private static bool IsNumber(object value)
        {
            return value is int || value is long || value is decimal || value is double;
        }

        private static string Written(object value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }

        private static void RequireThat(bool held, string message)
        {
            if (!held)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
