using System;
using System.Collections.Generic;
using PEPlugin.Vme;

namespace PmxEditorMcp
{
    public static class VmeResults
    {
        public const string SetKey = "PEPlugin.Vme.IPEVme.SetVmeResult(PEPlugin.Vme.IPEVmeResult)";

        private const int ResultAt = 0;

        public static bool TryCall(
            string rowKey, object item, object[] args, out string code, out string message)
        {
            code = ToolEnvelope.NotApplicable;
            message = null;
            IPEVme vme = item as IPEVme;
            IPEVmeResult result = args != null && args.Length > ResultAt
                ? args[ResultAt] as IPEVmeResult
                : null;
            if (vme == null || result == null || result.FrameCount <= 0
                || !string.Equals(rowKey, SetKey, StringComparison.Ordinal))
            {
                return true;
            }

            if (result.EnableBone && result.BoneCount > 0 && vme.Bone.Count > 0)
            {
                List<string> bones = new List<string>();
                foreach (IPEVmeBone bone in vme.Bone)
                {
                    bones.Add(((IPEVmeElement)bone).Name);
                }

                message = Refusal("ボーン", bones);
            }

            if (message == null && result.EnableMorph && result.MorphCount > 0 && vme.Morph.Count > 0)
            {
                List<string> morphs = new List<string>();
                foreach (IPEVmeSingleValueElement morph in vme.Morph)
                {
                    morphs.Add(((IPEVmeElement)morph).Name);
                }

                message = Refusal("モーフ", morphs);
            }

            return message == null;
        }

        private static string Refusal(string kind, IList<string> names)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                if (name == null)
                {
                    return "名前を持たない" + kind + "があるVMEには結果を適用できない。"
                        + "名前を付けてから適用する。";
                }

                if (!seen.Add(name))
                {
                    return "同じ名前の" + kind + "を持つVMEには結果を適用できない: " + name
                        + "。名前が重ならないようにしてから適用する。";
                }
            }

            return null;
        }
    }
}
