using System;
using System.Collections.Generic;
using System.Globalization;

namespace PmxEditorMcp
{
    public static class VmdPoseStates
    {
        public const string CreateKey =
            "PEPlugin.IPEBuilder.CreateVmdBonePoseState(PEPlugin.Vmd.IPEVmd,System.Int32[])";

        private const string BoneIndicesName = "boneIndices";

        private const int BoneIndicesAt = 1;

        public static bool TryCall(
            string rowKey, object[] args, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            if (args == null || !string.Equals(rowKey, CreateKey, StringComparison.Ordinal))
            {
                return true;
            }

            int[] indices = args.Length > BoneIndicesAt ? args[BoneIndicesAt] as int[] : null;
            if (indices == null)
            {
                return true;
            }

            HashSet<int> seen = new HashSet<int>();
            for (int at = 0; at < indices.Length; at++)
            {
                if (!seen.Add(indices[at]))
                {
                    message = BoneIndicesName + " の " + at + " 番目が前と重なっている: "
                        + indices[at].ToString(CultureInfo.InvariantCulture)
                        + "。重ならない番号を並べる。";

                    return false;
                }
            }

            return true;
        }
    }
}
