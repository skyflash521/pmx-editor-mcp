using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 列挙を型名で引けるかどうかを確かめる。総称型引数の表記は宣言時の名前そのままで、名前空間を
    /// 持たない型と同じ形になりうる。
    /// </summary>
    public static class InventoryAmbiguity
    {
        /// <summary>
        /// 同じ表記へ写る型定義が2つ以上在れば <see cref="InvalidOperationException"/>。
        /// 閉じた総称型は、引数の数だけを残した表記でも重ならないことを確かめる。
        /// </summary>
        public static void RequireDistinctNames(IEnumerable<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            foreach (IGrouping<string, Type> group in types
                .Distinct()
                .GroupBy(t => TypeDefinitionName.Of(TypeNameFormatter.Format(t)), StringComparer.Ordinal))
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
                    .SelectMany(t => TypeDefinitionName.Arguments(t.Name))
                    .Concat(inventory.Signatures.SelectMany(s => s.TypeParameters)));
        }

        /// <summary>
        /// 型引数と同じ名前の型が在れば <see cref="InvalidOperationException"/>。列挙が
        /// 書き出さない型の名前も渡す。
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

            return TypeDefinitionName.Arguments(typeName);
        }

        private static Type GenericDefinition(Type type)
        {
            return type.IsGenericType && !type.IsGenericTypeDefinition
                ? type.GetGenericTypeDefinition()
                : type;
        }
    }
}
