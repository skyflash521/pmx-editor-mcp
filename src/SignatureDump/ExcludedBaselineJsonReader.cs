using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>凍結した除外の組をJSONから読み取る。</summary>
    public static class ExcludedBaselineJsonReader
    {
        private const string CapabilitiesName = "capabilities";

        private const string CapabilityIdName = "capabilityId";

        private const string SignaturesName = "signatures";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                CapabilitiesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(CapabilityIdName, JsonForm.Text()),
                        JsonForm.Member(
                            SignaturesName, JsonForm.Array(JsonForm.Text(), allowEmpty: true))),
                    allowEmpty: true)));

        /// <summary>
        /// 能力IDの昇順、その中は行キーの昇順で返す。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static IList<ExcludedBaselineEntry> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            HashSet<string> capabilityIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            List<ExcludedBaselineEntry> entries = new List<ExcludedBaselineEntry>();

            foreach (IDictionary<string, object> members in
                ((object[])root[CapabilitiesName]).Cast<IDictionary<string, object>>())
            {
                string capabilityId = (string)members[CapabilityIdName];
                if (!capabilityIds.Add(capabilityId))
                {
                    throw new FormatException("同じ能力IDが二度現れる: " + capabilityId);
                }

                List<string> read = new List<string>();
                foreach (string key in ((object[])members[SignaturesName]).Cast<string>())
                {
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
    }
}
