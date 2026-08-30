using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>凍結した除外の組をJSONから読み取る。</summary>
    public static class ExcludedBaselineJsonReader
    {
        private const string CapabilitiesName = "capabilities";

        private const string CapabilityIdName = "capabilityId";

        private const string SignaturesName = "signatures";

        /// <summary>
        /// 能力IDの昇順、その中は行キーの昇順で返す。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static IList<ExcludedBaselineEntry> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object[] capabilities = Members(Parse(json), CapabilitiesName)[CapabilitiesName] as object[];
            if (capabilities == null)
            {
                throw new FormatException(CapabilitiesName + " は能力の並びでなければならない。");
            }

            HashSet<string> capabilityIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            List<ExcludedBaselineEntry> entries = new List<ExcludedBaselineEntry>();

            foreach (object capability in capabilities)
            {
                Dictionary<string, object> members =
                    Members(capability, CapabilityIdName, SignaturesName);
                string capabilityId = Text(members[CapabilityIdName], CapabilityIdName);
                if (!capabilityIds.Add(capabilityId))
                {
                    throw new FormatException("同じ能力IDが二度現れる: " + capabilityId);
                }

                object[] signatures = members[SignaturesName] as object[];
                if (signatures == null)
                {
                    throw new FormatException(SignaturesName + " は行キーの並びでなければならない。");
                }

                List<string> read = new List<string>();
                foreach (object signature in signatures)
                {
                    string key = Text(signature, SignaturesName);
                    if (!keys.Add(key))
                    {
                        throw new FormatException("同じ行キーが二度現れる: " + key);
                    }

                    read.Add(key);
                }

                entries.Add(new ExcludedBaselineEntry(
                    capabilityId,
                    new ReadOnlyCollection<string>(read.OrderBy(k => k, StringComparer.Ordinal).ToList())));
            }

            return new ReadOnlyCollection<ExcludedBaselineEntry>(
                entries.OrderBy(e => e.CapabilityId, StringComparer.Ordinal).ToList());
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
