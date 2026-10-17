using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleGateTests
    {
        private const string Root = "N.IRoot";

        [Fact]
        public void ATableThatMatchesTheEvidencePasses()
        {
            Require(
                Table(
                    Record(Root, TypeRole.Connector),
                    Record("N.IArgs", TypeRole.EventArgs),
                    Record("N.IThing", TypeRole.OperationTarget)),
                Set(Root, "N.IArgs", "N.IThing"),
                Roots(Root),
                Set("N.IArgs"),
                Set(Root));
        }

        [Fact]
        public void ATypeMissingFromTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector)),
                    Set(Root, "N.IOther"),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IOther", error.Message);
        }

        [Fact]
        public void ATypeThatIsNotARoleTypeStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IExtra", TypeRole.Dto)),
                    Set(Root),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IExtra", error.Message);
        }

        [Fact]
        public void TheSameTypeListedTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record(Root, TypeRole.Dto)),
                    Set(Root),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void ARootThatIsNotInTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record("N.IThing", TypeRole.Dto)),
                    Set("N.IThing"),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains(Root, error.Message);
        }

        [Fact]
        public void ARootThatIsGivenAnotherRoleStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Dto)),
                    Set(Root),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("コネクタ型", error.Message);
        }

        [Fact]
        public void ARootAfterTheFirstIsCheckedToo()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.ISecond", TypeRole.Dto)),
                    Set(Root, "N.ISecond"),
                    Roots(Root, "N.ISecond"),
                    Set(),
                    Set(Root, "N.ISecond")));

            Assert.Contains("N.ISecond", error.Message);
        }

        [Fact]
        public void AnEventArgumentThatIsGivenAnotherRoleStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IArgs", TypeRole.Dto)),
                    Set(Root, "N.IArgs"),
                    Roots(Root),
                    Set("N.IArgs"),
                    Set(Root)));

            Assert.Contains("N.IArgs", error.Message);
        }

        [Fact]
        public void ATypeThatIsNotAnEventArgumentCannotTakeThatRole()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.EventArgs)),
                    Set(Root, "N.IThing"),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void EvidenceForATypeOutsideTheTableIsIgnored()
        {
            Require(
                Table(Record(Root, TypeRole.Connector)),
                Set(Root),
                Roots(Root),
                Set("N.IOutside"),
                Set(Root));
        }

        [Fact]
        public void AConnectorTheHostDoesNotHoldStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.Connector)),
                    Set(Root, "N.IThing"),
                    Roots(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void ATypeReachedFromARootMayTakeAnotherRole()
        {
            Require(
                Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.OperationTarget)),
                Set(Root, "N.IThing"),
                Roots(Root),
                Set(),
                Set(Root, "N.IThing"));
        }

        [Fact]
        public void IssuancesThatMatchTheEvidencePass()
        {
            TypeRoleGate.Require(
                Issued(Issuance("N.A.Make()", true)),
                Set(Root),
                Roots(),
                Set(),
                Set(Root),
                Candidates("N.A.Make()", HandleIssuanceKind.Factory),
                Collections());
        }

        [Fact]
        public void AnIssuanceTheEvidenceDoesNotHaveStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(Issuance("N.A.Get()", false)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Candidates(),
                    Collections()));

            Assert.Contains("N.A.Get()", error.Message);
        }

        [Fact]
        public void AnIssuanceTheEvidenceHasButTheTableOmitsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Candidates("N.A.Make()", HandleIssuanceKind.Factory),
                    Collections()));

            Assert.Contains("N.A.Make()", error.Message);
        }

        [Fact]
        public void TheSameIssuanceTwiceInTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(
                        Issuance("N.A.Make()", true),
                        Issuance("N.A.Make()", true)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Candidates("N.A.Make()", HandleIssuanceKind.Factory),
                    Collections()));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void CollectionsThatMatchTheEvidencePass()
        {
            TypeRoleGate.Require(
                Listed(Collection("N.A.Items()", true), Collection("N.B.Refs()", false)),
                Set(Root),
                Roots(),
                Set(),
                Set(Root),
                Issuances(),
                Both());
        }

        [Fact]
        public void ACollectionTheEvidenceDoesNotHaveStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Listed(Collection("N.A.Items()", true)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections()));

            Assert.Contains("N.A.Items()", error.Message);
        }

        [Fact]
        public void ACollectionTheEvidenceHasButTheTableOmitsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Listed(),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections("N.A.Items()", "N.IThing")));

            Assert.Contains("N.A.Items()", error.Message);
        }

        [Fact]
        public void TwoOwningListsOfTheSameElementStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Listed(Collection("N.A.Items()", true), Collection("N.B.Refs()", true)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Both()));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void AReferencingListWithoutAnOwningListStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Listed(Collection("N.B.Refs()", false)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections("N.B.Refs()", "N.IThing")));

            Assert.Contains("N.B.Refs()", error.Message);
        }

        [Fact]
        public void TheSameCollectionTwiceInTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Listed(Collection("N.A.Items()", true), Collection("N.A.Items()", true)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections("N.A.Items()", "N.IThing")));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void ATableThatNamesEveryRootAsAConnectorPassesEvenWhenARootLeadsToNoProvidedType()
        {
            const string Thing = "N.IThing";
            const string RunArgs = "PEPlugin.IPERunArgs";
            List<TypeRecord> types = new List<TypeRecord>
            {
                new TypeRecord(
                    Thing, TypeKind.Interface, false, true, false, new List<string>(), new List<string>()),
            };
            List<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Property(RunArgs, "Thing", Thing),
                Property(Thing, "Name", "System.String"),
            };
            foreach (string root in TypeRoleEvidence.ConnectionRoots)
            {
                types.Add(new TypeRecord(
                    root, TypeKind.Interface, false, true, false, new List<string>(), new List<string>()));
                signatures.Add(Property(root, "Version", "System.String"));
            }

            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                types,
                new List<TypeRecord>
                {
                    new TypeRecord(
                        "System.String",
                        TypeKind.Class,
                        false,
                        true,
                        false,
                        new List<string>(),
                        new List<string>()),
                },
                signatures);
            IList<CapabilityRecord> ledger = new List<CapabilityRecord>
            {
                new CapabilityRecord(
                    "CAP-001",
                    "分類",
                    Thing,
                    CapabilityTargetKind.Single,
                    new List<string> { Thing },
                    CapabilityStatus.Provided,
                    CapabilityOwner.Model,
                    string.Empty),
                NotSupportedPattern("CAP-463", "PEPlugin.Pmd.*"),
                NotSupportedPattern("CAP-466", "PEPlugin.SDX.*"),
            };
            TypeRolePopulation population = TypeRolePopulation.Resolve(
                ledger, inventory, new List<ExcludedSignatureRecord>());
            TypeRoleTable table = Table(TypeRoleEvidence.ConnectionRoots
                .Select(root => Record(root, TypeRole.Connector))
                .Concat(new[] { Record(Thing, TypeRole.OperationTarget) })
                .OrderBy(r => r.TypeName, StringComparer.Ordinal)
                .ToArray());
            IDictionary<string, TypeRole> roles = table.Types.ToDictionary(
                r => r.TypeName, r => r.Role, StringComparer.Ordinal);

            TypeRoleGate.Require(
                table,
                population.RoleTypes,
                TypeRoleEvidence.ConnectionRoots,
                TypeRoleEvidence.EventArgumentTypes(inventory),
                TypeRoleEvidence.ConnectorCandidates(inventory, TypeRoleEvidence.ConnectionRoots),
                HandleIssuanceEvidence.Candidates(inventory, roles, population.Signatures),
                ElementCollectionEvidence.Candidates(inventory, roles, population.Signatures));
        }

        private static SignatureRecord Property(string declaringType, string memberName, string valueType)
        {
            ParameterRecord[] parameters = new ParameterRecord[0];

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType),
                declaringType,
                MemberKind.Property,
                memberName,
                false,
                0,
                parameters,
                valueType,
                true,
                false,
                OperationDirection.Read,
                false);
        }

        private static CapabilityRecord NotSupportedPattern(string id, string target)
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

        [Fact]
        public void EveryArgumentIsRequired()
        {
            TypeRoleTable table = Table(Record(Root, TypeRole.Connector));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    null, Set(Root), Roots(Root), Set(), Set(Root), Issuances(),
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, null, Roots(Root), Set(), Set(Root), Issuances(),
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), null, Set(), Set(Root), Issuances(),
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), null, Set(Root), Issuances(),
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), null, Issuances(),
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), null,
                    Collections()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), Issuances(), null));
        }

        /// <summary>接続の経路を持たない題材のための呼び出し。</summary>
        private static void Require(
            TypeRoleTable records,
            ISet<string> roleTypes,
            IEnumerable<string> connectionRoots,
            ISet<string> eventArgumentTypes,
            ICollection<string> connectorCandidates)
        {
            TypeRoleGate.Require(
                records,
                roleTypes,
                connectionRoots,
                eventArgumentTypes,
                connectorCandidates,
                Issuances(),
                Collections());
        }

        private static TypeRoleTable Table(params TypeRoleRecord[] records)
        {
            return new TypeRoleTable(
                records.ToList(),
                new List<HandleIssuanceRecord>(),
                new List<ElementCollectionRecord>());
        }

        private static IDictionary<string, string> Both()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "N.A.Items()", "N.IThing" },
                { "N.B.Refs()", "N.IThing" },
            };
        }

        private static IDictionary<string, string> Collections(
            string signatureKey = null, string elementType = null)
        {
            Dictionary<string, string> candidates =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (signatureKey != null)
            {
                candidates.Add(signatureKey, elementType);
            }

            return candidates;
        }

        private static TypeRoleTable Listed(params ElementCollectionRecord[] collections)
        {
            return new TypeRoleTable(
                new List<TypeRoleRecord> { Record(Root, TypeRole.Connector) },
                new List<HandleIssuanceRecord>(),
                collections.ToList());
        }

        private static ElementCollectionRecord Collection(string signatureKey, bool owns)
        {
            return new ElementCollectionRecord(
                signatureKey,
                owns,
                signatureKey + " の根拠。",
                owns ? new List<string> { signatureKey } : null);
        }

        private static TypeRoleTable Issued(params HandleIssuanceRecord[] issuances)
        {
            return new TypeRoleTable(
                new List<TypeRoleRecord> { Record(Root, TypeRole.Connector) },
                issuances.ToList(),
                new List<ElementCollectionRecord>());
        }

        private static HandleIssuanceRecord Issuance(string signatureKey, bool issues)
        {
            return new HandleIssuanceRecord(signatureKey, issues, signatureKey + " の根拠。");
        }

        private static IDictionary<string, HandleIssuanceKind> Candidates(
            string signatureKey = null, HandleIssuanceKind kind = HandleIssuanceKind.Factory)
        {
            Dictionary<string, HandleIssuanceKind> candidates =
                new Dictionary<string, HandleIssuanceKind>(StringComparer.Ordinal);
            if (signatureKey != null)
            {
                candidates.Add(signatureKey, kind);
            }

            return candidates;
        }

        private static IDictionary<string, HandleIssuanceKind> Issuances()
        {
            return new Dictionary<string, HandleIssuanceKind>(StringComparer.Ordinal);
        }

        private static IEnumerable<string> Roots(params string[] names)
        {
            return names;
        }

        private static TypeRoleRecord Record(
            string typeName,
            TypeRole role,
            CapabilityOwner group = CapabilityOwner.Model)
        {
            if (!TypeRoleRecord.HasIndependentTool(role))
            {
                return new TypeRoleRecord(typeName, role, typeName + " の根拠。");
            }

            return new TypeRoleRecord(
                typeName,
                role,
                typeName + " の根拠。",
                Singular,
                role == TypeRole.Connector ? string.Empty : Plural,
                group);
        }

        private const string Singular = "thing";

        private const string Plural = "things";

        private static ISet<string> Set(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.Ordinal);
        }
    }
}
