using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;

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

            object[] items = Array(Members(Parse(json), SignaturesName)[SignaturesName]);
            List<ExcludedSignatureRecord> records = new List<ExcludedSignatureRecord>();
            string previous = null;

            foreach (object item in items)
            {
                ExcludedSignatureRecord record = ReadRecord(item);
                if (previous != null)
                {
                    int order = string.CompareOrdinal(previous, record.Key);
                    if (order == 0)
                    {
                        throw new FormatException("同じ行キーが二度現れる: " + record.Key);
                    }

                    if (order > 0)
                    {
                        throw new FormatException("序数の昇順で並んでいない: " + record.Key);
                    }
                }

                previous = record.Key;
                records.Add(record);
            }

            return new ReadOnlyCollection<ExcludedSignatureRecord>(records);
        }

        private static ExcludedSignatureRecord ReadRecord(object item)
        {
            Dictionary<string, object> members = item as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            object qualification;
            if (!members.TryGetValue(QualificationName, out qualification))
            {
                throw new FormatException("項目が無い: " + QualificationName);
            }

            string text = Text(qualification, QualificationName);
            try
            {
                if (string.Equals(text, BaselineText, StringComparison.Ordinal))
                {
                    Members(item, KeyName, QualificationName, CapabilityIdName);
                    return ExcludedSignatureRecord.FromBaseline(
                        Text(members[KeyName], KeyName),
                        Text(members[CapabilityIdName], CapabilityIdName));
                }

                if (string.Equals(text, CategoryText, StringComparison.Ordinal))
                {
                    string alternative = members.ContainsKey(AlternativeName)
                        ? Text(members[AlternativeName], AlternativeName)
                        : string.Empty;
                    Members(
                        item,
                        alternative.Length == 0
                            ? new[] { KeyName, QualificationName, CategoryName }
                            : new[] { KeyName, QualificationName, CategoryName, AlternativeName });
                    return ExcludedSignatureRecord.FromCategory(
                        Text(members[KeyName], KeyName), Category(members[CategoryName]), alternative);
                }
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }

            throw new FormatException("知らない資格: " + text);
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
                throw new FormatException(SignaturesName + " は項目の並びでなければならない。");
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

        private static ExclusionCategory Category(object value)
        {
            string text = Text(value, CategoryName);
            ExclusionCategory category;
            if (!Categories.TryGetValue(text, out category))
            {
                throw new FormatException("知らないカテゴリ: " + text);
            }

            return category;
        }
    }
}
