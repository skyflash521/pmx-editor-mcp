using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class TypeRolePopulationTests
    {
        private const string Target = "PEPlugin.Pmx.IPXVertex";

        private const string TargetBase = "PEPlugin.Pmx.IPXVertexBase";

        private const string Payload = "PXCPlugin.Event.PXEventArgs+ViewMouse";

        private const string Listener = "PXCPlugin.Event.IPXViewEventListener";

        private const string Handler = "System.EventHandler";

        private const string ExternalPayload = "System.Windows.Forms.KeyEventArgs";

        private const string ExternalBase = "System.ICloneable";

        private const string Generic = "PEPlugin.Vme.IPEVmePrimaryValue";

        private const string GenericDefinition = Generic + "<TValue>";

        private const string GenericKey = Generic + "<1>";

        private const string Arity2 = Generic + "<TFirst,TSecond>";

        private const string Arity2Key = Generic + "<2>";

        private const string Arity2Base = "PEPlugin.Vme.IPEVmePairValue";

        private const string GenericBase = "PEPlugin.Vme.IPEVmeValue";

        private const string Nested = "PXCPlugin.UIModel.PXOuter<TOuter>+PXInner<TInner>";

        private const string NestedDefinition = "PXCPlugin.UIModel.PXOuter<1>+PXInner<1>";

        private const string SharedExternal = "System.ComponentModel.CancelEventArgs";

        private const string ExternalWithoutNamespace = "GlobalPayload";

        private const string ArgumentOnlyWithoutNamespace = "GlobalArgument";

        private const string MethodTypeParameter = "TMethod";

        private const string ConcreteNamedLikeParameter = "TShared";

        private const string Collided = "TCollided";

        private const string Solo = "PEPlugin.Vme.IPEVmeSoloValue";

        private const string SoloDefinition = Solo + "<TSolo>";

        private const string SoloKey = Solo + "<1>";

        private const string Box = "PXCPlugin.UIModel.PXBox";

        private const string BoxDefinition = Box + "<TBox>";

        private const string BoxKey = Box + "<1>";

        private const string NotSupported = "PEPlugin.Pmd.IPEPmd";

        private const string ExcludedKey = Target + ".Clone(PEPlugin.Pmd.IPEPmd)";

        [Fact]
        public void ProvidedSignaturesAreTheProvidedRowsMinusTheExcludedOnes()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains(Target + ".Position()", population.Signatures);
            Assert.DoesNotContain(ExcludedKey, population.Signatures);
            Assert.DoesNotContain(NotSupported + ".Name()", population.Signatures);
        }

        [Fact]
        public void TypesTheValueTableCoversAreSeparatedFromTheRoleTypes()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains("PEPlugin.SDX.V3", population.ValueMapped);
            Assert.Contains("System.String", population.ValueMapped);
            Assert.DoesNotContain("PEPlugin.SDX.V3", population.RoleTypes);
            Assert.Contains(Target, population.RoleTypes);
            Assert.DoesNotContain(Target, population.ValueMapped);
        }

        [Fact]
        public void ArraysAndByReferenceMarksAreResolvedToTheElementType()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains("System.Int32", population.ValueMapped);
            Assert.DoesNotContain("System.Int32[]", population.ValueMapped);
            Assert.DoesNotContain("System.Int32&", population.ValueMapped);
        }

        [Fact]
        public void ContainersAreCountedAsValueMappedInsteadOfRoleTypes()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains("System.Collections.Generic.IList<1>", population.ValueMapped);
            Assert.DoesNotContain("System.Collections.Generic.IList<1>", population.RoleTypes);
        }

        [Fact]
        public void BaseTypesOfTheAssemblyAreRoleTypesEvenWithoutAppearingInASignature()
        {
            Assert.Contains(TargetBase, Resolve().RoleTypes);
        }

        [Fact]
        public void MembersInheritedFromABaseTypeBringTheirOwnTypesIn()
        {
            Assert.Contains(Payload, Resolve().RoleTypes);
        }

        [Fact]
        public void BaseTypesDeclaredOutsideTheAssemblyAreNotRoleTypes()
        {
            Assert.DoesNotContain(ExternalBase, Resolve().RoleTypes);
        }

        [Fact]
        public void TypeArgumentsOfAnEventHandlerAreRoleTypesEvenWhenDeclaredOutside()
        {
            Assert.Contains(ExternalPayload, Resolve().RoleTypes);
        }

        [Fact]
        public void HandlerDelegatesAreNeitherValueMappedNorRoleTypes()
        {
            TypeRolePopulation population = Resolve();

            Assert.DoesNotContain(Handler, population.RoleTypes);
            Assert.DoesNotContain(Handler, population.ValueMapped);
        }

        [Fact]
        public void TypeParametersAreNeitherValueMappedNorRoleTypes()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains(GenericKey, population.RoleTypes);
            Assert.DoesNotContain("TValue", population.RoleTypes);
            Assert.DoesNotContain("TValue", population.ValueMapped);
        }

        [Fact]
        public void TypeParametersOfAGenericMethodAreExcludedFromInsideAGenericType()
        {
            TypeRolePopulation population = Resolve();

            Assert.DoesNotContain(MethodTypeParameter, population.RoleTypes);
            Assert.DoesNotContain(MethodTypeParameter, population.ValueMapped);
        }

        [Fact]
        public void AConcreteTypeNamedLikeAnotherTypeParameterIsStillARoleType()
        {
            Assert.Contains(ConcreteNamedLikeParameter, Resolve().RoleTypes);
        }

        [Fact]
        public void AGenericTypeDeclaringAProvidedMemberIsARoleType()
        {
            Assert.Contains(SoloKey, Resolve().RoleTypes);
        }

        [Fact]
        public void AGenericTypeDeclaringAProvidedConstructorIsARoleType()
        {
            Assert.Contains(BoxKey, Resolve().RoleTypes);
        }

        [Fact]
        public void ATypeSharingItsNameWithATypeParameterOnlyInTheReferencedTypesStops()
        {
            List<TypeRecord> referenced = new List<TypeRecord>(Inventory().ReferencedTypes)
            {
                Type(Collided, TypeKind.Interface),
            };
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                Inventory().Types,
                referenced,
                Collide(Inventory().Signatures));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRolePopulation.Resolve(Ledger(), inventory, Excluded()));

            Assert.Contains(Collided, error.Message);
        }

        [Fact]
        public void ATypeSharingItsNameWithATypeParameterStops()
        {
            List<TypeRecord> types = new List<TypeRecord>(Inventory().Types)
            {
                Type(Collided, TypeKind.Interface),
            };
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                types,
                Inventory().ReferencedTypes,
                Collide(Inventory().Signatures));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRolePopulation.Resolve(Ledger(), inventory, Excluded()));

            Assert.Contains(Collided, error.Message);
        }

        private static IList<SignatureRecord> Collide(IList<SignatureRecord> signatures)
        {
            return signatures
                .Select(s => s.MemberName == "Pick"
                    ? GenericMethod(
                        Listener, "Pick", Collided, "System.Collections.Generic.IList<" + Collided + ">")
                    : s)
                .ToList();
        }

        [Fact]
        public void TypesWithTheSameNameAndAnotherNumberOfTypeArgumentsStaySeparate()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains(GenericKey, population.RoleTypes);
            Assert.Contains(Arity2Key, population.RoleTypes);
            Assert.Contains(GenericBase, population.RoleTypes);
            Assert.Contains(Arity2Base, population.RoleTypes);
        }

        [Fact]
        public void AConcreteTypeWithoutANamespaceIsCollectedFromInsideATypeArgument()
        {
            Assert.Contains(ArgumentOnlyWithoutNamespace, Resolve().RoleTypes);
        }

        [Fact]
        public void BaseTypesOfAGenericDefinitionAreReachedThroughTheStrippedName()
        {
            Assert.Contains(GenericBase, Resolve().RoleTypes);
        }

        [Fact]
        public void NestedGenericTypesAreStrippedAtEveryLevel()
        {
            TypeRolePopulation population = Resolve();

            Assert.Contains(NestedDefinition, population.RoleTypes);
            Assert.DoesNotContain("TOuter", population.RoleTypes);
            Assert.DoesNotContain("TInner", population.RoleTypes);
        }

        [Fact]
        public void AnExternalTypeReachedBothAsABaseTypeAndInASignatureIsARoleType()
        {
            Assert.Contains(SharedExternal, Resolve().RoleTypes);
        }

        [Fact]
        public void AnExternalTypeWithoutANamespaceIsARoleTypeWhenTheEnumerationKnowsIt()
        {
            Assert.Contains(ExternalWithoutNamespace, Resolve().RoleTypes);
        }

        [Fact]
        public void EveryReachedTypeIsInExactlyOneOfTheTwoSets()
        {
            TypeRolePopulation population = Resolve();

            Assert.Equal(
                new[]
                {
                    Arity2Base,
                    BoxKey,
                    SoloKey,
                    Arity2Key,
                    ArgumentOnlyWithoutNamespace,
                    ConcreteNamedLikeParameter,
                    ExternalPayload,
                    ExternalWithoutNamespace,
                    GenericBase,
                    GenericKey,
                    Listener,
                    NestedDefinition,
                    Payload,
                    SharedExternal,
                    Target,
                    TargetBase,
                }.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                population.RoleTypes.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal(
                new[]
                {
                    "PEPlugin.SDX.V3",
                    "System.Collections.Generic.IList<1>",
                    "System.Int32",
                    "System.String",
                }.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                population.ValueMapped.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TypesReachedOnlyThroughRowsThatAreNotProvidedStayOut()
        {
            Assert.DoesNotContain(NotSupported, Resolve().RoleTypes);
        }

        [Fact]
        public void ResolvingWithoutTheInputsThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => TypeRolePopulation.Resolve(null, Inventory(), Excluded()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRolePopulation.Resolve(Ledger(), null, Excluded()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRolePopulation.Resolve(Ledger(), Inventory(), null));
        }

        private static TypeRolePopulation Resolve()
        {
            return TypeRolePopulation.Resolve(Ledger(), Inventory(), Excluded());
        }

        private static IList<ExcludedSignatureRecord> Excluded()
        {
            return new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromCategory(ExcludedKey, ExclusionCategory.PmdModel, string.Empty),
            };
        }

        private static IList<CapabilityRecord> Ledger()
        {
            return new List<CapabilityRecord>
            {
                Row("CAP-001", Target, CapabilityStatus.Provided),
                Row("CAP-002", Listener, CapabilityStatus.Provided),
                Row("CAP-004", NotSupported, CapabilityStatus.NotSupported),
                Row("CAP-005", Solo, CapabilityStatus.Provided),
                Row("CAP-006", Box, CapabilityStatus.Provided),
                Pattern("CAP-463", "PEPlugin.Pmd.*"),
                Pattern("CAP-466", "PEPlugin.SDX.*"),
            };
        }

        private static CapabilityRecord Pattern(string id, string target)
        {
            return new CapabilityRecord(
                id,
                "分類",
                target,
                CapabilityTargetKind.Pattern,
                new List<string> { target },
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }

        private static CapabilityRecord Row(string id, string target, CapabilityStatus status)
        {
            return new CapabilityRecord(
                id,
                "分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                status,
                status == CapabilityStatus.Provided ? CapabilityOwner.Model : CapabilityOwner.None,
                string.Empty);
        }

        private static InventoryRecord Inventory()
        {
            List<TypeRecord> types = new List<TypeRecord>
            {
                Type(Target, TypeKind.Interface, TargetBase, ExternalBase, SharedExternal),
                Type(TargetBase, TypeKind.Interface),
                Type(Listener, TypeKind.Interface),
                Type(Payload, TypeKind.Class),
                GenericType(GenericDefinition, GenericBase),
                GenericType(Arity2, Arity2Base),
                Type(Arity2Base, TypeKind.Interface),
                Type(GenericBase, TypeKind.Interface),
                GenericType(Nested),
                GenericType(SoloDefinition),
                GenericType(BoxDefinition),
                Type(NotSupported, TypeKind.Interface),
                Type("PEPlugin.SDX.V3", TypeKind.Class),
            };
            List<TypeRecord> referenced = new List<TypeRecord>
            {
                Type(Handler + "<" + Payload + ">", TypeKind.Delegate),
                Type(Handler + "<" + ExternalPayload + ">", TypeKind.Delegate),
                Type(ExternalBase, TypeKind.Interface),
                Type(SharedExternal, TypeKind.Interface),
                Type(ExternalWithoutNamespace, TypeKind.Interface),
                Type(ArgumentOnlyWithoutNamespace, TypeKind.Interface),
                Type(ConcreteNamedLikeParameter, TypeKind.Interface),
                Type(ExternalPayload, TypeKind.Class),
                Type("System.Collections.Generic.IList<System.String>", TypeKind.Interface),
                Type(
                    "System.Collections.Generic.IList<" + ArgumentOnlyWithoutNamespace + ">",
                    TypeKind.Interface),
                Type("System.String", TypeKind.Class),
                Type("System.Int32", TypeKind.Struct),
            };
            List<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Property(Target, "Position", "PEPlugin.SDX.V3"),
                Method(Target, "Clone", "System.Void", Arg("pmd", NotSupported)),
                Method(
                    Target,
                    "Weights",
                    "System.Int32[]",
                    Arg("names", "System.Collections.Generic.IList<System.String>"),
                    RefArg("count", "System.Int32&")),
                Property(TargetBase, "Source", Payload),
                Event(Listener, "MouseMove", Handler + "<" + Payload + ">"),
                Event(Listener, "KeyDown", Handler + "<" + ExternalPayload + ">"),
                Property(NotSupported, "Name", "System.String"),
                Property(SoloDefinition, "Text", "System.String"),
                Constructor(BoxDefinition),
                Property(Listener, "Cancel", SharedExternal),
                Property(Listener, "Global", ExternalWithoutNamespace),
                Property(
                    Listener,
                    "Nested",
                    "PXCPlugin.UIModel.PXOuter<System.String>+PXInner<System.Int32>"),
                Property(Listener, "Pair", Generic + "<System.String,System.Int32>"),
                Property(Listener, "Primary", Generic + "<System.String>"),
                GenericMethod(
                    Listener,
                    "Pick",
                    MethodTypeParameter,
                    "System.Collections.Generic.IList<" + MethodTypeParameter + ">"),
                GenericMethod(
                    Listener,
                    "Share",
                    MethodTypeParameter,
                    "System.Collections.Generic.IList<" + ConcreteNamedLikeParameter + ">"),
                Property(
                    Listener,
                    "Argument",
                    "System.Collections.Generic.IList<" + ArgumentOnlyWithoutNamespace + ">"),
            };

            foreach (string root in TypeRoleEvidence.ConnectionRoots)
            {
                types.Add(Type(root, TypeKind.Interface));
                signatures.Add(Property(root, "Version", "System.String"));
            }

            return new InventoryRecord("PEPlugin", "0.0.0.0", types, referenced, signatures);
        }

        private static TypeRecord GenericType(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, true, true, baseTypes.ToList(), new List<string>());
        }

        private static TypeRecord Type(string name, TypeKind kind, params string[] baseTypes)
        {
            return new TypeRecord(name, kind, false, true, false, baseTypes.ToList(), new List<string>());
        }

        private static ParameterRecord Arg(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.In, false);
        }

        private static ParameterRecord RefArg(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.Ref, false);
        }

        private static SignatureRecord GenericMethod(
            string declaringType, string memberName, string typeParameter, string valueType)
        {
            IList<ParameterRecord> parameters = new ParameterRecord[0];

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 1, parameters, valueType),
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                1,
                parameters,
                valueType,
                false,
                false,
                OperationDirection.Read,
                false,
                new[] { typeParameter });
        }

        private static SignatureRecord Constructor(string declaringType)
        {
            return Signature(
                declaringType,
                MemberKind.Constructor,
                SignatureKeyBuilder.ConstructorName,
                declaringType,
                new ParameterRecord[0],
                false);
        }

        private static SignatureRecord Property(string declaringType, string memberName, string valueType)
        {
            return Signature(
                declaringType, MemberKind.Property, memberName, valueType, new ParameterRecord[0], false);
        }

        private static SignatureRecord Event(string declaringType, string memberName, string handlerType)
        {
            return Signature(
                declaringType, MemberKind.Event, memberName, handlerType, new ParameterRecord[0], false);
        }

        private static SignatureRecord Method(
            string declaringType, string memberName, string valueType, params ParameterRecord[] parameters)
        {
            return Signature(declaringType, MemberKind.Method, memberName, valueType, parameters, false);
        }

        private static SignatureRecord Signature(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            string valueType,
            IList<ParameterRecord> parameters,
            bool valueTypeIsTypeArgument)
        {
            string key = SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType);

            return new SignatureRecord(
                key,
                declaringType,
                memberKind,
                memberName,
                false,
                0,
                parameters,
                valueType,
                true,
                false,
                OperationDirection.Read,
                valueTypeIsTypeArgument);
        }
    }
}
