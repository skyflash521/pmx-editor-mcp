using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>スキーマ正本を、能力対応表と仕様書の綴りの表と照合する。</summary>
    public static class ToolSchemaGate
    {
        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(ToolSchemaTable schemas, ToolMap map, ISet<string> spellings)
        {
            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (spellings == null)
            {
                throw new ArgumentNullException(nameof(spellings));
            }

            RequireSameTools(schemas, map);
            RequireOnePollingTool(schemas);
            RequireSamePayloads(schemas, map);
            foreach (ToolSchema schema in schemas.Tools)
            {
                RequireShapes(schema, spellings);
            }
        }

        /// <summary>
        /// 表が持つツールが、能力対応表がツールを持つ行のツールと一致することを求める。片方にしか
        /// 無いツールは、入出力の形か割り当て先のどちらかを失う。
        /// </summary>
        private static void RequireSameTools(ToolSchemaTable schemas, ToolMap map)
        {
            HashSet<string> assigned = new HashSet<string>(
                map.Rows.Where(r => r.Tool != null).Select(r => r.Tool), StringComparer.Ordinal);
            HashSet<string> described = new HashSet<string>(
                schemas.Tools.Select(t => t.Tool), StringComparer.Ordinal);

            string missing = assigned.Except(described, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException(
                    "能力対応表が割り当てたツールの入出力の形が無い: " + missing);
            }

            string extra = described.Except(assigned, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "能力対応表がどの行にも割り当てていないツールの形がある: " + extra);
            }
        }

        /// <summary>イベントの分岐を持つツールが、表に1つだけであることを求める。</summary>
        private static void RequireOnePollingTool(ToolSchemaTable schemas)
        {
            string second = schemas.Tools.Where(t => t.Payloads != null)
                .Select(t => t.Tool).OrderBy(t => t, StringComparer.Ordinal)
                .Skip(1).FirstOrDefault();
            if (second != null)
            {
                throw new InvalidOperationException(
                    "イベントの分岐を持つツールが2つ以上ある: " + second);
            }
        }

        /// <summary>
        /// イベントの取り出しが持つ分岐が、能力対応表のイベント行と一致することを求める。
        /// </summary>
        private static void RequireSamePayloads(ToolSchemaTable schemas, ToolMap map)
        {
            HashSet<string> branches = new HashSet<string>(
                map.Rows.Where(r => r.EventType != null).Select(r => r.EventType),
                StringComparer.Ordinal);
            HashSet<string> described = new HashSet<string>(
                schemas.Tools.Where(t => t.Payloads != null)
                    .SelectMany(t => t.Payloads).Select(p => p.Type),
                StringComparer.Ordinal);

            string missing = branches.Except(described, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("イベント行の分岐の形が無い: " + missing);
            }

            string extra = described.Except(branches, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "能力対応表にイベント行の無い分岐がある: " + extra);
            }
        }

        /// <summary>綴りの閉じた集合は仕様書が持つので、そこに実在することまで求める。</summary>
        private static void RequireShapes(ToolSchema schema, ISet<string> spellings)
        {
            foreach (SchemaItem item in schema.AllItems.Where(i => i.Shape != null))
            {
                if (!spellings.Contains(item.Shape))
                {
                    throw new InvalidOperationException(
                        "表現の綴りが仕様書に無い: " + schema.Tool + "(" + item.Shape + ")");
                }
            }
        }
    }
}
