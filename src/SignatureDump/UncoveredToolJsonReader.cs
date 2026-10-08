using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実機の検査に覆われないツールの正本をJSONから読み取る。</summary>
    public static class UncoveredToolJsonReader
    {
        private const string ToolsName = "tools";

        private const string ToolName = "tool";

        private const string ReasonName = "reason";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                ToolsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(ReasonName, JsonForm.Text())),
                    allowEmpty: true)));

        private static readonly Dictionary<string, UncoveredReason> Reasons =
            new Dictionary<string, UncoveredReason>(StringComparer.Ordinal)
            {
                { "noCase", UncoveredReason.NoCase },
                { "noEffectCheck", UncoveredReason.NoEffectCheck },
            };

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static UncoveredToolTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            try
            {
                return new UncoveredToolTable(((object[])root[ToolsName])
                    .Cast<IDictionary<string, object>>()
                    .Select(members => new UncoveredToolRecord(
                        (string)members[ToolName], Reason((string)members[ReasonName])))
                    .ToList());
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static UncoveredReason Reason(string text)
        {
            UncoveredReason reason;
            if (!Reasons.TryGetValue(text, out reason))
            {
                throw new FormatException(
                    ReasonName + " は " + string.Join("・", Reasons.Keys.ToArray())
                        + " のどれかでなければならない。");
            }

            return reason;
        }
    }
}
