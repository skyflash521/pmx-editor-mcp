using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型ごとのサンプル値の表の1行。</summary>
    public sealed class SampleValueRow
    {
        public SampleValueRow(string typeName, object first, object second)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            TypeName = typeName;
            First = first;
            Second = second;
        }

        /// <summary>値を写す型の名前。</summary>
        public string TypeName { get; }

        /// <summary>既定として使う値。</summary>
        public object First { get; }

        /// <summary>書き込む前の値が既定と一致するときに使う値。</summary>
        public object Second { get; }
    }

    /// <summary>型ごとのサンプル値の表。</summary>
    public sealed class SampleValueTable
    {
        public SampleValueTable(IList<SampleValueRow> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            Types = new ReadOnlyCollection<SampleValueRow>(types);
        }

        public IList<SampleValueRow> Types { get; }
    }

    /// <summary>型ごとのサンプル値の正本をJSONから読み取る。</summary>
    public static class SampleValueJsonReader
    {
        private const string TypesName = "types";

        private const string TypeNameName = "typeName";

        private const string DefaultName = "default";

        private const string SecondName = "second";

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static SampleValueTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> root = Members(Parse(json), TypesName);
            List<SampleValueRow> rows = new List<SampleValueRow>();
            string previous = null;
            foreach (object item in Array(root[TypesName], TypesName))
            {
                Dictionary<string, object> members =
                    Members(item, TypeNameName, DefaultName, SecondName);
                string typeName = Text(members[TypeNameName], TypeNameName);
                if (previous != null
                    && string.CompareOrdinal(previous, typeName) > 0)
                {
                    throw new FormatException("序数の昇順で並んでいない: " + typeName);
                }

                previous = typeName;
                rows.Add(new SampleValueRow(
                    typeName, members[DefaultName], members[SecondName]));
            }

            return new SampleValueTable(rows);
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

        private static object[] Array(object value, string name)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(name + " は項目の並びでなければならない。");
            }

            return items;
        }

        /// <summary>
        /// 求める項目だけを持つ対象として読む。余分な項目を黙って捨てると、正本の形が崩れても
        /// 気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

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
    }
}
