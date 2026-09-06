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
                Collections(),
                Groups());
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
                    Collections(),
                    Groups()));

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
                    Collections(),
                    Groups()));

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
                    Collections(),
                    Groups()));

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
                Both(),
                Groups());
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
                    Collections(),
                    Groups()));

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
                    Collections("N.A.Items()", "N.IThing"),
                    Groups()));

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
                    Both(),
                    Groups()));

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
                    Collections("N.B.Refs()", "N.IThing"),
                    Groups()));

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
                    Collections("N.A.Items()", "N.IThing"),
                    Groups()));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            TypeRoleTable table = Table(Record(Root, TypeRole.Connector));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    null, Set(Root), Roots(Root), Set(), Set(Root), Issuances(),
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, null, Roots(Root), Set(), Set(Root), Issuances(),
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), null, Set(), Set(Root), Issuances(),
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), null, Set(Root), Issuances(),
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), null, Issuances(),
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), null,
                    Collections(), Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), Issuances(), null,
                    Groups()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), Issuances(),
                    Collections(), null));
        }

        [Fact]
        public void AGroupThatMatchesTheOnlyOwnerInTheLedgerPasses()
        {
            TypeRoleGate.Require(
                Table(Record(Root, TypeRole.Connector, CapabilityOwner.View)),
                Set(Root),
                Roots(),
                Set(),
                Set(Root),
                Issuances(),
                Collections(),
                Groups(Root, CapabilityOwner.View));
        }

        [Fact]
        public void AGroupThatDiffersFromTheOnlyOwnerInTheLedgerStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector, CapabilityOwner.Model)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections(),
                    Groups(Root, CapabilityOwner.View)));

            Assert.Contains(Root, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AGroupIsTheAuthorsWhereTheLedgerDoesNotDecideOne()
        {
            foreach (IDictionary<string, ISet<CapabilityOwner>> ledger in new[]
            {
                Groups(Root, CapabilityOwner.View, CapabilityOwner.Session),
                Groups(Root),
                Groups(),
            })
            {
                TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector, CapabilityOwner.Model)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Issuances(),
                    Collections(),
                    ledger);
            }
        }

        [Fact]
        public void ARoleWithoutAnIndependentToolIsNotCheckedAgainstTheLedger()
        {
            TypeRoleGate.Require(
                Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.Dto)),
                Set(Root, "N.IThing"),
                Roots(),
                Set(),
                Set(Root),
                Issuances(),
                Collections(),
                Groups("N.IThing", CapabilityOwner.View));
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
                Collections(),
                Groups());
        }

        private static IDictionary<string, ISet<CapabilityOwner>> Groups(
            string typeName = null, params CapabilityOwner[] owners)
        {
            Dictionary<string, ISet<CapabilityOwner>> ledger =
                new Dictionary<string, ISet<CapabilityOwner>>(StringComparer.Ordinal);
            if (typeName != null)
            {
                ledger.Add(typeName, new HashSet<CapabilityOwner>(owners));
            }

            return ledger;
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
