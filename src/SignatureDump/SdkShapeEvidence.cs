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
        /// 綴りを導けない項目と、埋め込み先に持ち込む項目が無い行があれば
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<SchemaItem, string> Resolve(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> shapesByType)
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

            if (shapesByType == null)
            {
                throw new ArgumentNullException(nameof(shapesByType));
            }

            Dictionary<SchemaItem, string> shapes = new Dictionary<SchemaItem, string>();
            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
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
                    Dispatched(shapes, byTool[dispatched], signature, shapesByType);
                    continue;
                }

                foreach (string embedded in row.EmbeddedIn ?? new string[0])
                {
                    ToolSchema schema;
                    if (byTool.TryGetValue(embedded, out schema))
                    {
                        Embedded(shapes, schema, signature, shapesByType);
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

        /// <summary>独立したツールを持つ行。引数が入力へ、戻り値が応答へ当たる。</summary>
        private static void Dispatched(
            IDictionary<SchemaItem, string> shapes,
            ToolSchema schema,
            SignatureRecord signature,
            IDictionary<string, string> shapesByType)
        {
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                foreach (SchemaItem item in schema.Branches
                    .SelectMany(b => b.Inputs.Where(i => !i.Injected).SelectMany(i => i.WithNested))
                    .Concat(schema.Output == null ? new SchemaItem[0] : schema.Output.WithNested)
                    .Where(i => string.Equals(i.Name, parameter.Name, StringComparison.Ordinal)))
                {
                    Assign(shapes, schema.Tool, item, parameter.TypeName, shapesByType);
                }
            }

            if (schema.Output.Origin == null)
            {
                Assign(shapes, schema.Tool, schema.Output, signature.ValueType, shapesByType);
            }
        }

        /// <summary>
        /// 独立したツールを持たない行。取得の側では応答の項目へ、更新の側では入力の項目へ当たる。
        /// </summary>
        private static void Embedded(
            IDictionary<SchemaItem, string> shapes,
            ToolSchema schema,
            SignatureRecord signature,
            IDictionary<string, string> shapesByType)
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
                Assign(shapes, schema.Tool, item, signature.ValueType, shapesByType);
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
            IDictionary<string, string> shapesByType)
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

                Assign(shapes, tool, item.Element, element, shapesByType);

                return;
            }

            string shape = ShapeOf(typeName, shapesByType);
            if (shape == null)
            {
                throw new InvalidOperationException(
                    "値として写せない型の項目がある: " + tool + "." + Named(item)
                        + "(" + typeName + ")");
            }

            shapes[item] = shape;
        }

        /// <summary>その型を値として写す綴り。写せない型では null。</summary>
        private static string ShapeOf(string typeName, IDictionary<string, string> shapesByType)
        {
            string inner = ArgumentOf(typeName, NullableTypeName);
            if (inner != null)
            {
                return ShapeOf(inner, shapesByType);
            }

            string element = ElementTypeOf(typeName);
            if (element != null)
            {
                return string.Equals(element, ByteTypeName, StringComparison.Ordinal)
                    ? Base64Shape
                    : null;
            }

            string shape;

            return shapesByType.TryGetValue(typeName, out shape) ? shape : null;
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
