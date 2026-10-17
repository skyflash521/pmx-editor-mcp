using System;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ClonedModelNodes
    {
        public const string CloneRow = "PEPlugin.Pmx.IPXPmx.Clone()";

        /// <summary>複製する行でなければ、何もしない。</summary>
        public static void Relink(string rowKey, object source, object clone)
        {
            IPXPmx from = source as IPXPmx;
            IPXPmx made = clone as IPXPmx;
            if (!string.Equals(rowKey, CloneRow, StringComparison.Ordinal)
                || from == null
                || made == null
                || from.Node.Count != made.Node.Count)
            {
                return;
            }

            for (int at = 0; at < from.Node.Count; at++)
            {
                if (ReferenceEquals(from.Node[at], from.RootNode))
                {
                    made.Node[at] = made.RootNode;
                }
                else if (ReferenceEquals(from.Node[at], from.ExpressionNode))
                {
                    made.Node[at] = made.ExpressionNode;
                }
            }
        }
    }
}
