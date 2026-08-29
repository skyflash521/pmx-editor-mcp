using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// アセンブリの公開APIをリフレクションで列挙する。母集合は <see cref="Type.IsVisible"/> が
    /// 真の型とし、入れ子の公開型を落とさない。外側が公開でない入れ子の型は、入れ子の側が公開でも
    /// 母集合に入らない。
    ///
    /// 行にするのは、各型が自分で宣言する公開メンバーのうち、メソッド・プロパティ・フィールド・
    /// イベント・コンストラクタの5種類だけである。プロパティとイベントの取得・設定・追加・削除の
    /// アクセサーはメソッドの形で現れるが、そのプロパティ・イベントの行が表すので別の行にしない。
    /// 入れ子の型もメンバーの形で現れるが、型として記録するので行にしない。演算子のように、
    /// 言語が特別な名前を与えるメソッドでも、アクセサーでなければ行にする。
    ///
    /// 上の5種類のうち、次の閉じた集合だけは行にしない。
    /// 列挙型では、値の記憶域 <c>value__</c> と列挙子のフィールド。値の集合は
    /// <see cref="TypeRecord.EnumMembers"/> が持つので落ちない。
    /// デリゲート型では、コンストラクタと <c>BeginInvoke</c> と <c>EndInvoke</c>。どのデリゲート
    /// にも同じ形で現れ、その型固有の引数と戻り値は <c>Invoke</c> が持つ。
    /// これ以外はすべて行にする。デリゲートの <c>Invoke</c> も、クラスが明示的に宣言しない
    /// 公開コンストラクタも行にする。
    /// </summary>
    public static class AssemblyEnumerator
    {
        private const string DelegateInvokeName = "Invoke";

        private const BindingFlags DeclaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static InventoryRecord Enumerate(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type[] types = assembly.GetTypes().Where(t => t.IsVisible).ToArray();
            List<TypeRecord> typeRecords = new List<TypeRecord>();
            List<SignatureRecord> signatures = new List<SignatureRecord>();

            foreach (Type type in types)
            {
                TypeKind kind = ClassifyType(type);
                typeRecords.Add(new TypeRecord(
                    TypeNameFormatter.Format(type),
                    kind,
                    type.IsNested,
                    type.IsAbstract,
                    type.IsGenericTypeDefinition,
                    CollectBaseTypes(type, kind),
                    CollectEnumMembers(type, kind)));

                signatures.AddRange(CollectSignatures(type, kind));
            }

            AssemblyName name = assembly.GetName();
            return new InventoryRecord(
                name.Name,
                name.Version.ToString(),
                typeRecords.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
                signatures.OrderBy(s => s.Key, StringComparer.Ordinal).ToList());
        }

        // 列挙型は値型でもあり、デリゲートはクラスでもあるので、狭い分類から先に見る。
        private static TypeKind ClassifyType(Type type)
        {
            if (type.IsEnum)
            {
                return TypeKind.Enum;
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return TypeKind.Delegate;
            }

            if (type.IsInterface)
            {
                return TypeKind.Interface;
            }

            return type.IsValueType ? TypeKind.Struct : TypeKind.Class;
        }

        private static IList<string> CollectBaseTypes(Type type, TypeKind kind)
        {
            if (kind == TypeKind.Enum || kind == TypeKind.Delegate)
            {
                return new List<string>();
            }

            List<Type> bases = type.GetInterfaces().ToList();
            for (Type current = type.BaseType;
                current != null && current != typeof(object) && current != typeof(ValueType);
                current = current.BaseType)
            {
                bases.Add(current);
            }

            return bases
                .Where(t => t.IsVisible)
                .Select(TypeNameFormatter.Format)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        private static IList<string> CollectEnumMembers(Type type, TypeKind kind)
        {
            if (kind != TypeKind.Enum)
            {
                return new List<string>();
            }

            // 値の順ではなく宣言順で並べたいので、値からではなくフィールドから採る。メンバーを
            // 返す順序は保証されないので、宣言順に対応するメタデータの並びで明示的に整列する。
            return type.GetFields(DeclaredPublic)
                .Where(f => f.IsLiteral)
                .OrderBy(f => f.MetadataToken)
                .Select(f => f.Name)
                .ToList();
        }

        private static IEnumerable<SignatureRecord> CollectSignatures(Type type, TypeKind kind)
        {
            if (kind == TypeKind.Enum)
            {
                yield break;
            }

            if (kind == TypeKind.Delegate)
            {
                MethodInfo invoke = type.GetMethod(DelegateInvokeName, DeclaredPublic);
                if (invoke != null)
                {
                    yield return FromMethod(type, invoke);
                }

                yield break;
            }

            HashSet<MethodInfo> accessors = CollectAccessors(type);

            foreach (MethodInfo method in type.GetMethods(DeclaredPublic).Where(m => !accessors.Contains(m)))
            {
                yield return FromMethod(type, method);
            }

            foreach (PropertyInfo property in type.GetProperties(DeclaredPublic))
            {
                yield return FromProperty(type, property);
            }

            foreach (FieldInfo field in type.GetFields(DeclaredPublic))
            {
                yield return FromField(type, field);
            }

            foreach (EventInfo declaredEvent in type.GetEvents(DeclaredPublic))
            {
                yield return FromEvent(type, declaredEvent);
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                yield return FromConstructor(type, constructor);
            }
        }

        // アクセサーは名前ではなく、プロパティ・イベントが指しているものとして集める。名前で
        // 判定すると、同じ接頭辞を持つ通常のメソッドまで落ちる。
        private static HashSet<MethodInfo> CollectAccessors(Type type)
        {
            HashSet<MethodInfo> accessors = new HashSet<MethodInfo>();

            foreach (PropertyInfo property in type.GetProperties(DeclaredPublic))
            {
                foreach (MethodInfo accessor in property.GetAccessors(true))
                {
                    accessors.Add(accessor);
                }
            }

            foreach (EventInfo declaredEvent in type.GetEvents(DeclaredPublic))
            {
                Add(accessors, declaredEvent.GetAddMethod(true));
                Add(accessors, declaredEvent.GetRemoveMethod(true));
                Add(accessors, declaredEvent.GetRaiseMethod(true));
                foreach (MethodInfo other in declaredEvent.GetOtherMethods(true))
                {
                    accessors.Add(other);
                }
            }

            return accessors;
        }

        private static void Add(HashSet<MethodInfo> accessors, MethodInfo method)
        {
            if (method != null)
            {
                accessors.Add(method);
            }
        }

        private static SignatureRecord FromMethod(Type type, MethodInfo method)
        {
            IList<ParameterRecord> parameters = ToParameters(method.GetParameters());
            int arity = method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0;
            string valueType = TypeNameFormatter.Format(method.ReturnType);
            bool hasOutOrRef = parameters.Any(p => p.Direction != ParameterDirection.In);

            return Create(
                type,
                MemberKind.Method,
                method.Name,
                method.IsStatic,
                arity,
                parameters,
                valueType,
                false,
                false,
                OperationDirectionRule.ForMethod(method.Name, valueType, hasOutOrRef));
        }

        private static SignatureRecord FromProperty(Type type, PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(false);
            MethodInfo setter = property.GetSetMethod(false);

            return Create(
                type,
                MemberKind.Property,
                property.Name,
                (getter ?? setter).IsStatic,
                0,
                ToParameters(property.GetIndexParameters()),
                TypeNameFormatter.Format(property.PropertyType),
                getter != null,
                setter != null,
                OperationDirectionRule.ForProperty(getter != null));
        }

        private static SignatureRecord FromField(Type type, FieldInfo field)
        {
            return Create(
                type,
                MemberKind.Field,
                field.Name,
                field.IsStatic,
                0,
                new List<ParameterRecord>(),
                TypeNameFormatter.Format(field.FieldType),
                true,
                !field.IsInitOnly && !field.IsLiteral,
                OperationDirectionRule.ForOtherMember());
        }

        private static SignatureRecord FromEvent(Type type, EventInfo declaredEvent)
        {
            MethodInfo adder = declaredEvent.GetAddMethod(true);

            return Create(
                type,
                MemberKind.Event,
                declaredEvent.Name,
                adder != null && adder.IsStatic,
                0,
                new List<ParameterRecord>(),
                TypeNameFormatter.Format(declaredEvent.EventHandlerType),
                false,
                false,
                OperationDirectionRule.ForOtherMember());
        }

        private static SignatureRecord FromConstructor(Type type, ConstructorInfo constructor)
        {
            return Create(
                type,
                MemberKind.Constructor,
                SignatureKeyBuilder.ConstructorName,
                false,
                0,
                ToParameters(constructor.GetParameters()),
                TypeNameFormatter.Format(type),
                false,
                false,
                OperationDirectionRule.ForOtherMember());
        }

        private static SignatureRecord Create(
            Type type,
            MemberKind memberKind,
            string memberName,
            bool isStatic,
            int genericArity,
            IList<ParameterRecord> parameters,
            string valueType,
            bool canRead,
            bool canWrite,
            OperationDirection direction)
        {
            string declaringType = TypeNameFormatter.Format(type);
            string key = SignatureKeyBuilder.Build(declaringType, memberName, genericArity, parameters, valueType);

            return new SignatureRecord(
                key,
                declaringType,
                memberKind,
                memberName,
                isStatic,
                genericArity,
                parameters,
                valueType,
                canRead,
                canWrite,
                direction);
        }

        private static IList<ParameterRecord> ToParameters(ParameterInfo[] parameters)
        {
            return parameters.Select(p =>
            {
                bool byRef = p.ParameterType.IsByRef;
                Type valueType = byRef ? p.ParameterType.GetElementType() : p.ParameterType;
                ParameterDirection direction = byRef
                    ? (p.IsOut ? ParameterDirection.Out : ParameterDirection.Ref)
                    : ParameterDirection.In;

                return new ParameterRecord(p.Name, TypeNameFormatter.Format(valueType), direction, p.IsOptional);
            }).ToList();
        }
    }
}
