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

        [Fact]
        public void ARowThatCreatesBringsTheTypeItReturns()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle);

            Assert.Equal(new[] { Handle }, Made(signature).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void ARowThatCreatesManyBringsTheTypeInside()
        {
            SignatureRecord signature = Method(
                Connector, "MakeAll", "System.Collections.Generic.IList<" + Handle + ">");

            Assert.Equal(new[] { Handle }, Made(signature).ToArray());
        }

        [Fact]
        public void ARowThatCreatesNothingBringsNoType()
        {
            Assert.Empty(HandleIssuanceEvidence.Made(
                Map(Reading(Method(Connector, "Make", Handle))),
                Signatures(Method(Connector, "Make", Handle)),
                Branches()));
        }

        /// <summary>
        /// 枝の型を作れるなら、その枝を並びへ入れれば抽象の型としても指せる。作れるものの数え方が
        /// 枝だけを見ていると、抽象の型が作れないものとして数えられてしまう。
        /// </summary>
        [Fact]
        public void AnAbstractTypeWhoseBranchIsMadeIsMadeToo()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle);
            IDictionary<string, IList<string>> branches = Branches();
            branches.Add("N.IBase", new List<string> { Handle });

            Assert.Equal(
                new[] { "N.IBase", Handle },
                HandleIssuanceEvidence.Made(
                        Map(Creating(signature)), Signatures(signature), branches)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray());
        }

        [Fact]
        public void EveryArgumentOfMadeIsRequired()
        {
            SignatureRecord signature = Method(Connector, "Make", Handle);
            ToolMap map = Map(Creating(signature));
            IDictionary<string, SignatureRecord> signatures = Signatures(signature);

            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Made(null, signatures, Branches()));
            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Made(map, null, Branches()));
            Assert.Throws<ArgumentNullException>(
                () => HandleIssuanceEvidence.Made(map, signatures, null));
        }

        private static ISet<string> Made(SignatureRecord signature)
        {
            return HandleIssuanceEvidence.Made(
                Map(Creating(signature)), Signatures(signature), Branches());
        }

        private static IDictionary<string, IList<string>> Branches()
        {
            return new Dictionary<string, IList<string>>(StringComparer.Ordinal);
        }

        private static ToolMap Map(params ToolMapRow[] rows)
        {
            return new ToolMap(rows.ToList());
        }

        private static ToolMapRow Creating(SignatureRecord signature)
        {
            return new ToolMapRow(
                signature.Key,
                ToolMapEditKind.Read,
                null,
                "題材の根拠。",
                new List<Postcondition>
                {
                    new Postcondition(
                        EffectType.HandleCreated,
                        string.Empty,
                        EffectCheckKind.Handle,
                        "session_release_handle",
                        null,
                        null,
                        EffectComparison.AnyChanged,
                        null,
                        false,
                        null),
                },
                null,
                null);
        }

        private static ToolMapRow Reading(SignatureRecord signature)
        {
            return new ToolMapRow(
                signature.Key, ToolMapEditKind.Read, null, "題材の根拠。", null, null, null);
        }

        private static IDictionary<string, SignatureRecord> Signatures(
            params SignatureRecord[] signatures)
        {
            return signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
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
