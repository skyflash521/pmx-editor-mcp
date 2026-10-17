using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    public static class FrozenExclusionJsonReader
    {
        private const string CapabilitiesName = "capabilities";

        private const string CapabilityIdName = "capabilityId";

        private const string StatusName = "status";

        private const string TargetName = "target";

        private const string RemarksName = "remarks";

        private const string SignaturesName = "signatures";

        private const string TypesName = "types";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                CapabilitiesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(CapabilityIdName, JsonForm.Text()),
                        JsonForm.Member(StatusName, JsonForm.Text()),
                        JsonForm.Member(TargetName, JsonForm.Text()),
                        JsonForm.Member(RemarksName, JsonForm.OrNull(JsonForm.Text())),
                        JsonForm.Member(SignaturesName, JsonForm.Array(JsonForm.Text(), allowEmpty: true)),
                        JsonForm.Member(TypesName, JsonForm.Array(JsonForm.Text(), allowEmpty: true))),
                    CapabilityIdName)));

        /// <summary>能力IDの昇順で返す。形が違えば <see cref="FormatException"/>。</summary>
        public static IList<FrozenExclusion> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            List<FrozenExclusion> read = new List<FrozenExclusion>();
            foreach (IDictionary<string, object> capability in
                ((object[])root[CapabilitiesName]).Cast<IDictionary<string, object>>())
            {
                string id = (string)capability[CapabilityIdName];
                CapabilityStatus status =
                    LedgerJsonReader.StatusOf((string)capability[StatusName], id);
                string remarks = (string)capability[RemarksName];
                if ((status == CapabilityStatus.Provided) != (remarks != null))
                {
                    throw new FormatException(
                        id + " の備考は、分類が提供のときだけ書き、ほかでは null にする。");
                }

                read.Add(new FrozenExclusion(
                    id,
                    status,
                    (string)capability[TargetName],
                    remarks,
                    Names(capability, SignaturesName, id, named),
                    Names(capability, TypesName, id, named)));
            }

            return new ReadOnlyCollection<FrozenExclusion>(read);
        }

        private static IList<string> Names(
            IDictionary<string, object> capability, string name, string id, ISet<string> named)
        {
            List<string> names = ((object[])capability[name]).Cast<string>().ToList();
            foreach (string one in names)
            {
                if (!named.Add(one))
                {
                    throw new FormatException(id + " の " + name + " に、既に挙げた名前がある: " + one);
                }
            }

            return names;
        }
    }
}
