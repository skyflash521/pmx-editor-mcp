using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>束縛先のスロットの決まり方を固定する。</summary>
    public sealed class CommonAssignmentEvidenceTests
    {
        private const string Owner = "N.IOwner";

        private const string Connector = "N.IConnector";

        private const string Handle = "N.IHandle";

        private const string Root = "PEPlugin.IPERunArgs";

        [Fact]
        public void AReturnOfAConnectionRootIsTheRunArgsClone()
        {
            Assert.Equal(BindingSlot.RunArgsClone, Returned(Method("Get", Root)));
        }

        [Fact]
        public void AReturnOfAConnectorIsTheResidentObject()
        {
            Assert.Equal(BindingSlot.ResidentObject, Returned(Method("Get", Connector)));
        }

        [Fact]
        public void AReturnOfTheCurrentPmxIsThePmxClone()
        {
            Assert.Equal(
                BindingSlot.PmxClone,
                Returned(Method("Get", CommonAssignmentEvidence.CurrentPmxType)));
        }

        [Fact]
        public void AVoidReturnIsNotBound()
        {
            Assert.Null(Returned(Method("Do", "System.Void")));
        }

        [Fact]
        public void AReturnOfAnotherTypeStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Returned(Method("Get", "System.Int32")));

            Assert.Contains("戻り値の型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryParameterTypeTakesItsSlot()
        {
            Dictionary<string, BindingSlot> expected = new Dictionary<string, BindingSlot>
            {
                { TypeRoleEvidence.InjectedConnector, BindingSlot.InjectedConnector },
                { CommonAssignmentEvidence.CurrentPmxType, BindingSlot.PmxClone },
                { CommonAssignmentEvidence.UpdateKindType, BindingSlot.UpdateKind },
                { "System.Int32", BindingSlot.UpdateIndices },
                { "System.Int32[]", BindingSlot.UpdateIndices },
                { "System.Boolean", BindingSlot.UndoLock },
                { Handle, BindingSlot.TargetHandle },
            };
            foreach (KeyValuePair<string, BindingSlot> pair in expected)
            {
                SlotBinding binding = Binding(Method("Do", "System.Void", pair.Key));

                Assert.Equal(pair.Value, binding.Parameters["arg0"]);
            }
        }

        [Fact]
        public void ATextParameterOfASignatureThatReturnsARootIsTheModulePath()
        {
            SlotBinding binding = Binding(Method("Get", Root, "System.String"));

            Assert.Equal(BindingSlot.ModulePath, binding.Parameters["arg0"]);
        }

        [Fact]
        public void ATextParameterOfAnotherSignatureStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Binding(Method("Do", "System.Void", "System.String")));

            Assert.Contains("引数の型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheReceiverOfAReleaseWithoutParametersIsTheTargetHandle()
        {
            Assert.Equal(BindingSlot.TargetHandle, Binding(Method("Release", "System.Void")).Receiver);
            Assert.Equal(BindingSlot.TargetHandle, Binding(Method("Dispose", "System.Void")).Receiver);
        }

        [Fact]
        public void TheReceiverOfAReleaseWithParametersIsTheOwningObject()
        {
            Assert.Equal(
                BindingSlot.OwningObject,
                Binding(Method("ReleaseListener", "System.Void", Handle)).Receiver);
        }

        [Fact]
        public void TheReceiverOfAnotherMemberIsTheOwningObject()
        {
            Assert.Equal(BindingSlot.OwningObject, Binding(Method("Get", Connector)).Receiver);
        }

        [Fact]
        public void AStaticSignatureHasNoReceiver()
        {
            Assert.Null(Binding(Method("Get", Connector, null, true)).Receiver);
        }

        [Fact]
        public void OnlyTheAskedSignaturesAreBound()
        {
            InventoryRecord inventory = Inventory(
                Method("Get", Connector), Method("Other", "System.Int32"));

            IDictionary<string, SlotBinding> bindings = CommonAssignmentEvidence.Bindings(
                inventory, Roles(), Keys(inventory.Signatures[0].Key));

            Assert.Equal(new[] { inventory.Signatures[0].Key }, bindings.Keys);
        }

        [Fact]
        public void AProvidedSignatureThatReturnsAConnectorIsAResidentObject()
        {
            InventoryRecord inventory = Inventory(
                Method("Get", Connector), Method("Do", "System.Void"));

            ISet<string> found = CommonAssignmentEvidence.ResidentObjectSignatures(
                inventory, Roles(), Keys(inventory.Signatures.Select(s => s.Key).ToArray()));

            Assert.Equal(new[] { inventory.Signatures[0].Key }, found);
        }

        [Fact]
        public void ASignatureOutsideTheProvidedIsNotAResidentObject()
        {
            InventoryRecord inventory = Inventory(Method("Get", Connector));

            Assert.Empty(CommonAssignmentEvidence.ResidentObjectSignatures(
                inventory, Roles(), Keys()));
        }

        [Fact]
        public void AParameterThatIsNotAnInputStops()
        {
            SignatureRecord signature = WithDirection(ParameterDirection.Out);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentEvidence.Bindings(
                    Inventory(signature), Roles(), Keys(signature.Key)));

            Assert.Contains("入力でない引数", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AProvidedReleaseIsARelease()
        {
            InventoryRecord inventory = Inventory(
                Method("Release", "System.Void"),
                Method("Dispose", "System.Void"),
                Method("Get", Connector));

            ISet<string> found = CommonAssignmentEvidence.ReleaseSignatures(
                inventory, Keys(inventory.Signatures.Select(s => s.Key).ToArray()));

            Assert.Equal(
                new[] { inventory.Signatures[0].Key, inventory.Signatures[1].Key }
                    .OrderBy(k => k, StringComparer.Ordinal),
                found.OrderBy(k => k, StringComparer.Ordinal));
        }

        [Fact]
        public void AReleaseOutsideTheProvidedIsNotARelease()
        {
            InventoryRecord inventory = Inventory(Method("Release", "System.Void"));

            Assert.Empty(CommonAssignmentEvidence.ReleaseSignatures(inventory, Keys()));
        }

        [Fact]
        public void BothArgumentsOfTheReleasesAreRequired()
        {
            InventoryRecord inventory = Inventory(Method("Release", "System.Void"));

            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.ReleaseSignatures(null, Keys()));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.ReleaseSignatures(inventory, null));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            InventoryRecord inventory = Inventory(Method("Get", Connector));

            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.Bindings(null, Roles(), Keys()));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.Bindings(inventory, null, Keys()));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.Bindings(inventory, Roles(), null));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.ResidentObjectSignatures(null, Roles(), Keys()));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.ResidentObjectSignatures(inventory, null, Keys()));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentEvidence.ResidentObjectSignatures(inventory, Roles(), null));
        }

        private static BindingSlot? Returned(SignatureRecord signature)
        {
            return Binding(signature).Returned;
        }

        private static SlotBinding Binding(SignatureRecord signature)
        {
            InventoryRecord inventory = Inventory(signature);

            return CommonAssignmentEvidence.Bindings(
                inventory, Roles(), Keys(signature.Key))[signature.Key];
        }

        private static ISet<string> Keys(params string[] keys)
        {
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }

        private static IDictionary<string, TypeRole> Roles()
        {
            return new Dictionary<string, TypeRole>(StringComparer.Ordinal)
            {
                { Owner, TypeRole.OperationTarget },
                { Connector, TypeRole.Connector },
                { Handle, TypeRole.HandleTarget },
                { Root, TypeRole.Connector },
            };
        }

        private static SignatureRecord Method(
            string memberName,
            string valueType,
            string parameterType = null,
            bool isStatic = false)
        {
            List<ParameterRecord> parameters = new List<ParameterRecord>();
            if (parameterType != null)
            {
                parameters.Add(
                    new ParameterRecord("arg0", parameterType, ParameterDirection.In, false));
            }

            return new SignatureRecord(
                SignatureKeyBuilder.Build(Owner, memberName, 0, parameters, valueType),
                Owner,
                MemberKind.Method,
                memberName,
                isStatic,
                0,
                parameters,
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        private static SignatureRecord WithDirection(ParameterDirection direction)
        {
            List<ParameterRecord> parameters = new List<ParameterRecord>
            {
                new ParameterRecord("arg0", "System.Int32", direction, false),
            };

            return new SignatureRecord(
                SignatureKeyBuilder.Build(Owner, "Do", 0, parameters, "System.Void"),
                Owner,
                MemberKind.Method,
                "Do",
                false,
                0,
                parameters,
                "System.Void",
                false,
                false,
                OperationDirection.Read);
        }

        private static InventoryRecord Inventory(params SignatureRecord[] signatures)
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord>(),
                new List<TypeRecord>(),
                signatures.ToList());
        }
    }
}
