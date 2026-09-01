using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型をJSONの値表現へ写す。ここで表現が決まらない型は型役割表が受け持つので、この判定は
    /// 表に載る型を過不足なく拾い、それ以外へは何も返さない。
    /// </summary>
    public sealed class ValueRepresentationRule
    {
        private const string NullableTypeName = "System.Nullable";

        private const string ListTypeName = "System.Collections.Generic.IList";

        private const string ByteTypeName = "System.Byte";

        private static readonly Dictionary<string, ValueRepresentationKind> Fixed =
            new Dictionary<string, ValueRepresentationKind>(StringComparer.Ordinal)
            {
                { "System.Boolean", ValueRepresentationKind.Boolean },
                { ByteTypeName, ValueRepresentationKind.Number },
                { "System.Int32", ValueRepresentationKind.Number },
                { "System.Single", ValueRepresentationKind.Number },
                { "System.Double", ValueRepresentationKind.Number },
                { "System.String", ValueRepresentationKind.Text },
                { "System.Version", ValueRepresentationKind.Text },
                { "System.Object", ValueRepresentationKind.Json },
                { "System.Void", ValueRepresentationKind.Null },
                { "System.Drawing.Color", ValueRepresentationKind.Color },
                { "System.Drawing.Size", ValueRepresentationKind.Size },
                { "System.Drawing.Point", ValueRepresentationKind.Point },
                { "System.Drawing.Rectangle", ValueRepresentationKind.Rectangle },
                { "System.Drawing.Font", ValueRepresentationKind.Font },
                { "System.Drawing.Brush", ValueRepresentationKind.Brush },
                { "System.Drawing.Bitmap", ValueRepresentationKind.Image },
                { "PEPlugin.SDX.V2", ValueRepresentationKind.NumberArray },
                { "PEPlugin.SDX.V3", ValueRepresentationKind.NumberArray },
                { "PEPlugin.SDX.V4", ValueRepresentationKind.NumberArray },
                { "PEPlugin.SDX.Q", ValueRepresentationKind.NumberArray },
                { "PEPlugin.SDX.M", ValueRepresentationKind.NumberArray },
                { "PEPlugin.Pmd.IPEVector2", ValueRepresentationKind.NumberArray },
                { "PEPlugin.Pmd.IPEVector3", ValueRepresentationKind.NumberArray },
                { "PEPlugin.Pmd.IPEVector4", ValueRepresentationKind.NumberArray },
                { "PEPlugin.Pmd.IPEQuaternion", ValueRepresentationKind.NumberArray },
                { "PEPlugin.Pmd.IPEMatrix", ValueRepresentationKind.NumberArray },
                { "SlimDX.Vector2", ValueRepresentationKind.NumberArray },
                { "SlimDX.Vector3", ValueRepresentationKind.NumberArray },
                { "SlimDX.Vector4", ValueRepresentationKind.NumberArray },
                { "SlimDX.Quaternion", ValueRepresentationKind.NumberArray },
                { "SlimDX.Matrix", ValueRepresentationKind.NumberArray },
                { "SlimDX.Color3", ValueRepresentationKind.Color },
                { "SlimDX.Color4", ValueRepresentationKind.Color },
            };

        private readonly HashSet<string> enums;

        private ValueRepresentationRule(HashSet<string> enums)
        {
            this.enums = enums;
        }

        /// <summary>列挙型かどうかは名前では決まらないので、列挙の分類から引く。</summary>
        public static ValueRepresentationRule Create(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return new ValueRepresentationRule(new HashSet<string>(
                inventory.Types.Concat(inventory.ReferencedTypes)
                    .Where(t => t.Kind == TypeKind.Enum)
                    .Select(t => t.Name),
                StringComparer.Ordinal));
        }

        /// <summary>表現が決まらなければ false を返し、<paramref name="representation"/> に null を置く。</summary>
        public bool TryClassify(string typeName, out ValueRepresentation representation)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            representation = Classify(WithoutByReferenceMark(typeName));

            return representation != null;
        }

        private ValueRepresentation Classify(string typeName)
        {
            if (typeName.EndsWith("]", StringComparison.Ordinal))
            {
                return ForArray(typeName);
            }

            string argument;
            if (TryGenericArgument(typeName, NullableTypeName, out argument))
            {
                ValueRepresentation element = Classify(argument);
                return element == null ? null : element.AsNullable();
            }

            if (TryGenericArgument(typeName, ListTypeName, out argument))
            {
                return ForSequence(argument, true);
            }

            if (enums.Contains(typeName))
            {
                return ValueRepresentation.Of(ValueRepresentationKind.EnumName);
            }

            ValueRepresentationKind kind;

            return Fixed.TryGetValue(typeName, out kind) ? ValueRepresentation.Of(kind) : null;
        }

        private ValueRepresentation ForArray(string typeName)
        {
            int open = typeName.LastIndexOf('[');
            if (open < 0)
            {
                return null;
            }

            int separators = typeName.Skip(open + 1).Take(typeName.Length - open - 2).Count(c => c == ',');
            if (separators != typeName.Length - open - 2)
            {
                return null;
            }

            return ForSequence(typeName.Substring(0, open), separators == 0);
        }

        /// <summary>
        /// バイト列をBase64へ詰められるのは一列に並ぶときだけで、多次元の配列では各次元の長さが
        /// 失われるので、要素の表現を包む一般の規則へ倒す。
        /// </summary>
        private ValueRepresentation ForSequence(string elementTypeName, bool isLinear)
        {
            if (isLinear && string.Equals(elementTypeName, ByteTypeName, StringComparison.Ordinal))
            {
                return ValueRepresentation.Of(ValueRepresentationKind.Base64);
            }

            ValueRepresentation element = Classify(elementTypeName);

            return element == null ? null : ValueRepresentation.ArrayOf(element);
        }

        /// <summary>閉じた総称型の引数が1つだけのときにその引数を返す。</summary>
        private static bool TryGenericArgument(string typeName, string definition, out string argument)
        {
            argument = null;
            string prefix = definition + "<";
            if (!typeName.StartsWith(prefix, StringComparison.Ordinal)
                || !typeName.EndsWith(">", StringComparison.Ordinal))
            {
                return false;
            }

            string inner = typeName.Substring(prefix.Length, typeName.Length - prefix.Length - 1);
            int depth = 0;
            foreach (char c in inner)
            {
                if (c == '<' || c == '[')
                {
                    depth++;
                }
                else if (c == '>' || c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return false;
                }
            }

            argument = inner;

            return true;
        }

        private static string WithoutByReferenceMark(string typeName)
        {
            return typeName.EndsWith("&", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 1)
                : typeName;
        }
    }
}
