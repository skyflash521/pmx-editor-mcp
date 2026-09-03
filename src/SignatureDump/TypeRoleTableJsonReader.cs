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

        private const string IssuancesName = "issuances";

        private const string CollectionsName = "collections";

        private const string OwnsName = "owns";

        private const string OwnerPathName = "ownerPath";

        private const string ConcreteTypesName = "concreteTypes";

        private const string SignatureKeyName = "signatureKey";

        private const string IssuesName = "issues";

        private const string KindName = "kind";

        private const string TypeNameName = "typeName";

        private const string RoleName = "role";

        private const string BasisName = "basis";

        private const string ElementNounName = "elementNoun";

        private const string ElementNounPluralName = "elementNounPlural";

        private const string ConnectionPathName = "connectionPath";

        private const string GroupName = "group";

        private const string ToolsName = "tools";

        private static readonly Regex SnakeCase = new Regex(
            "^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, HandleIssuanceKind> Kinds =
            new Dictionary<string, HandleIssuanceKind>(StringComparer.Ordinal)
            {
                { "constructor", HandleIssuanceKind.Constructor },
                { "factory", HandleIssuanceKind.Factory },
                { "receiverBound", HandleIssuanceKind.ReceiverBound },
            };

        private static readonly Dictionary<string, ToolVerb> Verbs =
            new Dictionary<string, ToolVerb>(StringComparer.Ordinal)
            {
                { "get", ToolVerb.Get },
                { "list", ToolVerb.List },
                { "update", ToolVerb.Update },
                { "add", ToolVerb.Add },
                { "remove", ToolVerb.Remove },
            };

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
        /// 型ごとの役割と、ハンドル発行の判定と、要素を並べるリストの判定を、書かれた順に返す。
        /// 型名と行キーが序数の昇順に重複なく並び、要素名詞が表の中で重複しないことを求める。形が
        /// 違えば <see cref="FormatException"/>。
        /// </summary>
        public static TypeRoleTable ReadTypeRoles(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> members = Members(
                Parse(json), TypesName, IssuancesName, CollectionsName);

            return new TypeRoleTable(
                ReadTypes(members[TypesName]),
                ReadIssuances(members[IssuancesName]),
                ReadCollections(members[CollectionsName]));
        }

        private static IList<ElementCollectionRecord> ReadCollections(object value)
        {
            List<ElementCollectionRecord> records = new List<ElementCollectionRecord>();
            string previous = null;
            foreach (object item in Array(value, CollectionsName))
            {
                bool owns = Owned(item);
                Dictionary<string, object> members = Members(
                    item,
                    owns
                        ? new[] { SignatureKeyName, OwnsName, BasisName, OwnerPathName }
                        : new[] { SignatureKeyName, OwnsName, BasisName },
                    new[] { ConcreteTypesName });
                ElementCollectionRecord record;
                try
                {
                    record = new ElementCollectionRecord(
                        Text(members[SignatureKeyName], SignatureKeyName),
                        owns,
                        Text(members[BasisName], BasisName),
                        owns ? ReadPath(members[OwnerPathName]) : null,
                        members.ContainsKey(ConcreteTypesName)
                            ? ReadConcreteTypes(members[ConcreteTypesName])
                            : null);
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException(exception.Message, exception);
                }

                RequireAscending(previous, record.SignatureKey, "行キー");
                previous = record.SignatureKey;
                records.Add(record);
            }

            return records;
        }

        private static IList<HandleIssuanceRecord> ReadIssuances(object value)
        {
            List<HandleIssuanceRecord> records = new List<HandleIssuanceRecord>();
            string previous = null;
            foreach (object item in Array(value, IssuancesName))
            {
                HandleIssuanceRecord record = ReadIssuance(item);
                RequireAscending(previous, record.SignatureKey, "行キー");
                previous = record.SignatureKey;
                records.Add(record);
            }

            return records;
        }

        private static HandleIssuanceRecord ReadIssuance(object item)
        {
            bool issues = Flag(item);
            Dictionary<string, object> members = Members(
                item,
                issues
                    ? new[] { SignatureKeyName, IssuesName, KindName, BasisName }
                    : new[] { SignatureKeyName, IssuesName, BasisName },
                new string[0]);
            HandleIssuanceKind? kind = null;
            if (issues)
            {
                string text = Text(members[KindName], KindName);
                HandleIssuanceKind read;
                if (!Kinds.TryGetValue(text, out read))
                {
                    throw new FormatException("知らない発行の種別: " + text);
                }

                kind = read;
            }

            try
            {
                return new HandleIssuanceRecord(
                    Text(members[SignatureKeyName], SignatureKeyName),
                    issues,
                    kind,
                    Text(members[BasisName], BasisName));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static bool Owned(object item)
        {
            Dictionary<string, object> members = item as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            object value;
            if (!members.TryGetValue(OwnsName, out value))
            {
                throw new FormatException("項目が無い: " + OwnsName);
            }

            return Flag(value, OwnsName);
        }

        private static IList<string> ReadPath(object value)
        {
            object[] items = Array(value, OwnerPathName);
            if (items.Length == 0)
            {
                throw new FormatException(OwnerPathName + " は1件以上でなければならない。");
            }

            return items.Select(i => Text(i, OwnerPathName)).ToList();
        }

        /// <summary>
        /// 許容する具象型。要素の型を継承する型が在るかどうかは他の項目を見なければ決まらないので、
        /// 在ってもよい形にする。書くなら1件以上を序数の昇順で重複なく並べる。
        /// </summary>
        private static IList<string> ReadConcreteTypes(object value)
        {
            object[] items = Array(value, ConcreteTypesName);
            if (items.Length == 0)
            {
                throw new FormatException(ConcreteTypesName + " は1件以上でなければならない。");
            }

            List<string> names = items.Select(i => Text(i, ConcreteTypesName)).ToList();
            string previous = null;
            foreach (string name in names)
            {
                RequireAscending(previous, name, ConcreteTypesName + " の型");
                previous = name;
            }

            return names;
        }

        private static bool Flag(object item)
        {
            Dictionary<string, object> members = item as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            object value;
            if (!members.TryGetValue(IssuesName, out value))
            {
                throw new FormatException("項目が無い: " + IssuesName);
            }

            return Flag(value, IssuesName);
        }

        private static bool Flag(object value, string name)
        {
            if (!(value is bool))
            {
                throw new FormatException(name + " は真偽でなければならない。");
            }

            return (bool)value;
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
            Dictionary<string, object> members = Members(
                item, NamesFor(role), OptionalNamesFor(role));
            string noun = members.ContainsKey(ElementNounName)
                ? Noun(members[ElementNounName], ElementNounName)
                : string.Empty;
            string plural = members.ContainsKey(ElementNounPluralName)
                ? Noun(members[ElementNounPluralName], ElementNounPluralName)
                : string.Empty;
            string path = members.ContainsKey(ConnectionPathName)
                ? Text(members[ConnectionPathName], ConnectionPathName)
                : string.Empty;
            CapabilityOwner group = members.ContainsKey(GroupName)
                ? ReadGroup(members[GroupName])
                : CapabilityOwner.None;
            IDictionary<ToolVerb, string> tools = members.ContainsKey(ToolsName)
                ? ReadTools(members[ToolsName], role)
                : new Dictionary<ToolVerb, string>();

            try
            {
                return new TypeRoleRecord(
                    Text(members[TypeNameName], TypeNameName),
                    role,
                    Text(members[BasisName], BasisName),
                    noun,
                    plural,
                    path,
                    group,
                    tools);
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

        /// <summary>役割ごとに、項目が持つべき名前。欠けると項目が無いとして弾かれる。</summary>
        private static string[] NamesFor(TypeRole role)
        {
            if (role == TypeRole.EventArgs || role == TypeRole.Dto)
            {
                return new[] { TypeNameName, RoleName, BasisName };
            }

            if (role == TypeRole.Connector)
            {
                return new[]
                {
                    TypeNameName, RoleName, BasisName, ElementNounName, GroupName, ToolsName,
                };
            }

            return new[]
            {
                TypeNameName, RoleName, BasisName, ElementNounName, ElementNounPluralName,
                GroupName, ToolsName,
            };
        }

        /// <summary>
        /// 役割ごとに、持つべき名前に加えて項目が持ってもよい名前。接続の経路は在るかどうかが読む時点
        /// では決まらないので、必須にせずここへ置く。
        /// </summary>
        private static string[] OptionalNamesFor(TypeRole role)
        {
            return role == TypeRole.Connector
                ? new[] { ConnectionPathName }
                : new string[0];
        }

        /// <summary>
        /// はたらきごとのツール名。役割ごとに持つべきはたらきが決まり、所有するリストの要素かどうかで
        /// 決まる2つは在ってもよい形にする——要素かどうかは他の項目を見なければ決まらない。
        /// </summary>
        private static IDictionary<ToolVerb, string> ReadTools(object value, TypeRole role)
        {
            string[] required = role == TypeRole.Connector
                ? new[] { "get", "update" }
                : new[] { "list", "update" };
            string[] optional = role == TypeRole.Connector
                ? new string[0]
                : new[] { "add", "remove" };
            Dictionary<string, object> members = Members(value, required, optional);
            Dictionary<ToolVerb, string> tools = new Dictionary<ToolVerb, string>();
            foreach (KeyValuePair<string, object> member in members)
            {
                tools.Add(Verbs[member.Key], Noun(member.Value, ToolsName));
            }

            if (tools.ContainsKey(ToolVerb.Add) != tools.ContainsKey(ToolVerb.Remove))
            {
                throw new FormatException(
                    ToolsName + " の add と remove は揃って持つか、揃って持たないかにする。");
            }

            return tools;
        }

        private static CapabilityOwner ReadGroup(object value)
        {
            string text = Text(value, GroupName);
            CapabilityOwner group;
            if (!ToolGroups.ByToken.TryGetValue(text, out group))
            {
                throw new FormatException("知らない担当群: " + text);
            }

            return group;
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
            return Members(value, names, new string[0]);
        }

        private static Dictionary<string, object> Members(
            object value, string[] names, string[] optional)
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
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw new FormatException(
                    name + " は空でない文字列でなければならない(空白だけも不可)。");
            }

            return text;
        }
    }
}
