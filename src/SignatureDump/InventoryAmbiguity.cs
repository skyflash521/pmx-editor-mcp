using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 列挙を型名で引けるかどうかを確かめる。総称型引数の表記は宣言時の名前そのままで、名前空間を
    /// 持たない型と同じ形になりうる。両方が同じ名前で在ると、どの出現がどちらかを表記からは
    /// 決められないので、名前で引く側が黙って取り違えないように止める。
    /// </summary>
    public static class InventoryAmbiguity
    {
        /// <summary>
        /// 同じ表記へ写る型定義が2つ以上在れば <see cref="InvalidOperationException"/>。名前で型を引く側は
        /// 1つの表記が1つの型を指すことに頼るので、別のアセンブリの同名型のように複数の実体が
        /// 同じ表記になるなら、型の同一性が残っているここで止める。閉じた総称型は引数の数だけを
        /// 残した表記へ正規化されるので、その形でも重ならないことを確かめる。
        /// </summary>
        public static void RequireDistinctNames(IEnumerable<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            foreach (IGrouping<string, Type> group in types
                .Distinct()
                .GroupBy(t => NormalizedName(TypeNameFormatter.Format(t)), StringComparer.Ordinal))
            {
                if (group.Select(GenericDefinition).Distinct().Count() > 1)
                {
                    throw new InvalidOperationException("同じ表記になる型が複数ある: " + group.Key);
                }
            }
        }

        /// <summary>
        /// 型引数と同じ名前の型が在れば <see cref="InvalidOperationException"/>。
        /// </summary>
        public static void Require(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            RequireNoSharedName(
                inventory.Types.Concat(inventory.ReferencedTypes).Select(t => t.Name),
                inventory.Types
                    .Where(t => t.IsGenericTypeDefinition)
                    .SelectMany(t => TypeArguments(t.Name))
                    .Concat(inventory.Signatures.SelectMany(s => s.TypeParameters)));
        }

        /// <summary>
        /// 型引数と同じ名前の型が在れば <see cref="InvalidOperationException"/>。列挙が書き出さない型も
        /// 基底型として名前で引かれるので、その名前も渡す。
        /// </summary>
        public static void RequireNoSharedName(
            IEnumerable<string> typeNames, IEnumerable<string> parameterNames)
        {
            if (typeNames == null)
            {
                throw new ArgumentNullException(nameof(typeNames));
            }

            if (parameterNames == null)
            {
                throw new ArgumentNullException(nameof(parameterNames));
            }

            HashSet<string> names = new HashSet<string>(typeNames, StringComparer.Ordinal);
            string found = parameterNames.FirstOrDefault(names.Contains);
            if (found != null)
            {
                throw new InvalidOperationException("型引数と同じ名前の型が在る: " + found);
            }
        }

        /// <summary>総称型定義が宣言する型引数の名前。</summary>
        public static IEnumerable<string> TypeParameterNames(string typeName)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            return TypeArguments(typeName);
        }

        private static Type GenericDefinition(Type type)
        {
            return type.IsGenericType && !type.IsGenericTypeDefinition
                ? type.GetGenericTypeDefinition()
                : type;
        }

        /// <summary>
        /// 総称型引数を引数の数へ置き換えた表記。引数の数が違う同名の型は別の名前のままになる。
        /// </summary>
        private static string NormalizedName(string typeName)
        {
            StringBuilder builder = new StringBuilder();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (c == '<')
                {
                    depth++;
                    if (depth == 1)
                    {
                        start = i + 1;
                    }
                }
                else if (c == '>')
                {
                    depth--;
                    if (depth == 0)
                    {
                        builder.Append('<')
                            .Append(Split(typeName.Substring(start, i - start)).Count()
                                .ToString(CultureInfo.InvariantCulture))
                            .Append('>');
                    }
                }
                else if (depth == 0)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>総称型の各段の引数。段ごとに引数を持つ入れ子の型では全段ぶんを返す。</summary>
        private static IEnumerable<string> TypeArguments(string typeName)
        {
            List<string> arguments = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (c == '<')
                {
                    depth++;
                    if (depth == 1)
                    {
                        start = i + 1;
                    }
                }
                else if (c == '>')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arguments.AddRange(Split(typeName.Substring(start, i - start)));
                    }
                }
            }

            return arguments;
        }

        private static IEnumerable<string> Split(string inner)
        {
            List<string> parts = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
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
                    parts.Add(inner.Substring(start, i - start));
                    start = i + 1;
                }
            }

            parts.Add(inner.Substring(start));

            return parts;
        }
    }
}
