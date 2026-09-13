using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// SDKに由来する項目の表現の綴りを、行キーのシグネチャと型ごとの表現の表から導く。正本は綴りを
    /// 書かないので、組み立てる側はここから引く。
    /// </summary>
    public static class SdkShapeEvidence
    {
        private const string ByteTypeName = "System.Byte";

        private const string ListTypeName = "System.Collections.Generic.IList";

        private const string NullableTypeName = "System.Nullable";

        private const string Base64Shape = "base64";

        /// <summary>
        /// 項目から綴りへ引く表。SDKに由来する項目をすべて持ち、ホストが決める項目は持たない。
        /// 項目へ写す値は <paramref name="valuesByType"/> から引き、行と呼び分けの結び付けは
        /// <paramref name="spellings"/> の綴りで決めるので、後者には型から綴りへの表を渡す。
        /// 綴りを導けない項目と、埋め込み先に持ち込む項目が無い行があれば
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<SchemaItem, string> Resolve(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> valuesByType,
            IDictionary<string, string> spellings)
        {
            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (valuesByType == null)
            {
                throw new ArgumentNullException(nameof(valuesByType));
            }

            if (spellings == null)
            {
                throw new ArgumentNullException(nameof(spellings));
            }

            Dictionary<SchemaItem, string> shapes = new Dictionary<SchemaItem, string>();
            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            IDictionary<string, SchemaPayload> byBranch = schemas.Tools
                .Where(t => t.Payloads != null)
                .SelectMany(t => t.Payloads)
                .ToDictionary(p => p.Type, p => p, StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                string dispatched;
                if (toolNames.TryGetValue(row.SignatureKey, out dispatched))
                {
                    ToolSchema called;
                    if (!byTool.TryGetValue(dispatched, out called))
                    {
                        throw new InvalidOperationException(
                            "スキーマ正本に無いツールを行が持っている: " + dispatched);
                    }

                    Dispatched(
                        shapes,
                        called,
                        Branch(
                            called,
                            dispatched,
                            map,
                            signatures,
                            toolNames,
                            spellings,
                            row.SignatureKey),
                        signature,
                        valuesByType,
                        HandleIssuanceEvidence.Issues(row, signature));
                    continue;
                }

                foreach (string embedded in row.EmbeddedIn ?? new string[0])
                {
                    ToolSchema schema;
                    if (byTool.TryGetValue(embedded, out schema))
                    {
                        Embedded(shapes, schema, signature, valuesByType);

                        continue;
                    }

                    SchemaPayload branch;
                    if (byBranch.TryGetValue(embedded, out branch))
                    {
                        Carried(shapes, embedded, branch, signature, valuesByType);
                    }
                }
            }

            return shapes;
        }

        /// <summary>その行が持ち込む項目の名前。メンバー名を動作の語と同じ切れ目で区切って綴る。</summary>
        public static string MemberNameOf(string memberName)
        {
            string[] words = ToolNameRule.ActionWord(memberName).Split('_');
            StringBuilder built = new StringBuilder(words[0]);
            foreach (string word in words.Skip(1))
            {
                built.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1));
            }

            return built.ToString();
        }

        /// <summary>
        /// その行が呼ばれる呼び分け。分岐を選ぶ項目を持たないツールでは null で、そのときは
        /// すべての呼び分けが行の引数を受け取る。
        /// </summary>
        private static SchemaBranch Branch(
            ToolSchema schema,
            string tool,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> spellings,
            string rowKey)
        {
            if (!schema.Branches.Any(b => b.SelectorName != null))
            {
                return null;
            }

            SchemaBranch branch;
            BranchRowRule.Resolve(
                schema,
                map.Rows
                    .Where(r => Called(toolNames, r.SignatureKey, tool))
                    .Select(r => signatures[r.SignatureKey])
                    .ToList(),
                spellings)
                .TryGetValue(rowKey, out branch);

            return branch;
        }

        private static bool Called(
            IDictionary<string, string> toolNames, string rowKey, string tool)
        {
            string named;

            return toolNames.TryGetValue(rowKey, out named)
                && string.Equals(named, tool, StringComparison.Ordinal);
        }

        /// <summary>独立したツールを持つ行。引数が入力へ、戻り値が応答へ当たる。</summary>
        private static void Dispatched(
            IDictionary<SchemaItem, string> shapes,
            ToolSchema schema,
            SchemaBranch called,
            SignatureRecord signature,
            IDictionary<string, string> valuesByType,
            bool issues)
        {
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                foreach (SchemaItem item in (called == null ? schema.Branches : new[] { called })
                    .SelectMany(b => b.Inputs.Where(i => !i.Injected).SelectMany(i => i.WithNested))
                    .Concat(schema.Output == null ? new SchemaItem[0] : schema.Output.WithNested)
                    .Where(i => string.Equals(i.Name, parameter.Name, StringComparison.Ordinal)))
                {
                    Assign(shapes, schema.Tool, item, parameter.TypeName, valuesByType);
                }
            }

            string valueType = issues
                ? ValueTypeName.Contained(signature.ValueType)
                : signature.ValueType;
            SchemaItem output = schema.Output;
            while (output.Origin != null && output.Element != null && output.Element.Origin != null)
            {
                output = output.Element;
            }

            if (output.Origin == null)
            {
                Assign(shapes, schema.Tool, output, valueType, valuesByType);

                return;
            }

            if (output.Element != null && output.Element.Origin == null)
            {
                Assign(shapes, schema.Tool, output.Element, valueType, valuesByType);
            }
        }

        /// <summary>
        /// イベントの分岐へ項目を持ち込む行。分岐の値の組に同じ名前の項目が在り、その綴りは
        /// 持ち込む行の値の型から決まる。
        /// </summary>
        private static void Carried(
            IDictionary<SchemaItem, string> shapes,
            string branch,
            SchemaPayload payload,
            SignatureRecord signature,
            IDictionary<string, string> valuesByType)
        {
            string member = MemberNameOf(signature.MemberName);
            SchemaItem[] items = payload.Members
                .SelectMany(m => m.WithNested)
                .Where(i => string.Equals(i.Name, member, StringComparison.Ordinal))
                .ToArray();
            if (items.Length == 0)
            {
                throw new InvalidOperationException(
                    "イベントの分岐に持ち込む項目が無い: " + branch + "." + member);
            }

            foreach (SchemaItem item in items)
            {
                Assign(shapes, branch, item, signature.ValueType, valuesByType);
            }
        }

        /// <summary>
        /// 独立したツールを持たない行。取得の側では応答の項目へ、更新の側では入力の項目へ当たる。
        /// </summary>
        private static void Embedded(
            IDictionary<SchemaItem, string> shapes,
            ToolSchema schema,
            SignatureRecord signature,
            IDictionary<string, string> valuesByType)
        {
            string member = MemberNameOf(signature.MemberName);
            IEnumerable<SchemaItem> items = schema.Output.WithNested
                .Concat(schema.Branches.SelectMany(b => b.Inputs.SelectMany(i => i.WithNested)))
                .Where(i => string.Equals(i.Name, member, StringComparison.Ordinal))
                .ToList();
            if (!items.Any())
            {
                throw new InvalidOperationException(
                    "埋め込み先に持ち込む項目が無い: " + schema.Tool + "." + member);
            }

            foreach (SchemaItem item in items)
            {
                Assign(shapes, schema.Tool, item, signature.ValueType, valuesByType);
            }
        }

        /// <summary>
        /// 項目とその内側へ綴りを配る。並びを表す項目は要素の側が綴りを持つので、要素の型まで
        /// 剥がして配る。組を表す項目は、その組の項目を持ち込む行が別に在るので配らない。
        /// </summary>
        private static void Assign(
            IDictionary<SchemaItem, string> shapes,
            string tool,
            SchemaItem item,
            string typeName,
            IDictionary<string, string> valuesByType)
        {
            if (item.Members != null)
            {
                return;
            }

            if (item.Element != null)
            {
                string element = ElementTypeOf(typeName);
                if (element == null)
                {
                    throw new InvalidOperationException(
                        "並びでない型を要素の形で表している: " + tool + "." + Named(item)
                            + "(" + typeName + ")");
                }

                Assign(shapes, tool, item.Element, element, valuesByType);

                return;
            }

            string shape = ShapeOf(typeName, valuesByType);
            if (shape == null)
            {
                throw new InvalidOperationException(
                    "値として写せない型の項目がある: " + tool + "." + Named(item)
                        + "(" + typeName + ")");
            }

            shapes[item] = shape;
        }

        /// <summary>その型に対して表が持つ値。包みと配列の印を外して引き、無ければ null。</summary>
        public static string ShapeOf(string typeName, IDictionary<string, string> byType)
        {
            string inner = ArgumentOf(typeName, NullableTypeName);
            if (inner != null)
            {
                return ShapeOf(inner, byType);
            }

            string element = ElementTypeOf(typeName);
            if (element != null)
            {
                return string.Equals(element, ByteTypeName, StringComparison.Ordinal)
                    ? Base64Shape
                    : null;
            }

            string shape;

            return byType.TryGetValue(typeName, out shape) ? shape : null;
        }

        /// <summary>一列に並ぶ配列とリストの要素の型。並びでない型では null。</summary>
        private static string ElementTypeOf(string typeName)
        {
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - 2);
            }

            return ArgumentOf(typeName, ListTypeName);
        }

        /// <summary>その総称型で型付けされているなら、閉じた実引数の型。違えば null。</summary>
        private static string ArgumentOf(string typeName, string definition)
        {
            if (!typeName.StartsWith(definition + "<", StringComparison.Ordinal)
                || !typeName.EndsWith(">", StringComparison.Ordinal))
            {
                return null;
            }

            string[] arguments = TypeDefinitionName.Arguments(typeName).ToArray();

            return arguments.Length == 1 ? arguments[0] : null;
        }

        private static string Named(SchemaItem item)
        {
            return item.Name ?? "名前無し";
        }
    }
}
