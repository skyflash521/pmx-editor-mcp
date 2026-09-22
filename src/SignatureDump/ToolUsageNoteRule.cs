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

        private const string SelectedName = "selected";

        private const string TotalName = "total";

        private const string ItemsName = "items";

        private const string CountName = "count";

        private const string ToTheEnd =
            "端まで読むなら " + AllName + " に真を渡し、nextOffset が返らなくなるまで"
                + " offset へ渡し直す。";

        private const string WithParent = "親も " + ParentAllName + " に真を渡す。";

        private const string NameContainsName = "nameContains";

        private const string NarrowsByName =
            NameContainsName + " を渡すと、name がその文字列を含む要素だけが残る。"
                + "大文字小文字は区別し、" + TotalName + " は絞り込む前の件数のままになる。";

        private const string RangeIsNotClamped =
            RangeName + " は端で詰めず、リストの件数を超えると断る。";

        private const string TakesTheScreenSelection =
            SelectedName + " に真を渡すと、画面がいま選んでいるものを対象にする。";

        private const string MakesNew =
            "呼ぶたびに新しく作る。返るのは作ったもののハンドルの番号で、要らなくなったら"
                + " session_release_handle へ渡す。";

        /// <summary>
        /// そのツールの呼び方を、スキーマ正本から引いて組み立てる。対象の指し方は書かない。
        /// 正本に無いツールでは null。
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

            return schema == null ? null : Compose(schema, false);
        }

        /// <summary>そのツールの呼び方。対象の指し方も書く。書くことが無ければ null。</summary>
        public static string Compose(ToolSchema schema)
        {
            return Compose(schema, true);
        }

        private static string Compose(ToolSchema schema, bool pointing)
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

            if (Takes(schema, NameContainsName))
            {
                built.Append(NarrowsByName);
            }

            if (Takes(schema, RangeName))
            {
                built.Append(RangeIsNotClamped);
            }

            if (pointing && Takes(schema, SelectedName))
            {
                built.Append(TakesTheScreenSelection);
            }

            if (Issues(schema))
            {
                built.Append(MakesNew);
            }

            return built.Length == 0 ? null : built.ToString();
        }

        private static bool Takes(ToolSchema schema, string name)
        {
            return schema.Branches.Any(
                b => b.Inputs.Any(
                    i => !i.Injected && string.Equals(i.Name, name, StringComparison.Ordinal)));
        }

        private static bool Issues(ToolSchema schema)
        {
            SchemaItem output = schema.Output;

            return Takes(schema, CountName)
                && output != null
                && output.Element != null
                && (output.Members == null || output.Members.Count == 0);
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
