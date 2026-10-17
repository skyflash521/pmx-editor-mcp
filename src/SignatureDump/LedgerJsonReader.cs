using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の正本をJSONから読み取る。名前が型かメンバーかまでは決めない
    /// (<see cref="CapabilityRecord.TargetNames"/> を見よ)。読めない項目に出会ったら、読み飛ばさず
    /// 例外にする。
    /// </summary>
    public static class LedgerJsonReader
    {
        private const string SourceName = "source";

        private const string CapabilitiesName = "capabilities";

        private const string DistributionName = "distribution";

        private const string AssemblyName = "assembly";

        private const string AssemblyVersionName = "assemblyVersion";

        private const string FrameworkName = "framework";

        private const string IdName = "id";

        private const string CategoryName = "category";

        private const string TargetName = "target";

        private const string StatusName = "status";

        private const string OwnerName = "owner";

        private const string RemarksName = "remarks";

        private const string GroupSeparator = " / ";

        private const char PatternMark = '*';

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                SourceName,
                JsonForm.Object(
                    JsonForm.Member(DistributionName, JsonForm.Text()),
                    JsonForm.Member(AssemblyName, JsonForm.Text()),
                    JsonForm.Member(AssemblyVersionName, JsonForm.Text()),
                    JsonForm.Member(FrameworkName, JsonForm.Text()))),
            JsonForm.Member(
                CapabilitiesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(IdName, JsonForm.Text()),
                        JsonForm.Member(CategoryName, JsonForm.TextOrEmpty()),
                        JsonForm.Member(TargetName, JsonForm.Text()),
                        JsonForm.Member(StatusName, JsonForm.TextOrEmpty()),
                        JsonForm.Member(OwnerName, JsonForm.TextOrEmpty()),
                        JsonForm.Member(RemarksName, JsonForm.TextOrEmpty())),
                    allowEmpty: true)));

        /// <summary>型引数の数は1以上で、先頭に0を置いた書き方もしない。</summary>
        private static readonly Regex GenericAritySuffix =
            new Regex("`[1-9][0-9]*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, CapabilityStatus> Statuses =
            new Dictionary<string, CapabilityStatus>(StringComparer.Ordinal)
            {
                { "提供", CapabilityStatus.Provided },
                { "非対応", CapabilityStatus.NotSupported },
                { "要調査", CapabilityStatus.NeedsInvestigation },
            };

        private static readonly Dictionary<string, CapabilityOwner> Owners =
            new Dictionary<string, CapabilityOwner>(StringComparer.Ordinal)
            {
                { string.Empty, CapabilityOwner.None },
                { "モデル", CapabilityOwner.Model },
                { "セッション", CapabilityOwner.Session },
                { "ビュー", CapabilityOwner.View },
                { "変形・モーション", CapabilityOwner.MotionTransform },
            };

        /// <summary>台帳の分類の綴りが表す分類。知らない綴りなら <see cref="FormatException"/>。</summary>
        public static CapabilityStatus StatusOf(string spelling, string id)
        {
            return Lookup(Statuses, spelling, "分類", id);
        }

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static IList<CapabilityRecord> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<CapabilityRecord> records = ((object[])root[CapabilitiesName])
                .Cast<IDictionary<string, object>>()
                .Select(Build)
                .ToList();

            return records.AsReadOnly();
        }

        private static CapabilityRecord Build(IDictionary<string, object> members)
        {
            string id = (string)members[IdName];
            string target = (string)members[TargetName];
            CapabilityTargetKind kind = ClassifyTarget(target);
            CapabilityStatus status = Lookup(Statuses, (string)members[StatusName], "分類", id);
            CapabilityOwner owner = Lookup(Owners, (string)members[OwnerName], "担当", id);
            RequireOwnerMatchesStatus(id, status, owner);

            return new CapabilityRecord(
                id,
                (string)members[CategoryName],
                target,
                kind,
                ExtractNames(target, kind),
                status,
                owner,
                (string)members[RemarksName]);
        }

        private static CapabilityTargetKind ClassifyTarget(string target)
        {
            if (target.IndexOf(PatternMark) >= 0)
            {
                return CapabilityTargetKind.Pattern;
            }

            return target.IndexOf(GroupSeparator, StringComparison.Ordinal) >= 0
                ? CapabilityTargetKind.Group
                : CapabilityTargetKind.Single;
        }

        private static ReadOnlyCollection<string> ExtractNames(
            string target, CapabilityTargetKind kind)
        {
            if (kind == CapabilityTargetKind.Pattern)
            {
                return System.Array.AsReadOnly(new string[0]);
            }

            string[] names = kind == CapabilityTargetKind.Group
                ? target.Split(new[] { GroupSeparator }, StringSplitOptions.None)
                : new[] { target };

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = GenericAritySuffix.Replace(names[i].Trim(), string.Empty);
            }

            return System.Array.AsReadOnly(names);
        }

        /// <summary>表に無い語は既定値へ倒さず例外にする。</summary>
        private static TValue Lookup<TValue>(
            Dictionary<string, TValue> table, string value, string columnName, string id)
        {
            TValue found;
            if (!table.TryGetValue(value, out found))
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の{1}に知らない値がある: {2}",
                    id,
                    columnName,
                    value));
            }

            return found;
        }

        private static void RequireOwnerMatchesStatus(
            string id, CapabilityStatus status, CapabilityOwner owner)
        {
            if ((status == CapabilityStatus.Provided) == (owner != CapabilityOwner.None))
            {
                return;
            }

            throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                "{0} は分類と担当が食い違う。担当は分類が提供の行にだけ書く: 分類={1} 担当={2}",
                id,
                status,
                owner));
        }
    }
}
