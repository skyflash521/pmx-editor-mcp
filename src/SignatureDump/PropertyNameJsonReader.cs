using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>日本語名の正本をJSONから読み取る。</summary>
    public static class PropertyNameJsonReader
    {
        private const string PropertyNamesName = "propertyNames";

        private const string DeclaringTypeName = "declaringType";

        private const string MemberNameName = "memberName";

        private const string PropertyTypeName = "propertyType";

        private const string JapaneseNameName = "japaneseName";

        private const string DecisionName = "decision";

        private const string BasisName = "basis";

        private const string OriginName = "origin";

        private const string KindName = "kind";

        private const string PathName = "path";

        private const string FirstLineName = "firstLine";

        private const string LastLineName = "lastLine";

        private const string QuotedText = "quoted";

        private const string AuthoredText = "authored";

        private const string DocumentSectionText = "documentSection";

        private const string MemberShapeText = "memberShape";

        /// <summary>
        /// 日本語名を書かれた順に返す。並びは宣言型・メンバー名の序数の昇順で、重複が無いことを
        /// 求める(<see cref="RoleTypeProperties"/> の並びと同じ定義)。形が違えば
        /// <see cref="FormatException"/>。
        /// </summary>
        public static IList<PropertyNameRecord> ReadPropertyNames(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object[] items = Array(Members(Parse(json), PropertyNamesName)[PropertyNamesName]);
            List<PropertyNameRecord> records = new List<PropertyNameRecord>();
            PropertyRecord previous = null;

            foreach (object item in items)
            {
                PropertyNameRecord record = ReadRecord(item);
                if (previous != null)
                {
                    int order = Order(previous, record.Property);
                    if (order == 0)
                    {
                        throw new FormatException("同じ項目が二度現れる: " + record.Property.Key);
                    }

                    if (order > 0)
                    {
                        throw new FormatException("序数の昇順で並んでいない: " + record.Property.Key);
                    }
                }

                previous = record.Property;
                records.Add(record);
            }

            return new ReadOnlyCollection<PropertyNameRecord>(records);
        }

        private static int Order(PropertyRecord left, PropertyRecord right)
        {
            int order = string.CompareOrdinal(left.DeclaringType, right.DeclaringType);
            return order != 0 ? order : string.CompareOrdinal(left.MemberName, right.MemberName);
        }

        private static PropertyNameRecord ReadRecord(object item)
        {
            Dictionary<string, object> members = Dictionary(item);
            object decision;
            if (!members.TryGetValue(DecisionName, out decision))
            {
                throw new FormatException("項目が無い: " + DecisionName);
            }

            string text = Text(decision, DecisionName);
            try
            {
                if (string.Equals(text, QuotedText, StringComparison.Ordinal))
                {
                    Members(
                        item,
                        DeclaringTypeName,
                        MemberNameName,
                        PropertyTypeName,
                        JapaneseNameName,
                        DecisionName);
                    return PropertyNameRecord.FromQuoted(
                        Property(members), Text(members[JapaneseNameName], JapaneseNameName));
                }

                if (string.Equals(text, AuthoredText, StringComparison.Ordinal))
                {
                    Members(
                        item,
                        DeclaringTypeName,
                        MemberNameName,
                        PropertyTypeName,
                        JapaneseNameName,
                        DecisionName,
                        BasisName,
                        OriginName);
                    return PropertyNameRecord.FromAuthored(
                        Property(members),
                        Text(members[JapaneseNameName], JapaneseNameName),
                        Basis(members[BasisName]),
                        Text(members[OriginName], OriginName));
                }
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }

            throw new FormatException("知らない決め方: " + text);
        }

        private static PropertyRecord Property(IDictionary<string, object> members)
        {
            return new PropertyRecord(
                Text(members[DeclaringTypeName], DeclaringTypeName),
                Text(members[MemberNameName], MemberNameName),
                Text(members[PropertyTypeName], PropertyTypeName));
        }

        private static NameBasis Basis(object value)
        {
            Dictionary<string, object> members = Dictionary(value);
            object kind;
            if (!members.TryGetValue(KindName, out kind))
            {
                throw new FormatException("項目が無い: " + KindName);
            }

            string text = Text(kind, KindName);
            if (string.Equals(text, DocumentSectionText, StringComparison.Ordinal))
            {
                Members(value, KindName, PathName, FirstLineName, LastLineName);
                return NameBasis.FromDocumentSection(
                    Text(members[PathName], PathName),
                    Number(members[FirstLineName], FirstLineName),
                    Number(members[LastLineName], LastLineName));
            }

            if (string.Equals(text, MemberShapeText, StringComparison.Ordinal))
            {
                Members(value, KindName);
                return NameBasis.FromMemberShape();
            }

            throw new FormatException("知らない根拠の種別: " + text);
        }

        private static object Parse(string json)
        {
            try
            {
                return new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }
        }

        private static object[] Array(object value)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(PropertyNamesName + " は項目の並びでなければならない。");
            }

            return items;
        }

        private static Dictionary<string, object> Dictionary(object value)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            return members;
        }

        /// <summary>
        /// 求める項目だけを持つ対象として読む。余分な項目を黙って捨てると、正本の形が崩れても
        /// 気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            Dictionary<string, object> members = Dictionary(value);
            foreach (string name in names)
            {
                if (!members.ContainsKey(name))
                {
                    throw new FormatException("項目が無い: " + name);
                }
            }

            foreach (string name in members.Keys)
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }
            }

            return members;
        }

        private static string Text(object value, string name)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException(name + " は空でない文字列でなければならない。");
            }

            return text;
        }

        private static int Number(object value, string name)
        {
            if (!(value is int))
            {
                throw new FormatException(name + " は整数でなければならない。");
            }

            return System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
