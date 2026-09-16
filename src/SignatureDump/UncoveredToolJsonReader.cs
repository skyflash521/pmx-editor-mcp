using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実機の検査に覆われないツールの正本をJSONから読み取る。</summary>
    public static class UncoveredToolJsonReader
    {
        private const string ToolsName = "tools";

        private const string ToolName = "tool";

        private const string ReasonName = "reason";

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

            Dictionary<string, object> root = Members(Parse(json), ToolsName);
            try
            {
                List<UncoveredToolRecord> tools = new List<UncoveredToolRecord>();
                foreach (object item in Array(root[ToolsName], ToolsName))
                {
                    Dictionary<string, object> members = Members(item, ToolName, ReasonName);
                    tools.Add(new UncoveredToolRecord(
                        Text(members[ToolName], ToolName), Reason(members[ReasonName])));
                }

                return new UncoveredToolTable(tools);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static UncoveredReason Reason(object value)
        {
            UncoveredReason reason;
            if (!Reasons.TryGetValue(Text(value, ReasonName), out reason))
            {
                throw new FormatException(
                    ReasonName + " は " + string.Join("・", Reasons.Keys.ToArray())
                        + " のどれかでなければならない。");
            }

            return reason;
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
    }
}
