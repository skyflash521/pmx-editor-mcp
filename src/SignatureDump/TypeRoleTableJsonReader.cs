using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型ごとの役割を書いた正本を読む。</summary>
    public static class TypeRoleTableJsonReader
    {
        private const string RootsName = "connectionRoots";

        private const string TypesName = "types";

        private const string TypeNameName = "typeName";

        private const string RoleName = "role";

        private const string BasisName = "basis";

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
        /// 接続の根と型ごとの役割を、書かれた順に返す。根は1件以上で、根も型名も序数の昇順に重複なく
        /// 並ぶことを求める。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static TypeRoleTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> members = Members(Parse(json), RootsName, TypesName);

            return new TypeRoleTable(ReadRoots(members[RootsName]), ReadTypes(members[TypesName]));
        }

        private static IList<string> ReadRoots(object value)
        {
            object[] items = Array(value, RootsName);
            if (items.Length == 0)
            {
                throw new FormatException(RootsName + " は1件以上でなければならない。");
            }

            List<string> roots = new List<string>();
            foreach (object item in items)
            {
                string root = Text(item, RootsName);
                RequireAscending(roots.Count == 0 ? null : roots[roots.Count - 1], root, "根");
                roots.Add(root);
            }

            return roots;
        }

        private static IList<TypeRoleRecord> ReadTypes(object value)
        {
            List<TypeRoleRecord> records = new List<TypeRoleRecord>();
            string previous = null;
            foreach (object item in Array(value, TypesName))
            {
                TypeRoleRecord record = ReadRecord(item);
                RequireAscending(previous, record.TypeName, "型");
                previous = record.TypeName;
                records.Add(record);
            }

            return records;
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
            Dictionary<string, object> members = Members(item, TypeNameName, RoleName, BasisName);
            string text = Text(members[RoleName], RoleName);
            TypeRole role;
            if (!Roles.TryGetValue(text, out role))
            {
                throw new FormatException("知らない役割: " + text);
            }

            try
            {
                return new TypeRoleRecord(
                    Text(members[TypeNameName], TypeNameName), role, Text(members[BasisName], BasisName));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
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
