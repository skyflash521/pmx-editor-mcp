using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツールを持つ行のシグネチャと、そのツールの入出力の形が対応することを確かめる。引数と受け手と
    /// 戻り値は呼び出しの成立に要るので、スキーマの側に行き先が無ければその行は呼べない。
    /// </summary>
    public static class SchemaCorrespondenceGate
    {
        /// <summary>値を返さないことを表す型の名前。</summary>
        private const string VoidTypeName = "System.Void";

        /// <summary>値を返さないことを表す綴り。</summary>
        private const string NullSpelling = "null_value";

        /// <summary>[対象の集合]が定める指し方。受け手を集合で受け取る入力の名前。</summary>
        private static readonly string[] TargetSelectors = { "all", "handles", "indices", "range" };

        /// <summary>ハンドルで操作する型の受け手の入力の名前。</summary>
        private const string HandleSelector = "handles";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            ToolSchemaTable schemas,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            IDictionary<string, TypeRole> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t.Role, StringComparer.Ordinal);

            foreach (ToolMapRow row in map.Rows.Where(r => r.Tool != null)
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    throw new InvalidOperationException(
                        "行キーのシグネチャが公開APIの列挙に無い: " + row.SignatureKey);
                }

                ToolSchema schema;
                if (!byTool.TryGetValue(row.Tool, out schema))
                {
                    throw new InvalidOperationException(
                        "行が割り当てたツールの入出力の形が無い: " + row.Tool);
                }

                RequireArguments(signature, schema);
                RequireReceiver(signature, schema, byType);
                RequireOutput(signature, schema);
            }
        }

        /// <summary>
        /// 入力に現れる引数がいずれかの呼び分けの入力に、出力に現れる引数が応答に、同じ名前で
        /// 在ることを求める。入力の項目は組と配列で入れ子になるので内側まで見る。
        /// </summary>
        private static void RequireArguments(SignatureRecord signature, ToolSchema schema)
        {
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                if (parameter.Direction != ParameterDirection.Out
                    && !schema.Branches.Any(b => HasInput(b, parameter.Name)))
                {
                    throw new InvalidOperationException(
                        "引数に対応する入力が無い: " + schema.Tool + "(" + parameter.Name + ")");
                }

                if (parameter.Direction != ParameterDirection.In
                    && !Named(schema.Output.WithNested, parameter.Name))
                {
                    throw new InvalidOperationException(
                        "引数に対応する応答の項目が無い: " + schema.Tool
                            + "(" + parameter.Name + ")");
                }
            }
        }

        /// <summary>受け手の入力が、宣言型の役割から決まる形であることを求める。</summary>
        private static void RequireReceiver(
            SignatureRecord signature, ToolSchema schema, IDictionary<string, TypeRole> byType)
        {
            TypeRole role;
            if (signature.IsStatic
                || signature.MemberKind == MemberKind.Constructor
                || !byType.TryGetValue(
                    TypeDefinitionName.OfElement(signature.DeclaringType), out role))
            {
                return;
            }

            if (role == TypeRole.OperationTarget
                && !schema.Branches.All(b => TargetSelectors.Any(n => HasDirectInput(b, n))))
            {
                throw new InvalidOperationException(
                    "操作対象型の受け手を指す入力が無い呼び分けがある: " + schema.Tool);
            }

            if (role == TypeRole.HandleTarget
                && !schema.Branches.All(b => HasDirectInput(b, HandleSelector)))
            {
                throw new InvalidOperationException(
                    "ハンドル操作型の受け手を指す入力が無い呼び分けがある: " + schema.Tool);
            }

            if (role == TypeRole.Connector
                && schema.Branches.Any(b => TargetSelectors.Any(n => HasDirectInput(b, n))))
            {
                throw new InvalidOperationException(
                    "コネクタ型なのに受け手を指す入力がある: " + schema.Tool);
            }
        }

        /// <summary>値を返すシグネチャのツールが、値を返す形を持つことを求める。</summary>
        private static void RequireOutput(SignatureRecord signature, ToolSchema schema)
        {
            if (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(schema.Output.Shape, NullSpelling, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "値を返すシグネチャなのに応答が値を持たない: " + schema.Tool);
            }
        }

        /// <summary>入れ子の内側まで含めて、その名前の入力を持つか。</summary>
        private static bool HasInput(SchemaBranch branch, string name)
        {
            return Named(branch.Inputs.SelectMany(i => i.WithNested), name);
        }

        /// <summary>呼び分けの直下に、その名前の入力を持つか。</summary>
        private static bool HasDirectInput(SchemaBranch branch, string name)
        {
            return Named(branch.Inputs, name);
        }

        private static bool Named(IEnumerable<SchemaItem> items, string name)
        {
            return items.Any(i => string.Equals(i.Name, name, StringComparison.Ordinal));
        }
    }
}
