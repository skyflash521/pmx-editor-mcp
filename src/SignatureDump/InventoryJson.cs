using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 列挙結果をJSONへ書き出す。同じ入力からは常に同じバイト列になり、配列の要素は1行ずつに
    /// 分かれるので、行単位の差分で変化を追える。
    /// </summary>
    public static class InventoryJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append(Member("assemblyName", Text(inventory.AssemblyName))).Append(",\n");
            builder.Append(Member("assemblyVersion", Text(inventory.AssemblyVersion))).Append(",\n");
            AppendArray(builder, "types", inventory.Types.Select(WriteType));
            builder.Append(",\n");
            AppendArray(builder, "referencedTypes", inventory.ReferencedTypes.Select(WriteType));
            builder.Append(",\n");
            AppendArray(builder, "signatures", inventory.Signatures.Select(WriteSignature));
            builder.Append("\n}\n");

            return builder.ToString();
        }

        private static void AppendArray(StringBuilder builder, string name, IEnumerable<string> elements)
        {
            string[] items = elements.ToArray();
            if (items.Length == 0)
            {
                builder.Append(Member(name, "[]"));
                return;
            }

            builder.Append(Member(name, "[\n"));
            builder.Append(string.Join(",\n", items));
            builder.Append("\n]");
        }

        private static string WriteType(TypeRecord type)
        {
            return "{"
                + Member("name", Text(type.Name)) + ","
                + Member("kind", Text(Word(type.Kind.ToString()))) + ","
                + Member("isNested", Flag(type.IsNested)) + ","
                + Member("isAbstract", Flag(type.IsAbstract)) + ","
                + Member("isGenericTypeDefinition", Flag(type.IsGenericTypeDefinition)) + ","
                + Member("baseTypes", TextArray(type.BaseTypes)) + ","
                + Member("enumMembers", TextArray(type.EnumMembers)) + ","
                + Member("isCombinable", Flag(type.IsCombinable))
                + "}";
        }

        private static string WriteSignature(SignatureRecord signature)
        {
            return "{"
                + Member("key", Text(signature.Key)) + ","
                + Member("declaringType", Text(signature.DeclaringType)) + ","
                + Member("memberKind", Text(Word(signature.MemberKind.ToString()))) + ","
                + Member("memberName", Text(signature.MemberName)) + ","
                + Member("isStatic", Flag(signature.IsStatic)) + ","
                + Member("genericArity", signature.GenericArity.ToString(CultureInfo.InvariantCulture)) + ","
                + Member("typeParameters", Array(signature.TypeParameters.Select(Text))) + ","
                + Member("parameters", Array(signature.Parameters.Select(WriteParameter))) + ","
                + Member("valueType", Text(signature.ValueType)) + ","
                + Member("canRead", Flag(signature.CanRead)) + ","
                + Member("canWrite", Flag(signature.CanWrite)) + ","
                + Member("operationDirection", Text(Word(signature.OperationDirection.ToString()))) + ","
                + Member("valueTypeIsTypeArgument", Flag(signature.ValueTypeIsTypeArgument))
                + "}";
        }

        private static string WriteParameter(ParameterRecord parameter)
        {
            return "{"
                + Member("name", Text(parameter.Name)) + ","
                + Member("typeName", Text(parameter.TypeName)) + ","
                + Member("direction", Text(Word(parameter.Direction.ToString()))) + ","
                + Member("isOptional", Flag(parameter.IsOptional)) + ","
                + Member("isTypeArgument", Flag(parameter.IsTypeArgument))
                + "}";
        }

        private static string Member(string name, string value)
        {
            return Text(name) + ":" + value;
        }

        private static string Array(IEnumerable<string> elements)
        {
            return "[" + string.Join(",", elements) + "]";
        }

        private static string TextArray(IEnumerable<string> values)
        {
            return Array(values.Select(Text));
        }

        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }

        // 分類を表す値は、読み手が言語の識別子と取り違えないよう小文字で書く。
        private static string Word(string name)
        {
            return name.ToLowerInvariant();
        }

        private static string Text(string value)
        {
            return JsonText.Quote(value);
        }
    }
}
