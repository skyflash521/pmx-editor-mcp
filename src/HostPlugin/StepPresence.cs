using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 親ごとに1つだけ辿る段のうち、親が持たないときも先の実体を返すもの。
    /// </summary>
    public static class StepPresence
    {
        private const string BoneIkKey = "PEPlugin.Pmx.IPXBone.IK()";

        /// <summary>その親が、その段の先の実体を持つか。</summary>
        public static bool Holds(string rowKey, object owner)
        {
            if (rowKey == BoneIkKey)
            {
                IPXBone bone = owner as IPXBone;

                return bone == null || bone.IsIK;
            }

            return true;
        }
    }
}
