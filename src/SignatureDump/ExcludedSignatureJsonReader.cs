using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>除外一覧の正本をJSONから読み取る。</summary>
    public static class ExcludedSignatureJsonReader
    {
        private const string SignaturesName = "signatures";

        private const string KeyName = "key";

        private const string QualificationName = "qualification";

        private const string CapabilityIdName = "capabilityId";

        private const string CategoryName = "category";

        private const string AlternativeName = "alternative";

        private const string BaselineText = "baseline";

        private const string CategoryText = "category";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                SignaturesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(KeyName, JsonForm.Text()),
                        JsonForm.Member(QualificationName, JsonForm.Text()),
                        JsonForm.Optional(CapabilityIdName, JsonForm.Text()),
                        JsonForm.Optional(CategoryName, JsonForm.Text()),
                        JsonForm.Optional(AlternativeName, JsonForm.Text())),
                    KeyName,
                    allowEmpty: true)));

        private static readonly Dictionary<string, ExclusionCategory> Categories =
            new Dictionary<string, ExclusionCategory>(StringComparer.Ordinal)
            {
                { "pmd", ExclusionCategory.Pmd },
                { "pmdModel", ExclusionCategory.PmdModel },
                { "cPluginArgument", ExclusionCategory.CPluginArgument },
                { "delegate", ExclusionCategory.Delegate },
                { "constructorDuplicate", ExclusionCategory.ConstructorDuplicate },
            };

        /// <summary>
        /// 書かれた順に返す。行キーは序数の昇順で重複が無いことを求める。形が違えば
        /// <see cref="FormatException"/>。
        /// </summary>
        public static IList<ExcludedSignatureRecord> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<ExcludedSignatureRecord> records = ((object[])root[SignaturesName])
                .Cast<IDictionary<string, object>>()
                .Select(ReadRecord)
                .ToList();

            return new ReadOnlyCollection<ExcludedSignatureRecord>(records);
        }

        private static ExcludedSignatureRecord ReadRecord(IDictionary<string, object> members)
        {
            string key = (string)members[KeyName];
            string text = (string)members[QualificationName];
            try
            {
                if (string.Equals(text, BaselineText, StringComparison.Ordinal))
                {
                    Require(members, CapabilityIdName, CategoryName, AlternativeName);
                    return ExcludedSignatureRecord.FromBaseline(
                        key, (string)members[CapabilityIdName]);
                }

                if (string.Equals(text, CategoryText, StringComparison.Ordinal))
                {
                    Require(members, CategoryName, CapabilityIdName);
                    object alternative;
                    return ExcludedSignatureRecord.FromCategory(
                        key,
                        Category((string)members[CategoryName]),
                        members.TryGetValue(AlternativeName, out alternative)
                            ? (string)alternative
                            : string.Empty);
                }
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }

            throw new FormatException("知らない資格: " + text);
        }

        /// <summary>その資格が求める項目が無いか、持てない項目があれば <see cref="FormatException"/>。</summary>
        private static void Require(
            IDictionary<string, object> members, string required, params string[] forbidden)
        {
            if (!members.ContainsKey(required))
            {
                throw new FormatException("項目が無い: " + required);
            }

            foreach (string name in forbidden.Where(members.ContainsKey))
            {
                throw new FormatException("この資格が持てない項目がある: " + name);
            }
        }

        private static ExclusionCategory Category(string text)
        {
            ExclusionCategory category;
            if (!Categories.TryGetValue(text, out category))
            {
                throw new FormatException("知らないカテゴリ: " + text);
            }

            return category;
        }
    }
}
