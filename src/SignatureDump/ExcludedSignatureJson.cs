using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 除外一覧をJSONへ書き出す。同じ入力からは常に同じバイト列になり、1件が1行に収まるので、
    /// 行単位の差分で除外の増減を追える。
    /// </summary>
    public static class ExcludedSignatureJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(IList<ExcludedSignatureRecord> records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("\"signatures\":");
            if (records.Count == 0)
            {
                builder.Append("[]");
            }
            else
            {
                builder.Append("[\n");
                builder.Append(string.Join(",\n", records.Select(WriteRecord)));
                builder.Append("\n]");
            }

            builder.Append("\n}\n");

            return builder.ToString();
        }

        private static string WriteRecord(ExcludedSignatureRecord record)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"key\":").Append(Text(record.Key));
            builder.Append(",\"qualification\":").Append(Text(Name(record.Qualification)));

            if (record.Qualification == ExclusionQualification.Baseline)
            {
                builder.Append(",\"capabilityId\":").Append(Text(record.CapabilityId));
            }
            else
            {
                builder.Append(",\"category\":").Append(Text(Name(record.Category)));
                if (record.Alternative.Length != 0)
                {
                    builder.Append(",\"alternative\":").Append(Text(record.Alternative));
                }
            }

            return builder.Append("}").ToString();
        }

        /// <summary>
        /// 読む側は綴りで分岐するので、列挙子の名前を変えても書き出す綴りは動かさない。
        /// </summary>
        private static string Name(ExclusionQualification qualification)
        {
            switch (qualification)
            {
                case ExclusionQualification.Baseline:
                    return "baseline";
                case ExclusionQualification.Category:
                    return "category";
                default:
                    throw new ArgumentOutOfRangeException(nameof(qualification));
            }
        }

        private static string Name(ExclusionCategory category)
        {
            switch (category)
            {
                case ExclusionCategory.Pmd:
                    return "pmd";
                case ExclusionCategory.CPluginArgument:
                    return "cPluginArgument";
                case ExclusionCategory.Delegate:
                    return "delegate";
                case ExclusionCategory.ConstructorDuplicate:
                    return "constructorDuplicate";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private static string Text(string value)
        {
            return JsonText.Quote(value);
        }
    }
}
