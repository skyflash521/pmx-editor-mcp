using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    public enum ParameterDirection
    {
        In,
        Out,
        Ref,
    }

    public enum MemberKind
    {
        Method,
        Property,
        Field,
        Event,
        Constructor,
    }

    public enum OperationDirection
    {
        Read,
        Write,
    }

    /// <summary>型の分類。メンバー列挙の可否と型役割の判定の材料になる。</summary>
    public enum TypeKind
    {
        Interface,
        Class,
        Struct,
        Enum,
        Delegate,
    }

    public sealed class ParameterRecord
    {
        public ParameterRecord(string name, string typeName, ParameterDirection direction, bool isOptional)
        {
            Name = name;
            TypeName = typeName;
            Direction = direction;
            IsOptional = isOptional;
        }

        public string Name { get; }

        /// <summary>参照渡しでも要素型の表記とする。向きは <see cref="Direction"/> が持つ。</summary>
        public string TypeName { get; }

        public ParameterDirection Direction { get; }

        public bool IsOptional { get; }
    }

    public sealed class SignatureRecord
    {
        public SignatureRecord(
            string key,
            string declaringType,
            MemberKind memberKind,
            string memberName,
            bool isStatic,
            int genericArity,
            IList<ParameterRecord> parameters,
            string valueType,
            bool canRead,
            bool canWrite,
            OperationDirection operationDirection)
        {
            Key = key;
            DeclaringType = declaringType;
            MemberKind = memberKind;
            MemberName = memberName;
            IsStatic = isStatic;
            GenericArity = genericArity;
            Parameters = parameters;
            ValueType = valueType;
            CanRead = canRead;
            CanWrite = canWrite;
            OperationDirection = operationDirection;
        }

        /// <summary>
        /// 宣言型・メンバー名・総称型引数の数・引数の型と向きの列から一意に決まる識別子。
        /// 戻り値の型だけが違うオーバーロードを言語が許す変換演算子では、戻り値の型も含む。
        /// </summary>
        public string Key { get; }

        public string DeclaringType { get; }

        public MemberKind MemberKind { get; }

        /// <summary>コンストラクタは <c>.ctor</c>。</summary>
        public string MemberName { get; }

        public bool IsStatic { get; }

        /// <summary>総称メソッドの型引数の数。総称でなければ0。</summary>
        public int GenericArity { get; }

        /// <summary>宣言順の引数。</summary>
        public IList<ParameterRecord> Parameters { get; }

        /// <summary>
        /// メソッドは戻り値型、プロパティ・フィールドはその型、イベントはハンドラ型、
        /// コンストラクタは宣言型。
        /// </summary>
        public string ValueType { get; }

        /// <summary>
        /// 値を読めるかどうか。プロパティとフィールドのみ意味を持つ。プロパティは公開の取得
        /// アクセサーを持つかどうかで決まり、フィールドは常に読める。
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        /// 値を書けるかどうか。プロパティとフィールドのみ意味を持つ。プロパティは公開の設定
        /// アクセサーを持つかどうかで決まり、フィールドは読み取り専用でも定数でもないかどうかで
        /// 決まる。
        /// </summary>
        public bool CanWrite { get; }

        public OperationDirection OperationDirection { get; }
    }

    public sealed class TypeRecord
    {
        public TypeRecord(
            string name,
            TypeKind kind,
            bool isNested,
            bool isAbstract,
            bool isGenericTypeDefinition,
            IList<string> baseTypes,
            IList<string> enumMembers)
        {
            Name = name;
            Kind = kind;
            IsNested = isNested;
            IsAbstract = isAbstract;
            IsGenericTypeDefinition = isGenericTypeDefinition;
            BaseTypes = baseTypes;
            EnumMembers = enumMembers;
        }

        public string Name { get; }

        public TypeKind Kind { get; }

        public bool IsNested { get; }

        public bool IsAbstract { get; }

        public bool IsGenericTypeDefinition { get; }

        /// <summary>
        /// その型が継承・実装している公開の型の表記(表記の昇順)。基底クラスの連鎖と、
        /// インターフェースを推移的に含む。<see cref="object"/> や構造体の基底のように、言語が
        /// その種類のすべての型へ与える基底は含めない。列挙型とデリゲートでは、基底が型の種類で
        /// 一つに決まるので空。
        /// </summary>
        public IList<string> BaseTypes { get; }

        /// <summary>列挙型の列挙子名。宣言順。列挙型でなければ空。</summary>
        public IList<string> EnumMembers { get; }
    }

    public sealed class InventoryRecord
    {
        public InventoryRecord(
            string assemblyName,
            string assemblyVersion,
            IList<TypeRecord> types,
            IList<SignatureRecord> signatures)
        {
            AssemblyName = assemblyName;
            AssemblyVersion = assemblyVersion;
            Types = types;
            Signatures = signatures;
        }

        public string AssemblyName { get; }

        public string AssemblyVersion { get; }

        /// <summary>表記の昇順。</summary>
        public IList<TypeRecord> Types { get; }

        /// <summary>行キーの昇順。</summary>
        public IList<SignatureRecord> Signatures { get; }
    }
}
