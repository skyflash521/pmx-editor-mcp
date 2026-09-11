using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

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

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static IList<CapabilityRecord> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> root = Members(Parse(json), SourceName, CapabilitiesName);
            Dictionary<string, object> source = Members(
                root[SourceName],
                DistributionName,
                AssemblyName,
                AssemblyVersionName,
                FrameworkName);
            foreach (string name in source.Keys)
            {
                Text(source[name], name);
            }

            List<CapabilityRecord> records = new List<CapabilityRecord>();
            foreach (object item in Array(root[CapabilitiesName], CapabilitiesName))
            {
                records.Add(Build(Members(
                    item,
                    IdName,
                    CategoryName,
                    TargetName,
                    StatusName,
                    OwnerName,
                    RemarksName)));
            }

            return records.AsReadOnly();
        }

        private static CapabilityRecord Build(Dictionary<string, object> members)
        {
            string id = Text(members[IdName], IdName);
            string target = Text(members[TargetName], TargetName);
            CapabilityTargetKind kind = ClassifyTarget(target);
            CapabilityStatus status = Lookup(
                Statuses, Optional(members[StatusName], StatusName), "分類", id);
            CapabilityOwner owner = Lookup(
                Owners, Optional(members[OwnerName], OwnerName), "担当", id);
            RequireOwnerMatchesStatus(id, status, owner);

            return new CapabilityRecord(
                id,
                Optional(members[CategoryName], CategoryName),
                target,
                kind,
                ExtractNames(target, kind),
                status,
                owner,
                Optional(members[RemarksName], RemarksName));
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

        /// <summary>空でよい項目。書かれていない値は空文字列として読む。</summary>
        private static string Optional(object value, string name)
        {
            string text = value as string;
            if (text == null)
            {
                throw new FormatException(name + " は文字列でなければならない。");
            }

            return text;
        }
    }
}
