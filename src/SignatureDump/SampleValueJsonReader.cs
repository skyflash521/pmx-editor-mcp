using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>ファイルの位置で渡すときの決めごと。中身はその型のサンプル値から採る。</summary>
    public sealed class SampleFile
    {
        public SampleFile(string kind, string extension, string purpose)
        {
            Kind = kind;
            Extension = extension;
            Purpose = purpose;
        }

        /// <summary>ファイルの種別。検査はこの名前で引くので、表の中で二度現れない。</summary>
        public string Kind { get; }

        /// <summary>書き出すときに付ける拡張子。点から始まる。</summary>
        public string Extension { get; }

        /// <summary>何に渡すためのものか。</summary>
        public string Purpose { get; }
    }

    /// <summary>型ごとのサンプル値の表の1行。</summary>
    public sealed class SampleValueRow
    {
        public SampleValueRow(string typeName, object first, object second, SampleFile file = null)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            TypeName = typeName;
            First = first;
            Second = second;
            File = file;
        }

        /// <summary>値を写す型の名前。</summary>
        public string TypeName { get; }

        /// <summary>既定として使う値。</summary>
        public object First { get; }

        /// <summary>書き込む前の値が既定と一致するときに使う値。</summary>
        public object Second { get; }

        /// <summary>ファイルの位置で渡せる型だけが持つ決めごと。ほかの型では null。</summary>
        public SampleFile File { get; }
    }

    /// <summary>行ごとに渡す値の表の1行。</summary>
    public sealed class SampleCallRow
    {
        public SampleCallRow(
            string signatureKey,
            IDictionary<string, object> arguments,
            string basis,
            string refused = null,
            string says = null)
        {
            PropertyRecord.RequireText(signatureKey, nameof(signatureKey));
            PropertyRecord.RequireText(basis, nameof(basis));
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            SignatureKey = signatureKey;
            Arguments = new ReadOnlyDictionary<string, object>(arguments);
            Basis = basis;
            Refused = refused;
            Says = says;
        }

        /// <summary>値を渡す相手の行キー。</summary>
        public string SignatureKey { get; }

        /// <summary>その行を呼ぶときに渡す引数。</summary>
        public IDictionary<string, object> Arguments { get; }

        /// <summary>その値を選んだ根拠の一文。</summary>
        public string Basis { get; }

        /// <summary>
        /// 渡した値を断ることを確かめるときの、断る理由の綴り。呼び出しが成り立つ値を検査の側で
        /// 用意できない行だけが持ち、ほかは null。
        /// </summary>
        public string Refused { get; }

        /// <summary>
        /// 断る理由を見分ける文面。断ることを確かめる行だけが持ち、ほかは null。綴りだけでは
        /// 狙った理由とほかの失敗を見分けられないので、文面まで一致を求める。
        /// </summary>
        public string Says { get; }
    }

    /// <summary>型ごとのサンプル値と、行ごとに渡す値の表。</summary>
    public sealed class SampleValueTable
    {
        public SampleValueTable(IList<SampleValueRow> types, IList<SampleCallRow> calls = null)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            Types = new ReadOnlyCollection<SampleValueRow>(types);
            Calls = new ReadOnlyCollection<SampleCallRow>(calls ?? new SampleCallRow[0]);
        }

        public IList<SampleValueRow> Types { get; }

        /// <summary>
        /// 行ごとに渡す値。型から決められる最小の値では意味を成さない呼び出しだけが持つ
        /// ——在りもしないファイルの位置や、要素を持たない立体の大きさになってしまう。
        /// </summary>
        public IList<SampleCallRow> Calls { get; }
    }

    /// <summary>型ごとのサンプル値の正本をJSONから読み取る。</summary>
    public static class SampleValueJsonReader
    {
        private const string TypesName = "types";

        private const string RowsName = "rows";

        private const string SignatureKeyName = "signatureKey";

        private const string ArgumentsName = "arguments";

        private const string BasisName = "basis";

        private const string RefusedName = "refused";

        private const string SaysName = "says";

        private const string TypeNameName = "typeName";

        private const string DefaultName = "default";

        private const string SecondName = "second";

        private const string FileName = "file";

        private const string KindName = "kind";

        private const string ExtensionName = "extension";

        private const string PurposeName = "purpose";

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static SampleValueTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> root = Members(Parse(json), TypesName, RowsName);
            List<SampleValueRow> rows = new List<SampleValueRow>();
            HashSet<string> kinds = new HashSet<string>(StringComparer.Ordinal);
            string previous = null;
            foreach (object item in Array(root[TypesName], TypesName))
            {
                Dictionary<string, object> members =
                    Members(item, new[] { TypeNameName, DefaultName, SecondName }, FileName);
                string typeName = Text(members[TypeNameName], TypeNameName);
                if (previous != null
                    && string.CompareOrdinal(previous, typeName) > 0)
                {
                    throw new FormatException("序数の昇順で並んでいない: " + typeName);
                }

                previous = typeName;
                SampleFile file = File(members);
                if (file != null && !kinds.Add(file.Kind))
                {
                    throw new FormatException("ファイルの種別が二度現れる: " + file.Kind);
                }

                rows.Add(new SampleValueRow(
                    typeName, members[DefaultName], members[SecondName], file));
            }

            return new SampleValueTable(rows, Calls(root));
        }

        /// <summary>行ごとに渡す値。行キーの序数の昇順で並ぶ。</summary>
        private static IList<SampleCallRow> Calls(Dictionary<string, object> root)
        {
            List<SampleCallRow> calls = new List<SampleCallRow>();
            string previous = null;
            foreach (object item in Array(root[RowsName], RowsName))
            {
                Dictionary<string, object> members = Members(
                    item,
                    new[] { SignatureKeyName, ArgumentsName, BasisName },
                    RefusedName,
                    SaysName);
                string key = Text(members[SignatureKeyName], SignatureKeyName);
                if (previous != null && string.CompareOrdinal(previous, key) > 0)
                {
                    throw new FormatException("序数の昇順で並んでいない: " + key);
                }

                previous = key;
                Dictionary<string, object> arguments =
                    members[ArgumentsName] as Dictionary<string, object>;
                if (arguments == null)
                {
                    throw new FormatException(ArgumentsName + " は項目の組でなければならない。");
                }

                object refused;
                object says;
                bool denies = members.TryGetValue(RefusedName, out refused);
                if (denies != members.TryGetValue(SaysName, out says))
                {
                    throw new FormatException(
                        RefusedName + " と " + SaysName + " は揃って書く: " + key);
                }

                calls.Add(new SampleCallRow(
                    key,
                    arguments,
                    Text(members[BasisName], BasisName),
                    denies ? Text(refused, RefusedName) : null,
                    denies ? Text(says, SaysName) : null));
            }

            return calls;
        }

        /// <summary>ファイルの決めごと。持たない行では null。拡張子は点から始まる。</summary>
        private static SampleFile File(Dictionary<string, object> members)
        {
            object value;
            if (!members.TryGetValue(FileName, out value))
            {
                return null;
            }

            Dictionary<string, object> file = Members(value, KindName, ExtensionName, PurposeName);
            string extension = Text(file[ExtensionName], ExtensionName);
            if (!extension.StartsWith(".", StringComparison.Ordinal) || extension.Length < 2)
            {
                throw new FormatException(ExtensionName + " は点から始まらなければならない。");
            }

            return new SampleFile(
                Text(file[KindName], KindName), extension, Text(file[PurposeName], PurposeName));
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
            return Members(value, names, new string[0]);
        }

        /// <summary>
        /// 求める項目と、在ってもよい項目だけを持つ対象として読む。余分な項目を黙って捨てると、
        /// 正本の形が崩れても気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(
            object value, string[] names, params string[] optional)
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
                if (!names.Contains(name, StringComparer.Ordinal)
                    && !optional.Contains(name, StringComparer.Ordinal))
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
