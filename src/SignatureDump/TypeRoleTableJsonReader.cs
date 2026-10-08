using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        private const string SignatureKeyName = "signatureKey";

        private const string IssuesName = "issues";

        private const string TypeNameName = "typeName";

        private const string RoleName = "role";

        private const string BasisName = "basis";

        private const string ElementNounName = "elementNoun";

        private const string ElementNounPluralName = "elementNounPlural";

        private const string GroupName = "group";

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

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                TypesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(TypeNameName, JsonForm.Text()),
                        JsonForm.Member(RoleName, JsonForm.Text()),
                        JsonForm.Member(BasisName, JsonForm.Text()),
                        JsonForm.Optional(ElementNounName, JsonForm.Text()),
                        JsonForm.Optional(ElementNounPluralName, JsonForm.Text()),
                        JsonForm.Optional(GroupName, JsonForm.Text())),
                    TypeNameName,
                    allowEmpty: true)),
            JsonForm.Member(
                IssuancesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(SignatureKeyName, JsonForm.Text()),
                        JsonForm.Member(IssuesName, JsonForm.Flag()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    SignatureKeyName,
                    allowEmpty: true)),
            JsonForm.Member(
                CollectionsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(SignatureKeyName, JsonForm.Text()),
                        JsonForm.Member(OwnsName, JsonForm.Flag()),
                        JsonForm.Member(BasisName, JsonForm.Text()),
                        JsonForm.Optional(OwnerPathName, JsonForm.Array(JsonForm.Text()))),
                    SignatureKeyName,
                    allowEmpty: true)));

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

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);

            return new TypeRoleTable(
                ReadTypes(root[TypesName]),
                Each(root[IssuancesName]).Select(ReadIssuance).ToList(),
                Each(root[CollectionsName]).Select(ReadCollection).ToList());
        }

        private static ElementCollectionRecord ReadCollection(IDictionary<string, object> members)
        {
            bool owns = (bool)members[OwnsName];
            RequireMembers(members, owns ? new[] { OwnerPathName } : new string[0], new string[0]);
            try
            {
                return new ElementCollectionRecord(
                    (string)members[SignatureKeyName],
                    owns,
                    (string)members[BasisName],
                    owns ? ((object[])members[OwnerPathName]).Cast<string>().ToList() : null);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static HandleIssuanceRecord ReadIssuance(IDictionary<string, object> members)
        {
            try
            {
                return new HandleIssuanceRecord(
                    (string)members[SignatureKeyName],
                    (bool)members[IssuesName],
                    (string)members[BasisName]);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static IList<TypeRoleRecord> ReadTypes(object value)
        {
            List<TypeRoleRecord> records = new List<TypeRoleRecord>();
            HashSet<string> nouns = new HashSet<string>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> item in Each(value))
            {
                TypeRoleRecord record = ReadRecord(item);
                RequireUnique(nouns, record.ElementNoun);
                RequireUnique(nouns, record.ElementNounPlural);
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

        private static TypeRoleRecord ReadRecord(IDictionary<string, object> members)
        {
            string text = (string)members[RoleName];
            TypeRole role;
            if (!Roles.TryGetValue(text, out role))
            {
                throw new FormatException("知らない役割: " + text);
            }

            RequireMembers(members, RequiredOptionalNamesFor(role), OptionalNamesFor(role));
            string noun = members.ContainsKey(ElementNounName)
                ? Noun((string)members[ElementNounName], ElementNounName)
                : string.Empty;
            string plural = members.ContainsKey(ElementNounPluralName)
                ? Noun((string)members[ElementNounPluralName], ElementNounPluralName)
                : string.Empty;
            CapabilityOwner group = members.ContainsKey(GroupName)
                ? ReadGroup((string)members[GroupName])
                : CapabilityOwner.None;
            try
            {
                return new TypeRoleRecord(
                    (string)members[TypeNameName],
                    role,
                    (string)members[BasisName],
                    noun,
                    plural,
                    group);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <summary>
        /// 役割ごとに、持つべき名前に加えて項目が持ってもよい名前。担当群は台帳が決める型では
        /// 書かないので、必須にせずここへ置く。
        /// </summary>
        private static string[] OptionalNamesFor(TypeRole role)
        {
            return TypeRoleRecord.HasIndependentTool(role)
                ? new[] { GroupName }
                : new string[0];
        }

        private static string[] RequiredOptionalNamesFor(TypeRole role)
        {
            if (role == TypeRole.EventArgs || role == TypeRole.Dto)
            {
                return new string[0];
            }

            if (role == TypeRole.Connector)
            {
                return new[] { ElementNounName };
            }

            return new[] { ElementNounName, ElementNounPluralName };
        }

        private static void RequireMembers(
            IDictionary<string, object> members, string[] required, string[] allowed)
        {
            foreach (string name in required.Where(n => !members.ContainsKey(n)))
            {
                throw new FormatException("項目が無い: " + name);
            }

            foreach (string name in new[] { ElementNounName, ElementNounPluralName, GroupName, OwnerPathName }
                .Where(n => members.ContainsKey(n) && !required.Contains(n) && !allowed.Contains(n)))
            {
                throw new FormatException("知らない項目がある: " + name);
            }
        }

        private static CapabilityOwner ReadGroup(string text)
        {
            CapabilityOwner group;
            if (!ToolGroups.ByToken.TryGetValue(text, out group))
            {
                throw new FormatException("知らない担当群: " + text);
            }

            return group;
        }

        /// <summary>要素名詞はツール名の一部になるので、小文字と数字と下線だけの語に限る。</summary>
        private static string Noun(string text, string name)
        {
            if (!SnakeCase.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + text);
            }

            return text;
        }

        private static IEnumerable<IDictionary<string, object>> Each(object value)
        {
            return ((object[])value).Cast<IDictionary<string, object>>();
        }
    }
}
