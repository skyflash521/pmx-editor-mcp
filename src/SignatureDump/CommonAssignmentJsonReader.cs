using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>共通契約割当の正本を読む。</summary>
    public static class CommonAssignmentJsonReader
    {
        private const string AssignmentsName = "assignments";

        private const string SignatureKeyName = "signatureKey";

        private const string AssignmentName = "assignment";

        private const string TargetName = "target";

        private const string BasisName = "basis";

        private static readonly Regex ToolName = new Regex(
            "^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Regex ArgumentName = new Regex(
            "^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, CommonAssignmentKind> Kinds =
            new Dictionary<string, CommonAssignmentKind>(StringComparer.Ordinal)
            {
                { "tool", CommonAssignmentKind.Tool },
                { "commonArg", CommonAssignmentKind.CommonArg },
                { "internalFlow", CommonAssignmentKind.InternalFlow },
            };

        /// <summary>
        /// 内部フローへの割当の対象名になる流れ。ハンドル解放はツールが受け持つのでここに無い。
        /// </summary>
        private static readonly Dictionary<string, string> Flows =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "duplicateEdit", "複製編集" },
                { "stateRead", "状態取得" },
                { "connect", "接続初期化" },
            };

        /// <summary>
        /// 共通契約割当を書かれた順に返す。行キーが序数の昇順に重複なく並ぶことを求める。形が違えば
        /// <see cref="FormatException"/>。
        /// </summary>
        public static CommonAssignmentTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object parsed;
            try
            {
                parsed = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }

            List<CommonAssignmentRecord> records = new List<CommonAssignmentRecord>();
            string previous = null;
            foreach (object item in Array(Members(parsed, AssignmentsName)[AssignmentsName]))
            {
                CommonAssignmentRecord record = ReadRecord(item);
                RequireAscending(previous, record.SignatureKey);
                previous = record.SignatureKey;
                records.Add(record);
            }

            return new CommonAssignmentTable(records);
        }

        private static CommonAssignmentRecord ReadRecord(object item)
        {
            Dictionary<string, object> members = Members(
                item, SignatureKeyName, AssignmentName, TargetName, BasisName);
            CommonAssignmentKind assignment = ReadAssignmentKind(members[AssignmentName]);
            string target = ReadAssignmentTarget(members[TargetName], assignment);

            try
            {
                return new CommonAssignmentRecord(
                    Text(members[SignatureKeyName], SignatureKeyName),
                    assignment,
                    target,
                    Text(members[BasisName], BasisName));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <summary>割当の種別を読む。</summary>
        private static CommonAssignmentKind ReadAssignmentKind(object value)
        {
            string text = Text(value, AssignmentName);
            CommonAssignmentKind kind;
            if (!Kinds.TryGetValue(text, out kind))
            {
                throw new FormatException("知らない割当: " + text);
            }

            return kind;
        }

        /// <summary>割当の対象名を読む。</summary>
        private static string ReadAssignmentTarget(
            object value, CommonAssignmentKind assignment)
        {
            string target = Text(value, TargetName);
            if (assignment == CommonAssignmentKind.InternalFlow)
            {
                if (!Flows.ContainsKey(target))
                {
                    throw new FormatException("内部フローへの割当の対象名でない: " + target);
                }
            }
            else if (assignment == CommonAssignmentKind.Tool && !ToolName.IsMatch(target))
            {
                throw new FormatException(
                    TargetName
                        + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + target);
            }
            else if (assignment == CommonAssignmentKind.CommonArg && !ArgumentName.IsMatch(target))
            {
                throw new FormatException(
                    TargetName + " は小文字で始まり、英数字だけからなる語でなければならない: "
                        + target);
            }

            return target;
        }

        private static void RequireAscending(string previous, string current)
        {
            if (previous == null)
            {
                return;
            }

            int order = string.CompareOrdinal(previous, current);
            if (order == 0)
            {
                throw new FormatException("同じ行キーが二度現れる: " + current);
            }

            if (order > 0)
            {
                throw new FormatException("序数の昇順で並んでいない: " + current);
            }
        }

        private static object[] Array(object value)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(AssignmentsName + " は項目の並びでなければならない。");
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
