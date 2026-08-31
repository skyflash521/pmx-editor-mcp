using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class AssemblyEnumeratorTests
    {
        private const string N = "PmxEditorMcp.SignatureDump.Tests.Sample.";
        private const string Other = "PmxEditorMcp.SignatureDump.Tests.OtherSample.IOtherApi";
        private const string Api = N + "ISampleApi";
        private const string Aux = N + "ISampleAux";
        private const string Base = N + "ISampleBase";
        private const string Root = N + "ISampleRoot";
        private const string BaseClass = N + "SampleBaseClass";
        private const string Data = N + "SampleData";
        private const string Derived = N + "SampleDerived";
        private const string Generic = N + "SampleGeneric<T>";
        private const string Kind = N + "SampleKind";
        private const string Nested = N + "SampleOuter+SampleNested";
        private const string Proc = N + "SampleProc";
        private const string Value = N + "SampleValue";
        private const string OuterGeneric = N + "SampleOuterGeneric<TOuter>";
        private const string InnerGeneric = OuterGeneric + "+SampleInnerGeneric<TInner>";

        // 題材の名前空間について期待する型の全体を、型が持つ全項目まで書き下したもの。名前だけを
        // 照合すると、名前は合っているが中身の項目が誤っている実装が通る。
        private static readonly string[] ExpectedTypeRows =
        {
            Api + "|Interface|top|abstract|closed|" + Aux + ";" + Base + ";" + Root + "|",
            Aux + "|Interface|top|abstract|closed||",
            Base + "|Interface|top|abstract|closed|" + Root + "|",
            Root + "|Interface|top|abstract|closed||",
            BaseClass + "|Class|top|abstract|closed|" + Root + "|",
            Data + "|Class|top|concrete|closed||",
            Derived + "|Class|top|concrete|closed|" + Aux + ";" + Root + ";" + BaseClass + "|",
            Generic + "|Class|top|concrete|generic||",
            Kind + "|Enum|top|concrete|closed||Second;First",
            N + "SampleOuter|Class|top|abstract|closed||",
            Nested + "|Class|nested|concrete|closed||",
            Proc + "|Delegate|top|concrete|closed||",
            Value + "|Struct|top|concrete|closed||",
            OuterGeneric + "|Class|top|concrete|generic||",
            InnerGeneric + "|Class|nested|concrete|generic||",
        };

        // 題材の名前空間について期待するシグネチャ行の全体を、行が持つ全項目まで書き下したもの。
        private static readonly string[] ExpectedSignatureRows =
        {
            Api + ".Apply()|" + Api + "|Apply|Method|instance|0|System.Void|--|Write|",
            Api + ".Apply<1>()|" + Api + "|Apply|Method|instance|1|System.Void|--|Write|",
            Api + ".Changed()|" + Api + "|Changed|Event|instance|0|System.EventHandler|--|Write|",
            Api + ".Convert<1>(T)|" + Api
                + "|Convert|Method|instance|1|T:typeArgument|--|Write|value:T:In:required:typeArgument",
            Api + ".Fill(System.Int32[],System.Collections.Generic.IList<System.String>)|" + Api
                + "|Fill|Method|instance|0|System.Void|--|Write|"
                + "values:System.Int32[]:In:required"
                + ";names:System.Collections.Generic.IList<System.String>:In:required",
            Api + ".GetCount()|" + Api + "|GetCount|Method|instance|0|System.Int32|--|Read|",
            Api + ".Pack(System.Byte[])|" + Api + "|Pack|Method|instance|0|System.Void|--|Write|"
                + "data:System.Byte[]:In:required",
            Api + ".Item(System.Guid)|" + Api
                + "|Item|Property|instance|0|System.String|r-|Read|key:System.Guid:In:required",
            Api + ".GetState(ref System.Int32)|" + Api
                + "|GetState|Method|instance|0|System.Boolean|--|Write|value:System.Int32:Ref:required",
            Api + ".ReadOnlyName()|" + Api + "|ReadOnlyName|Property|instance|0|System.String|r-|Read|",
            Api + ".SetThing(System.Int32,System.String)|" + Api
                + "|SetThing|Method|instance|0|System.Void|--|Write|"
                + "index:System.Int32:In:required;text:System.String:In:required",
            Api + ".Swap(ref System.Int32,ref System.Int32)|" + Api
                + "|Swap|Method|instance|0|System.Void|--|Write|"
                + "a:System.Int32:Ref:required;b:System.Int32:Ref:required",
            Api + ".TryGet(System.Int32,out System.String)|" + Api
                + "|TryGet|Method|instance|0|System.Boolean|--|Write|"
                + "index:System.Int32:In:required;text:System.String:Out:required",
            Api + ".Value()|" + Api + "|Value|Property|instance|0|System.Int32|rw|Read|",
            Api + ".Walk(" + Proc + ")|" + Api
                + "|Walk|Method|instance|0|System.Void|--|Write|step:" + Proc + ":In:required",
            Api + ".WriteOnlyLevel()|" + Api + "|WriteOnlyLevel|Property|instance|0|System.Int32|-w|Write|",
            Aux + ".AuxValue()|" + Aux + "|AuxValue|Property|instance|0|System.Int32|r-|Read|",
            Base + ".BaseValue()|" + Base + "|BaseValue|Property|instance|0|System.Int32|r-|Read|",
            Root + ".RootValue()|" + Root + "|RootValue|Property|instance|0|System.Int32|r-|Read|",
            BaseClass + ".Level()|" + BaseClass + "|Level|Property|instance|0|System.Int32|rw|Read|",
            BaseClass + ".RootValue()|" + BaseClass + "|RootValue|Property|instance|0|System.Int32|r-|Read|",
            Data + "..ctor()|" + Data + "|.ctor|Constructor|instance|0|" + Data + "|--|Write|",
            Data + "..ctor(System.Int32)|" + Data + "|.ctor|Constructor|instance|0|" + Data
                + "|--|Write|seed:System.Int32:In:required",
            Data + ".Computed()|" + Data + "|Computed|Property|instance|0|System.Int32|r-|Read|",
            Data + ".Create(System.Int32)|" + Data + "|Create|Method|static|0|" + Data
                + "|--|Write|seed:System.Int32:In:required",
            Data + ".Field()|" + Data + "|Field|Field|instance|0|System.Int32|rw|Write|",
            Data + ".Marker()|" + Data + "|Marker|Field|static|0|System.String|r-|Write|",
            Data + ".Reset(System.Int32)|" + Data
                + "|Reset|Method|instance|0|System.Void|--|Write|seed:System.Int32:In:optional",
            Data + ".Tag()|" + Data + "|Tag|Field|static|0|System.String|r-|Write|",
            Data + ".Total()|" + Data + "|Total|Property|static|0|System.Int32|rw|Read|",
            Data + ".op_Addition(" + Data + "," + Data + ")|" + Data
                + "|op_Addition|Method|static|0|" + Data + "|--|Write|"
                + "left:" + Data + ":In:required;right:" + Data + ":In:required",
            Data + ".op_Implicit(" + Data + "):System.Int32|" + Data
                + "|op_Implicit|Method|static|0|System.Int32|--|Write|value:" + Data + ":In:required",
            Data + ".op_Implicit(" + Data + "):System.Int64|" + Data
                + "|op_Implicit|Method|static|0|System.Int64|--|Write|value:" + Data + ":In:required",
            Derived + "..ctor()|" + Derived + "|.ctor|Constructor|instance|0|" + Derived + "|--|Write|",
            Derived + ".AuxValue()|" + Derived + "|AuxValue|Property|instance|0|System.Int32|r-|Read|",
            Generic + "..ctor()|" + Generic + "|.ctor|Constructor|instance|0|" + Generic + "|--|Write|",
            Generic + ".Value()|" + Generic + "|Value|Property|instance|0|T:typeArgument|rw|Read|",
            Nested + "..ctor()|" + Nested + "|.ctor|Constructor|instance|0|" + Nested + "|--|Write|",
            Nested + ".Nested()|" + Nested + "|Nested|Property|instance|0|System.Int32|rw|Read|",
            Proc + ".Invoke(System.Int32)|" + Proc
                + "|Invoke|Method|instance|0|System.Int32|--|Write|x:System.Int32:In:required",
            Value + ".X()|" + Value + "|X|Field|instance|0|System.Int32|rw|Write|",
            OuterGeneric + "..ctor()|" + OuterGeneric + "|.ctor|Constructor|instance|0|"
                + OuterGeneric + "|--|Write|",
            InnerGeneric + "..ctor()|" + InnerGeneric + "|.ctor|Constructor|instance|0|"
                + InnerGeneric + "|--|Write|",
            InnerGeneric + ".Inner()|" + InnerGeneric
                + "|Inner|Property|instance|0|TInner:typeArgument|rw|Read|",
            InnerGeneric + ".Outer()|" + InnerGeneric
                + "|Outer|Property|instance|0|TOuter:typeArgument|rw|Read|",
        };

        private static InventoryRecord Enumerate()
        {
            return AssemblyEnumerator.Enumerate(typeof(ISampleApi).Assembly);
        }

        [Fact]
        public void ArrayTypesAreRecordedByTheirElementType()
        {
            InventoryRecord inventory = Enumerate();

            Assert.DoesNotContain(
                inventory.Types.Concat(inventory.ReferencedTypes).Select(t => t.Name),
                n => n.EndsWith("]", StringComparison.Ordinal));
            Assert.Contains("System.Byte", inventory.ReferencedTypes.Select(t => t.Name));
        }

        [Fact]
        public void EverySignatureTypeHasAClassification()
        {
            InventoryRecord inventory = Enumerate();
            HashSet<string> classified = new HashSet<string>(
                inventory.Types.Concat(inventory.ReferencedTypes).Select(t => t.Name), StringComparer.Ordinal);

            string[] unclassified = inventory.Signatures
                .SelectMany(Classifiable)
                .Where(n => n != "System.Void" && !classified.Contains(n))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new string[0], unclassified);
        }

        /// <summary>
        /// 総称型引数は宣言ごとに別の型で、分類を持たない。配列は要素の型で分類する。
        /// </summary>
        private static IEnumerable<string> Classifiable(SignatureRecord signature)
        {
            IEnumerable<string> parameters = signature.Parameters
                .Where(p => !p.IsTypeArgument)
                .Select(p => Element(p.TypeName));
            IEnumerable<string> value = signature.ValueTypeIsTypeArgument
                ? new string[0]
                : new[] { Element(signature.ValueType) };

            return parameters.Concat(value);
        }

        private static string Element(string typeName)
        {
            string name = typeName.EndsWith("&", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 1)
                : typeName;

            while (name.EndsWith("]", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.LastIndexOf('['));
            }

            return name;
        }

        private static string Describe(TypeRecord type)
        {
            return string.Join(
                "|",
                type.Name,
                type.Kind.ToString(),
                type.IsNested ? "nested" : "top",
                type.IsAbstract ? "abstract" : "concrete",
                type.IsGenericTypeDefinition ? "generic" : "closed",
                string.Join(";", type.BaseTypes),
                string.Join(";", type.EnumMembers));
        }

        private static string Describe(SignatureRecord signature)
        {
            return string.Join(
                "|",
                signature.Key,
                signature.DeclaringType,
                signature.MemberName,
                signature.MemberKind.ToString(),
                signature.IsStatic ? "static" : "instance",
                signature.GenericArity.ToString(CultureInfo.InvariantCulture),
                signature.ValueType + (signature.ValueTypeIsTypeArgument ? ":typeArgument" : string.Empty),
                (signature.CanRead ? "r" : "-") + (signature.CanWrite ? "w" : "-"),
                signature.OperationDirection.ToString(),
                string.Join(";", signature.Parameters.Select(Describe)));
        }

        private static string Describe(ParameterRecord parameter)
        {
            return string.Join(
                ":",
                parameter.Name,
                parameter.TypeName,
                parameter.Direction.ToString(),
                parameter.IsOptional ? "optional" : "required")
                + (parameter.IsTypeArgument ? ":typeArgument" : string.Empty);
        }

        private static string[] Sorted(IEnumerable<string> values)
        {
            return values.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        }

        private static IEnumerable<TypeRecord> SampleTypes(InventoryRecord inventory)
        {
            return inventory.Types.Where(t => t.Name.StartsWith(N, StringComparison.Ordinal));
        }

        private static IEnumerable<SignatureRecord> SampleSignatures(InventoryRecord inventory)
        {
            return inventory.Signatures.Where(s => s.DeclaringType.StartsWith(N, StringComparison.Ordinal));
        }

        [Fact]
        public void EnumeratesSamplePublicTypesExactlyWithAllFields()
        {
            InventoryRecord inventory = Enumerate();

            Assert.Equal(Sorted(ExpectedTypeRows), Sorted(SampleTypes(inventory).Select(Describe)));

            // 外側が公開でない入れ子の型は、入れ子の側が公開でも母集合に入らない。完全一致でも
            // 落ちるが、境界そのものを名指ししておく。
            Assert.DoesNotContain(inventory.Types, t => t.Name == N + "HiddenOuter+VisibleNested");
        }

        [Fact]
        public void EnumeratesSampleSignaturesExactlyWithAllFields()
        {
            InventoryRecord inventory = Enumerate();

            Assert.Equal(Sorted(ExpectedSignatureRows), Sorted(SampleSignatures(inventory).Select(Describe)));
        }

        [Fact]
        public void PublicTypesInOtherNamespacesAreEnumerated()
        {
            InventoryRecord inventory = Enumerate();

            // 上の2つの照合は題材の名前空間へ絞ってから比べるので、特定の名前空間しか見ない実装
            // でも通ってしまう。母集合はアセンブリ全体である。
            Assert.Contains(inventory.Types, t => t.Name == Other);
            Assert.Contains(inventory.Signatures, s => s.Key == Other + ".OtherValue()");
        }

        [Fact]
        public void SignaturesForTheSameMemberAreNotDuplicated()
        {
            InventoryRecord inventory = Enumerate();

            // 行キーの重複だけを見ると、同じメンバーへ異なるキーを付けた実装を見逃す。材料は
            // 行キーと同じで、変換演算子では戻り値の型も同一性の一部になる。
            string[] identities = inventory.Signatures.Select(s => string.Join(
                "|",
                s.DeclaringType,
                s.MemberName,
                s.GenericArity.ToString(CultureInfo.InvariantCulture),
                string.Join(",", s.Parameters.Select(p => p.Direction + " " + p.TypeName)),
                SignatureKeyBuilder.ConversionOperatorNames.Contains(s.MemberName)
                    ? s.ValueType
                    : string.Empty)).ToArray();

            Assert.Equal(identities.Length, identities.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                inventory.Signatures.Count,
                inventory.Signatures.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void EnumTypesProduceNoSignatures()
        {
            InventoryRecord inventory = Enumerate();

            Assert.DoesNotContain(inventory.Signatures, s => s.DeclaringType == Kind);
        }

        [Fact]
        public void DelegateTypesProduceOnlyTheInvokeSignature()
        {
            InventoryRecord inventory = Enumerate();

            // 非同期呼び出しの組とコンストラクタは、どのデリゲートにも同じ形で現れる。
            Assert.Equal(
                new[] { Proc + ".Invoke(System.Int32)" },
                inventory.Signatures.Where(s => s.DeclaringType == Proc).Select(s => s.Key).ToArray());
        }

        [Fact]
        public void AccessorMethodsDoNotBecomeSignatures()
        {
            InventoryRecord inventory = Enumerate();

            Assert.DoesNotContain(
                inventory.Signatures, s => s.MemberName.StartsWith("get_", StringComparison.Ordinal));
            Assert.DoesNotContain(
                inventory.Signatures, s => s.MemberName.StartsWith("set_", StringComparison.Ordinal));
            Assert.DoesNotContain(
                inventory.Signatures, s => s.MemberName.StartsWith("add_", StringComparison.Ordinal));
            Assert.DoesNotContain(
                inventory.Signatures, s => s.MemberName.StartsWith("remove_", StringComparison.Ordinal));

            // 除くのはアクセサーだけである。演算子も言語が特別な名前を与えるメソッドなので、
            // 特別な名前を一律に除いた実装はここで落ちる。
            Assert.Contains(inventory.Signatures, s => s.MemberName == "op_Addition");
            Assert.Contains(inventory.Signatures, s => s.MemberName == "op_Implicit");
        }

        [Fact]
        public void InheritedMembersDoNotBecomeSignaturesOfTheDerivedType()
        {
            InventoryRecord inventory = Enumerate();

            Assert.DoesNotContain(
                inventory.Signatures, s => s.DeclaringType == Api && s.MemberName == "BaseValue");
            Assert.DoesNotContain(
                inventory.Signatures, s => s.DeclaringType == Derived && s.MemberName == "Level");
            Assert.DoesNotContain(
                inventory.Signatures, s => s.DeclaringType == Data && s.MemberName == "ToString");
        }

        [Fact]
        public void EnumerationIsSortedAscending()
        {
            InventoryRecord inventory = Enumerate();

            Assert.Equal(
                Sorted(inventory.Types.Select(t => t.Name)), inventory.Types.Select(t => t.Name).ToArray());
            Assert.Equal(
                Sorted(inventory.Signatures.Select(s => s.Key)),
                inventory.Signatures.Select(s => s.Key).ToArray());
        }

        [Fact]
        public void AssemblyIdentityIsIncluded()
        {
            InventoryRecord inventory = Enumerate();
            AssemblyName name = typeof(ISampleApi).Assembly.GetName();

            Assert.Equal(name.Name, inventory.AssemblyName);
            Assert.Equal(name.Version.ToString(), inventory.AssemblyVersion);
        }

        [Fact]
        public void MissingAssemblyThrows()
        {
            Assert.Throws<ArgumentNullException>(() => AssemblyEnumerator.Enumerate(null));
        }
    }
}
