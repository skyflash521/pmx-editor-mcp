using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>ハンドルを新しく発行しうるシグネチャの母集合と、その種別の決まり方を固定する。</summary>
    public sealed class HandleIssuanceEvidenceTests
    {
        private const string Handle = "N.IHandle";

        private const string Connector = "N.IConnector";

        [Fact]
        public void APublicConstructorOfAHandleTargetIsAConstructor()
        {
            SignatureRecord signature = Constructor(Handle);

            Assert.Equal(
                HandleIssuanceKind.Constructor, Only(Candidates(signature), signature));
        }

        [Fact]
        public void AMethodOfAConnectorIsAFactory()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle);

            Assert.Equal(HandleIssuanceKind.Factory, Only(Candidates(signature), signature));
        }

        [Fact]
        public void AStaticMethodOfAConnectorIsStillAFactory()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle, true);

            Assert.Equal(HandleIssuanceKind.Factory, Only(Candidates(signature), signature));
        }

        [Fact]
        public void AMethodOfAHandleTargetIsReceiverBound()
        {
            SignatureRecord signature = Method(Handle, "Derive", Handle);

            Assert.Equal(HandleIssuanceKind.ReceiverBound, Only(Candidates(signature), signature));
        }

        [Fact]
        public void AnArrayOfHandlesIsAlsoACandidate()
        {
            SignatureRecord signature = Method(Connector, "MakeAll", Handle + "[]");

            Assert.Equal(HandleIssuanceKind.Factory, Only(Candidates(signature), signature));
        }

        [Fact]
        public void APropertyIsNotACandidate()
        {
            Assert.Empty(Candidates(Property(Connector, "Current", Handle)));
        }

        [Fact]
        public void AMethodThatDoesNotReturnAHandleIsNotACandidate()
        {
            Assert.Empty(Candidates(Method(Connector, "Count", "System.Int32")));
        }

        [Fact]
        public void AMethodOfATypeOutsideTheTableIsNotACandidate()
        {
            Assert.Empty(Candidates(Method("N.IOutside", "Make", Handle)));
        }

        [Fact]
        public void ASignatureThatIsNotProvidedIsNotACandidate()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle);

            Assert.Empty(HandleIssuanceEvidence.Candidates(
                Inventory(signature), Roles(), new HashSet<string>(StringComparer.Ordinal)));
        }

        [Fact]
        public void AConstructorOfATypeThatIsNotAHandleTargetIsNotACandidate()
        {
            Assert.Empty(Candidates(Constructor(Connector)));
        }

        [Fact]
        public void AReceiverThatTakesNoKindStops()
        {
            SignatureRecord signature = Method("N.IThing", "Make", Handle);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Candidates(signature));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void AStaticMethodOfAHandleTargetStops()
        {
            SignatureRecord signature = Method(Handle, "Make", Handle, true);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Candidates(signature));

            Assert.Contains(Handle, error.Message);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            InventoryRecord inventory = Inventory(Method(Connector, "Make", Handle));

            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Candidates(null, Roles(), Provided()));
            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Candidates(inventory, null, Provided()));
            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Candidates(inventory, Roles(), null));
        }

        private static HandleIssuanceKind Only(
            IDictionary<string, HandleIssuanceKind> candidates, SignatureRecord signature)
        {
            return candidates[Assert.Single(candidates.Keys, k => k == signature.Key)];
        }

        private static IDictionary<string, HandleIssuanceKind> Candidates(
            params SignatureRecord[] signatures)
        {
            return HandleIssuanceEvidence.Candidates(
                Inventory(signatures),
                Roles(),
                new HashSet<string>(signatures.Select(s => s.Key), StringComparer.Ordinal));
        }

        private static ISet<string> Provided()
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        private static IDictionary<string, TypeRole> Roles()
        {
            return new Dictionary<string, TypeRole>(StringComparer.Ordinal)
            {
                { Handle, TypeRole.HandleTarget },
                { Connector, TypeRole.Connector },
                { "N.IThing", TypeRole.OperationTarget },
            };
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

        private static SignatureRecord Constructor(string declaringType)
        {
            return Signature(declaringType, MemberKind.Constructor, ".ctor", declaringType, false);
        }

        private static SignatureRecord Method(
            string declaringType, string memberName, string valueType, bool isStatic = false)
        {
            return Signature(declaringType, MemberKind.Method, memberName, valueType, isStatic);
        }

        private static SignatureRecord Property(
            string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Property, memberName, valueType, false);
        }

        private static SignatureRecord Signature(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            string valueType,
            bool isStatic)
        {
            ParameterRecord[] parameters = new ParameterRecord[0];

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType),
                declaringType,
                memberKind,
                memberName,
                isStatic,
                0,
                parameters,
                valueType,
                true,
                false,
                OperationDirection.Read,
                false);
        }
    }
}
