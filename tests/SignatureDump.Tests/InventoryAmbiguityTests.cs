using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class InventoryAmbiguityTests
    {
        private const string Parameter = "TShared";

        private const string Generic = "N.IHolder<" + Parameter + ">";

        [Fact]
        public void AnEnumerationWithoutASharedNamePasses()
        {
            InventoryAmbiguity.Require(Inventory(new List<TypeRecord>(), new List<TypeRecord>()));
        }

        [Fact]
        public void ATypeSharingItsNameWithATypeParameterOfAGenericTypeThrows()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InventoryAmbiguity.Require(Inventory(
                    new List<TypeRecord> { Type(Parameter) }, new List<TypeRecord>())));

            Assert.Contains(Parameter, error.Message);
        }

        [Fact]
        public void ATypeSharingItsNameOnlyInTheReferencedTypesThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => InventoryAmbiguity.Require(Inventory(
                    new List<TypeRecord>(), new List<TypeRecord> { Type(Parameter) })));
        }

        [Fact]
        public void ATypeSharingItsNameWithATypeParameterOfAGenericMethodThrows()
        {
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord> { Type(Parameter) },
                new List<TypeRecord>(),
                new List<SignatureRecord> { GenericMethod() });

            Assert.Throws<InvalidOperationException>(() => InventoryAmbiguity.Require(inventory));
        }

        [Fact]
        public void ATypeSharingItsNameWithATypeParameterOfANestedGenericTypeThrows()
        {
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord>
                {
                    Type(Parameter),
                    GenericType("N.Outer<TOuter>+Inner<" + Parameter + ">"),
                },
                new List<TypeRecord>(),
                new List<SignatureRecord>());

            Assert.Throws<InvalidOperationException>(() => InventoryAmbiguity.Require(inventory));
        }

        [Fact]
        public void TypesWithDistinctNamesPass()
        {
            InventoryAmbiguity.RequireDistinctNames(new[] { typeof(string), typeof(int) });
        }

        [Fact]
        public void TwoTypesWritingTheSameNameThrow()
        {
            Type emitted = EmitTypeNamed(typeof(InventoryAmbiguityTests).FullName);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InventoryAmbiguity.RequireDistinctNames(
                    new[] { typeof(InventoryAmbiguityTests), emitted }));

            Assert.Contains(typeof(InventoryAmbiguityTests).FullName, error.Message);
        }

        [Fact]
        public void TwoGenericDefinitionsWritingTheSameNameThrow()
        {
            ModuleBuilder outside = EmitModule("OutsideBox");
            ModuleBuilder inside = EmitModule("InsideBox");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InventoryAmbiguity.RequireDistinctNames(new[]
                {
                    EmitGeneric(outside, "N.Box").MakeGenericType(typeof(int)),
                    EmitGeneric(inside, "N.Box").MakeGenericType(typeof(string)),
                }));

            Assert.Contains("N.Box", error.Message);
        }

        [Fact]
        public void TheSameGenericDefinitionClosedTwoWaysPasses()
        {
            Type definition = EmitGeneric(EmitModule("OneBox"), "N.Box");

            InventoryAmbiguity.RequireDistinctNames(new[]
            {
                definition.MakeGenericType(typeof(int)),
                definition.MakeGenericType(typeof(string)),
            });
        }

        [Fact]
        public void AGenericTypeWritingTheNameOfATypeParameterPasses()
        {
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord> { GenericType(Parameter + "<TInner>") },
                new List<TypeRecord>(),
                new List<SignatureRecord> { GenericMethod() });

            InventoryAmbiguity.Require(inventory);
        }

        private static ModuleBuilder EmitModule(string name)
        {
            return AppDomain.CurrentDomain
                .DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run)
                .DefineDynamicModule(name);
        }

        private static Type EmitGeneric(ModuleBuilder module, string name)
        {
            TypeBuilder builder = module.DefineType(name, TypeAttributes.Public);
            builder.DefineGenericParameters("T");

            return builder.CreateType();
        }

        [Fact]
        public void TheSameTypeListedTwicePasses()
        {
            InventoryAmbiguity.RequireDistinctNames(new[] { typeof(string), typeof(string) });
        }

        [Fact]
        public void RequiringDistinctNamesWithoutTypesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => InventoryAmbiguity.RequireDistinctNames(null));
        }

        /// <summary>
        /// 同じ完全名の型は1つのアセンブリには置けないので、別のアセンブリを実行時に組み立てる。
        /// </summary>
        private static Type EmitTypeNamed(string fullName)
        {
            ModuleBuilder module = AppDomain.CurrentDomain
                .DefineDynamicAssembly(new AssemblyName("Collision"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("Collision");

            return module.DefineType(fullName, TypeAttributes.Public).CreateType();
        }

        [Fact]
        public void RequiringWithoutAnInventoryThrows()
        {
            Assert.Throws<ArgumentNullException>(() => InventoryAmbiguity.Require(null));
        }

        private static InventoryRecord Inventory(
            IList<TypeRecord> extraTypes, IList<TypeRecord> referenced)
        {
            List<TypeRecord> types = new List<TypeRecord> { GenericType(Generic) };
            foreach (TypeRecord type in extraTypes)
            {
                types.Add(type);
            }

            return new InventoryRecord(
                "PEPlugin", "0.0.0.0", types, referenced, new List<SignatureRecord>());
        }

        private static SignatureRecord GenericMethod()
        {
            IList<ParameterRecord> parameters = new ParameterRecord[0];

            return new SignatureRecord(
                "N.IThing.Pick<1>()",
                "N.IThing",
                MemberKind.Method,
                "Pick",
                false,
                1,
                parameters,
                "System.Void",
                false,
                false,
                OperationDirection.Write,
                false,
                new[] { Parameter });
        }

        private static TypeRecord Type(string name)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, true, false, new List<string>(), new List<string>());
        }

        private static TypeRecord GenericType(string name)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, true, true, new List<string>(), new List<string>());
        }
    }
}
