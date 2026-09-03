using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型ごとの役割を書いた正本を読む。</summary>
    public static class TypeRoleTableJsonReader
    {
        private const string TypesName = "types";

        private const string TypeNameName = "typeName";

        private const string RoleName = "role";

        private const string BasisName = "basis";

        private const string ElementNounName = "elementNoun";

        private const string ElementNounPluralName = "elementNounPlural";

        private static readonly Regex SnakeCase = new Regex(
            "^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, TypeRole> Roles =
            new Dictionary<string, TypeRole>(StringComparer.Ordinal)
            {
                { "connector", TypeRole.Connector },
                { "eventArgs", TypeRole.EventArgs },
                { "handleTarget", TypeRole.HandleTarget },
                { "operationTarget", TypeRole.OperationTarget },
                { "dto", TypeRole.Dto },
            };

        /// <summary>
        /// 型ごとの役割を、書かれた順に返す。型名が序数の昇順に重複なく並び、要素名詞が表の中で
        /// 重複しないことを求める。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static IList<TypeRoleRecord> ReadTypeRoles(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return ReadTypes(Members(Parse(json), TypesName)[TypesName]);
        }

        private static IList<TypeRoleRecord> ReadTypes(object value)
        {
            List<TypeRoleRecord> records = new List<TypeRoleRecord>();
            HashSet<string> nouns = new HashSet<string>(StringComparer.Ordinal);
            string previous = null;
            foreach (object item in Array(value, TypesName))
            {
                TypeRoleRecord record = ReadRecord(item);
                RequireAscending(previous, record.TypeName, "型");
                RequireUnique(nouns, record.ElementNoun);
                RequireUnique(nouns, record.ElementNounPlural);
                previous = record.TypeName;
                records.Add(record);
            }

            return records;
        }

        /// <summary>
        /// 要素名詞はツール名と説明文が対象を指す語なので、単数形と複数形をまたいで表の中で一意に
        /// する。二つの型が同じ語を名乗ると、どちらのツールかが名前から決まらない。
        /// </summary>
        private static void RequireUnique(ISet<string> nouns, string noun)
        {
            if (noun.Length != 0 && !nouns.Add(noun))
            {
                throw new FormatException("同じ要素名詞が二度現れる: " + noun);
            }
        }

        private static void RequireAscending(string previous, string current, string what)
        {
            if (previous == null)
            {
                return;
            }

            int order = string.CompareOrdinal(previous, current);
            if (order == 0)
            {
                throw new FormatException("同じ" + what + "が二度現れる: " + current);
            }

            if (order > 0)
            {
                throw new FormatException("序数の昇順で並んでいない: " + current);
            }
        }

        private static TypeRoleRecord ReadRecord(object item)
        {
            TypeRole role = ReadRole(item);
            Dictionary<string, object> members = Members(item, NamesFor(role));
            string noun = members.ContainsKey(ElementNounName)
                ? Noun(members[ElementNounName], ElementNounName)
                : string.Empty;
            string plural = members.ContainsKey(ElementNounPluralName)
                ? Noun(members[ElementNounPluralName], ElementNounPluralName)
                : string.Empty;

            try
            {
                return new TypeRoleRecord(
                    Text(members[TypeNameName], TypeNameName),
                    role,
                    Text(members[BasisName], BasisName),
                    noun,
                    plural);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static TypeRole ReadRole(object item)
        {
            Dictionary<string, object> members = item as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            object value;
            if (!members.TryGetValue(RoleName, out value))
            {
                throw new FormatException("項目が無い: " + RoleName);
            }

            string text = Text(value, RoleName);
            TypeRole role;
            if (!Roles.TryGetValue(text, out role))
            {
                throw new FormatException("知らない役割: " + text);
            }

            return role;
        }

        /// <summary>役割ごとに、項目が持つべき名前。持てない名前を書くと未知の項目として弾かれる。</summary>
        private static string[] NamesFor(TypeRole role)
        {
            if (role == TypeRole.EventArgs || role == TypeRole.Dto)
            {
                return new[] { TypeNameName, RoleName, BasisName };
            }

            if (role == TypeRole.Connector)
            {
                return new[] { TypeNameName, RoleName, BasisName, ElementNounName };
            }

            return new[]
            {
                TypeNameName, RoleName, BasisName, ElementNounName, ElementNounPluralName,
            };
        }

        /// <summary>要素名詞はツール名の一部になるので、小文字と数字と下線だけの語に限る。</summary>
        private static string Noun(object value, string name)
        {
            string text = Text(value, name);
            if (!SnakeCase.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + text);
            }

            return text;
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
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw new FormatException(
                    name + " は空でない文字列でなければならない(空白だけも不可)。");
            }

            return text;
        }
    }
}
