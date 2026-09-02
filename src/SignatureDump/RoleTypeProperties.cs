using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型役割表が日本語名を持つプロパティの母集合を、役割対象の型からリフレクションで列挙する。
    /// </summary>
    public static class RoleTypeProperties
    {
        /// <summary>
        /// 役割対象の型が持つ読み取り可能な公開プロパティを、宣言型とメンバー名の組で重複を除いて
        /// 返す。並びは宣言型・メンバー名の序数の昇順。宣言型の表記は
        /// <see cref="TypeNameFormatter"/> に従うので、同じ総称型でも開いた定義と閉じた型は別の
        /// 項目になる(プロパティの型が違い、応答の形も違うため)。同じ宣言型の同じ名前で
        /// プロパティの型だけが違う項目に当たったら <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IList<PropertyRecord> Enumerate(ISet<string> roleTypes, IEnumerable<Type> candidates)
        {
            if (roleTypes == null)
            {
                throw new ArgumentNullException(nameof(roleTypes));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            Dictionary<string, PropertyRecord> records =
                new Dictionary<string, PropertyRecord>(StringComparer.Ordinal);
            foreach (Type type in candidates)
            {
                if (type == null)
                {
                    throw new ArgumentException("型に空を混ぜられない。", nameof(candidates));
                }

                if (!roleTypes.Contains(TypeDefinitionName.Of(TypeNameFormatter.Format(type))))
                {
                    continue;
                }

                foreach (PropertyInfo property in Readable(type))
                {
                    Add(records, property);
                }
            }

            return new ReadOnlyCollection<PropertyRecord>(records.Values
                .OrderBy(r => r.DeclaringType, StringComparer.Ordinal)
                .ThenBy(r => r.MemberName, StringComparer.Ordinal)
                .ToList());
        }

        private static void Add(IDictionary<string, PropertyRecord> records, PropertyInfo property)
        {
            PropertyRecord record = new PropertyRecord(
                TypeNameFormatter.Format(property.DeclaringType),
                property.Name,
                TypeNameFormatter.Format(property.PropertyType));
            string key = record.DeclaringType + "|" + record.MemberName;

            PropertyRecord kept;
            if (!records.TryGetValue(key, out kept))
            {
                records.Add(key, record);
                return;
            }

            if (!string.Equals(kept.PropertyType, record.PropertyType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "同じ名前で型の違うプロパティが在る: " + key);
            }
        }

        // インターフェイスの GetProperties は継いだインターフェイスの分を返さないので、実装している
        // インターフェイスを辿って足す。
        private static IEnumerable<PropertyInfo> Readable(Type type)
        {
            List<PropertyInfo> properties = Declared(type).ToList();
            if (type.IsInterface)
            {
                foreach (Type inherited in type.GetInterfaces())
                {
                    properties.AddRange(Declared(inherited));
                }
            }

            return properties;
        }

        private static IEnumerable<PropertyInfo> Declared(Type type)
        {
            return type
                .GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.FlattenHierarchy)
                .Where(p => p.GetGetMethod(false) != null);
        }
    }
}
