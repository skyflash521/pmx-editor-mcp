using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    public static class ToolUsageNoteRule
    {
        private const string AllName = "all";

        private const string ParentAllName = "parentAll";

        private const string RangeName = "range";

        private const string TotalName = "total";

        private const string ItemsName = "items";

        private const string ToTheEnd =
            "端まで読むなら " + AllName + " に真を渡し、nextOffset が返らなくなるまで"
                + " offset へ渡し直す。";

        private const string WithParent = "親も " + ParentAllName + " に真を渡す。";

        private const string RangeIsNotClamped =
            RangeName + " は端で詰めず、リストの件数を超えると断る。";

        /// <summary>
        /// そのツールの呼び方を、スキーマ正本から引いて組み立てる。正本に無いツールでは null。
        /// </summary>
        public static string Of(string tool, ToolSchemaTable schemas)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            ToolSchema schema = schemas.Tools.FirstOrDefault(
                t => string.Equals(t.Tool, tool, StringComparison.Ordinal));

            return schema == null ? null : Compose(schema);
        }

        /// <summary>そのツールの呼び方。書くことが無ければ null。</summary>
        public static string Compose(ToolSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            StringBuilder built = new StringBuilder();
            if (IsListing(schema) && Takes(schema, AllName))
            {
                built.Append(ToTheEnd);
                if (Takes(schema, ParentAllName))
                {
                    built.Append(WithParent);
                }
            }

            if (Takes(schema, RangeName))
            {
                built.Append(RangeIsNotClamped);
            }

            return built.Length == 0 ? null : built.ToString();
        }

        private static bool Takes(ToolSchema schema, string name)
        {
            return schema.Branches.Any(
                b => b.Inputs.Any(
                    i => !i.Injected && string.Equals(i.Name, name, StringComparison.Ordinal)));
        }

        private static bool IsListing(ToolSchema schema)
        {
            IList<SchemaItem> members = schema.Output == null ? null : schema.Output.Members;

            return members != null
                && members.Any(m => string.Equals(m.Name, TotalName, StringComparison.Ordinal))
                && members.Any(
                    m => string.Equals(m.Name, ItemsName, StringComparison.Ordinal)
                        && m.Element != null);
        }
    }
}
