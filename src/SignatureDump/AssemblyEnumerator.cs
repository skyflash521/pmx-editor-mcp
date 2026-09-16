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
    /// 行にするのは、各型が自分で宣言する公開メンバーと、その型が継承する公開メンバーのうち、
    /// メソッド・プロパティ・フィールド・イベント・コンストラクタの5種類だけである。継承した
    /// メンバーを行にするのは、宣言元が対象アセンブリの外の型であるものに限る——SDKの中の基底型
    /// まで含めると、基底が宣言する同じAPIに派生型のぶんだけ行が立つ。
    /// <see cref="object"/>・<see cref="ValueType"/>・<see cref="Enum"/>・<see cref="Delegate"/>・
    /// <see cref="MulticastDelegate"/> が宣言するものは、実行環境がすべての型へ配るので行にしない。
    /// 静的なメンバーと、型引数の決まっていない型が継承するメンバーも行にしない。プロパティとイベントの取得・設定・追加・削除の
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

        private const BindingFlags InheritedPublic =
            BindingFlags.Public | BindingFlags.Instance;

        /// <summary>実行環境がすべての型へ配るメンバーの宣言元。</summary>
        private static readonly Type[] Universal =
        {
            typeof(object),
            typeof(ValueType),
            typeof(Enum),
            typeof(Delegate),
            typeof(MulticastDelegate),
        };

        public static InventoryRecord Enumerate(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type[] types = assembly.GetTypes().Where(t => t.IsVisible).ToArray();
            List<TypeRecord> typeRecords = new List<TypeRecord>();
            List<SignatureRecord> signatures = new List<SignatureRecord>();
            HashSet<Type> referenced = new HashSet<Type>();
            HashSet<Type> inherited = new HashSet<Type>();

            foreach (Type type in types)
            {
                TypeKind kind = ClassifyType(type);
                typeRecords.Add(Describe(type, kind));
                signatures.AddRange(CollectSignatures(type, kind));

                foreach (Type used in CollectReferencedTypes(type, kind))
                {
                    Spread(used, referenced);
                }
            }

            foreach (Type type in types.Concat(referenced))
            {
                foreach (Type baseType in BaseTypes(type, ClassifyType(type)))
                {
                    Spread(baseType, inherited);
                }
            }

            InventoryAmbiguity.RequireDistinctNames(types.Concat(referenced).Concat(inherited));

            HashSet<string> declared = new HashSet<string>(
                typeRecords.Select(t => t.Name), StringComparer.Ordinal);
            List<TypeRecord> referencedRecords = referenced
                .Select(t => Describe(t, ClassifyType(t)))
                .Where(t => !declared.Contains(t.Name))
                .ToList();

            InventoryAmbiguity.RequireNoSharedName(
                typeRecords.Concat(referencedRecords).Select(t => t.Name)
                    .Concat(inherited.Select(TypeNameFormatter.Format)),
                typeRecords.Concat(referencedRecords)
                    .Where(t => t.IsGenericTypeDefinition)
                    .SelectMany(t => InventoryAmbiguity.TypeParameterNames(t.Name))
                    .Concat(signatures.SelectMany(s => s.TypeParameters)));

            AssemblyName name = assembly.GetName();
            return new InventoryRecord(
                name.Name,
                name.Version.ToString(),
                typeRecords.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
                referencedRecords.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
                signatures.OrderBy(s => s.Key, StringComparer.Ordinal).ToList());
        }

        // 総称型の引数もそれ自体が分類の対象になるので、閉じた総称型とあわせて記録する。
        private static void Spread(Type used, ISet<Type> referenced)
        {
            Type type = Element(used);
            if (type == null || type == typeof(void) || type.IsGenericParameter)
            {
                return;
            }

            if (!referenced.Add(type) || !type.IsGenericType)
            {
                return;
            }

            foreach (Type argument in type.GetGenericArguments())
            {
                Spread(argument, referenced);
            }
        }

        private static TypeRecord Describe(Type type, TypeKind kind)
        {
            return new TypeRecord(
                TypeNameFormatter.Format(type),
                kind,
                type.IsNested,
                type.IsAbstract,
                type.IsGenericTypeDefinition,
                CollectBaseTypes(type, kind),
                CollectEnumMembers(type, kind),
                kind == TypeKind.Enum && type.IsDefined(typeof(FlagsAttribute), false));
        }

        /// <summary>
        /// メンバーが引数・戻り値・値の型として指している型。配列は要素の型へ落として返すので、
        /// 引く側も同じ形へ落としてから型の種類を引く。
        /// </summary>
        private static IEnumerable<Type> CollectReferencedTypes(Type type, TypeKind kind)
        {
            if (kind == TypeKind.Enum)
            {
                yield break;
            }

            foreach (MethodInfo method in DeclaredMethods(type, kind))
            {
                foreach (Type used in Used(method.GetParameters()))
                {
                    yield return used;
                }

                yield return Element(method.ReturnType);
            }

            if (kind == TypeKind.Delegate)
            {
                yield break;
            }

            foreach (PropertyInfo property in DeclaredProperties(type))
            {
                foreach (Type used in Used(property.GetIndexParameters()))
                {
                    yield return used;
                }

                yield return Element(property.PropertyType);
            }

            foreach (FieldInfo field in DeclaredFields(type))
            {
                yield return Element(field.FieldType);
            }

            foreach (EventInfo declaredEvent in DeclaredEvents(type))
            {
                yield return Element(declaredEvent.EventHandlerType);
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (Type used in Used(constructor.GetParameters()))
                {
                    yield return used;
                }
            }
        }

        private static IEnumerable<MethodInfo> DeclaredMethods(Type type, TypeKind kind)
        {
            if (kind == TypeKind.Delegate)
            {
                MethodInfo invoke = type.GetMethod(DelegateInvokeName, DeclaredPublic);
                return invoke == null ? new MethodInfo[0] : new[] { invoke };
            }

            HashSet<MethodInfo> accessors = CollectAccessors(type);

            return General(
                type,
                Members(type, t => t.GetMethods(DeclaredPublic), t => t.GetMethods(InheritedPublic))
                    .Where(m => !accessors.Contains(m))
                    .ToList());
        }

        /// <summary>
        /// 同じ名前と同じ引数の数で多重定義されたもののうち、ほかを受け取れる一般の側だけを残す。
        /// 継承で1つの型へ集まる多重定義には、基底の型を取る版と派生の型を取る版が並ぶことがあり、
        /// 派生の版で呼べることは基底の版でも呼べる。両方を残すと、同じ呼び出しに2つのツールが
        /// 立ち、引数の名前が同じなので呼び分けを入力で判別できなくなる。自分で宣言するものは
        /// その型の契約そのものなので落とさない。
        /// </summary>
        private static IEnumerable<MethodInfo> General(Type type, IList<MethodInfo> methods)
        {
            foreach (MethodInfo method in methods)
            {
                if (method.DeclaringType == type || !methods.Any(other => Covers(other, method)))
                {
                    yield return method;
                }
            }
        }

        /// <summary>その多重定義が、もう一方の引数をそのまま受け取れるか。同じものは覆わない。</summary>
        private static bool Covers(MethodInfo one, MethodInfo other)
        {
            ParameterInfo[] mine = one.GetParameters();
            ParameterInfo[] theirs = other.GetParameters();
            if (one == other
                || !string.Equals(one.Name, other.Name, StringComparison.Ordinal)
                || mine.Length != theirs.Length
                || mine.Length == 0)
            {
                return false;
            }

            bool wider = false;
            for (int at = 0; at < mine.Length; at++)
            {
                if (!mine[at].ParameterType.IsAssignableFrom(theirs[at].ParameterType))
                {
                    return false;
                }

                wider = wider || mine[at].ParameterType != theirs[at].ParameterType;
            }

            return wider;
        }

        private static IEnumerable<PropertyInfo> DeclaredProperties(Type type)
        {
            return Members(
                type, t => t.GetProperties(DeclaredPublic), t => t.GetProperties(InheritedPublic));
        }

        private static IEnumerable<FieldInfo> DeclaredFields(Type type)
        {
            return Members(type, t => t.GetFields(DeclaredPublic), t => t.GetFields(InheritedPublic));
        }

        private static IEnumerable<EventInfo> DeclaredEvents(Type type)
        {
            return Members(type, t => t.GetEvents(DeclaredPublic), t => t.GetEvents(InheritedPublic));
        }

        /// <summary>
        /// その型の行にするメンバー。自分で宣言するものを先に、継承するもののうち持ち込む条件を
        /// 満たすものを後に並べる。<paramref name="declared"/> は型が自分で宣言するものを、
        /// <paramref name="reachable"/> は継承したものまで含めて引く。インタフェースは基底の
        /// メンバーをこの引き方では返さないので、実装している側を1つずつ辿る。
        /// </summary>
        private static IEnumerable<TMember> Members<TMember>(
            Type type,
            Func<Type, TMember[]> declared,
            Func<Type, TMember[]> reachable)
            where TMember : MemberInfo
        {
            TMember[] own = declared(type);
            foreach (TMember member in own)
            {
                yield return member;
            }

            if (type.IsGenericTypeDefinition)
            {
                yield break;
            }

            HashSet<TMember> held = new HashSet<TMember>(own);
            IEnumerable<TMember> beyond = type.IsInterface
                ? type.GetInterfaces().SelectMany(declared)
                : reachable(type);
            foreach (TMember member in beyond)
            {
                if (!held.Add(member) || !Inherits(type, member))
                {
                    continue;
                }

                yield return member;
            }
        }

        /// <summary>その継承したメンバーを行にするか。宣言元と、静的かどうかで決まる。</summary>
        private static bool Inherits(Type type, MemberInfo member)
        {
            Type declaring = member.DeclaringType;
            if (declaring == null
                || declaring.Assembly == type.Assembly
                || Universal.Contains(declaring))
            {
                return false;
            }

            FieldInfo field = member as FieldInfo;
            MethodInfo method = member as MethodInfo;

            return !(field != null && field.IsStatic) && !(method != null && method.IsStatic);
        }

        private static IEnumerable<Type> Used(ParameterInfo[] parameters)
        {
            return parameters.Select(p => Element(p.ParameterType));
        }

        // 総称型引数は宣言ごとに別の型になり、分類の対象にならない。配列は要素の型で分類するので、
        // 次元に依らず要素まで辿る。
        private static Type Element(Type type)
        {
            if (type == null)
            {
                return null;
            }

            Type element = type;
            while (element.IsByRef || element.IsArray)
            {
                element = element.GetElementType();
            }

            return element.IsGenericParameter ? null : element;
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
            return BaseTypes(type, kind)
                .Select(TypeNameFormatter.Format)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<Type> BaseTypes(Type type, TypeKind kind)
        {
            if (kind == TypeKind.Enum || kind == TypeKind.Delegate)
            {
                return new List<Type>();
            }

            List<Type> bases = type.GetInterfaces().ToList();
            for (Type current = type.BaseType;
                current != null && current != typeof(object) && current != typeof(ValueType);
                current = current.BaseType)
            {
                bases.Add(current);
            }

            return bases.Where(t => t.IsVisible);
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

            foreach (MethodInfo method in DeclaredMethods(type, kind))
            {
                yield return FromMethod(type, method);
            }

            if (kind == TypeKind.Delegate)
            {
                yield break;
            }

            foreach (PropertyInfo property in DeclaredProperties(type))
            {
                yield return FromProperty(type, property);
            }

            foreach (FieldInfo field in DeclaredFields(type))
            {
                yield return FromField(type, field);
            }

            foreach (EventInfo declaredEvent in DeclaredEvents(type))
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

            foreach (PropertyInfo property in DeclaredProperties(type))
            {
                foreach (MethodInfo accessor in property.GetAccessors(true))
                {
                    accessors.Add(accessor);
                }
            }

            foreach (EventInfo declaredEvent in DeclaredEvents(type))
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
            IList<string> typeParameters = method.IsGenericMethodDefinition
                ? method.GetGenericArguments().Select(a => a.Name).ToList()
                : (IList<string>)new string[0];
            int arity = typeParameters.Count;
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
                OperationDirectionRule.ForMethod(method.Name, valueType, hasOutOrRef),
                IsTypeArgument(method.ReturnType),
                typeParameters);
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
                OperationDirectionRule.ForProperty(getter != null),
                IsTypeArgument(property.PropertyType));
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
                OperationDirectionRule.ForOtherMember(),
                IsTypeArgument(field.FieldType));
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
                OperationDirectionRule.ForOtherMember(),
                IsTypeArgument(declaredEvent.EventHandlerType));
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
            OperationDirection direction,
            bool valueTypeIsTypeArgument = false,
            IList<string> typeParameters = null)
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
                direction,
                valueTypeIsTypeArgument,
                typeParameters);
        }

        private static bool IsTypeArgument(Type type)
        {
            return (type.IsByRef ? type.GetElementType() : type).IsGenericParameter;
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

                return new ParameterRecord(
                    p.Name,
                    TypeNameFormatter.Format(valueType),
                    direction,
                    p.IsOptional,
                    valueType.IsGenericParameter);
            }).ToList();
        }
    }
}
