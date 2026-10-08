using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                AssignmentsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(SignatureKeyName, JsonForm.Text()),
                        JsonForm.Member(AssignmentName, JsonForm.Text()),
                        JsonForm.Member(TargetName, JsonForm.Text()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    SignatureKeyName,
                    allowEmpty: true)));

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

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<CommonAssignmentRecord> records = new List<CommonAssignmentRecord>();
            foreach (IDictionary<string, object> item in
                ((object[])root[AssignmentsName]).Cast<IDictionary<string, object>>())
            {
                records.Add(ReadRecord(item));
            }

            return new CommonAssignmentTable(records);
        }

        private static CommonAssignmentRecord ReadRecord(IDictionary<string, object> members)
        {
            CommonAssignmentKind assignment = ReadAssignmentKind((string)members[AssignmentName]);
            string target = ReadAssignmentTarget((string)members[TargetName], assignment);

            try
            {
                return new CommonAssignmentRecord(
                    (string)members[SignatureKeyName],
                    assignment,
                    target,
                    (string)members[BasisName]);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <summary>割当の種別を読む。</summary>
        private static CommonAssignmentKind ReadAssignmentKind(string text)
        {
            CommonAssignmentKind kind;
            if (!Kinds.TryGetValue(text, out kind))
            {
                throw new FormatException("知らない割当: " + text);
            }

            return kind;
        }

        /// <summary>割当の対象名を読む。</summary>
        private static string ReadAssignmentTarget(
            string target, CommonAssignmentKind assignment)
        {
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
    }
}
