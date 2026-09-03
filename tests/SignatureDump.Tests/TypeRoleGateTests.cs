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
        public void AConnectionPathThatMatchesTheEvidencePasses()
        {
            TypeRoleGate.Require(
                Table(Connector(Root, "Host.Connector")),
                Set(Root),
                Roots(),
                Set(),
                Set(Root),
                Paths(Root, "Host.Connector"),
                Issuances());
        }

        [Fact]
        public void AConnectionPathThatDiffersFromTheEvidenceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Connector(Root, "Host.Other")),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(Root, "Host.Connector"),
                    Issuances()));

            Assert.Contains("Host.Other", error.Message);
            Assert.Contains("Host.Connector", error.Message);
        }

        [Fact]
        public void AConnectorThatOmitsThePathTheEvidenceHasStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(Root, "Host.Connector"),
                    Issuances()));

            Assert.Contains("無し", error.Message);
        }

        [Fact]
        public void AConnectorWithAPathTheEvidenceDoesNotHaveStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Connector(Root, "Host.Connector")),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(),
                    Issuances()));

            Assert.Contains("無し", error.Message);
        }

        [Fact]
        public void ARootThatCarriesAPathStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Connector(Root, "Host")),
                    Set(Root),
                    Roots(Root),
                    Set(),
                    Set(Root),
                    Paths(Root, string.Empty),
                    Issuances()));

            Assert.Contains(Root, error.Message);
        }

        [Fact]
        public void IssuancesThatMatchTheEvidencePass()
        {
            TypeRoleGate.Require(
                Issued(Issuance("N.A.Make()", true, HandleIssuanceKind.Factory)),
                Set(Root),
                Roots(),
                Set(),
                Set(Root),
                Paths(),
                Candidates("N.A.Make()", HandleIssuanceKind.Factory));
        }

        [Fact]
        public void AnIssuanceTheEvidenceDoesNotHaveStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(Issuance("N.A.Get()", false, null)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(),
                    Candidates()));

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
                    Paths(),
                    Candidates("N.A.Make()", HandleIssuanceKind.Factory)));

            Assert.Contains("N.A.Make()", error.Message);
        }

        [Fact]
        public void AKindThatDiffersFromTheReceiverStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(Issuance("N.A.Make()", true, HandleIssuanceKind.ReceiverBound)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(),
                    Candidates("N.A.Make()", HandleIssuanceKind.Factory)));

            Assert.Contains("Factory", error.Message);
        }

        [Fact]
        public void TheSameIssuanceTwiceInTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Issued(
                        Issuance("N.A.Make()", true, HandleIssuanceKind.Factory),
                        Issuance("N.A.Make()", true, HandleIssuanceKind.Factory)),
                    Set(Root),
                    Roots(),
                    Set(),
                    Set(Root),
                    Paths(),
                    Candidates("N.A.Make()", HandleIssuanceKind.Factory)));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            TypeRoleTable table = Table(Record(Root, TypeRole.Connector));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(null, Set(Root), Roots(Root), Set(), Set(Root), Paths(), Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, null, Roots(Root), Set(), Set(Root), Paths(), Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), null, Set(), Set(Root), Paths(), Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), null, Set(Root), Paths(), Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), Set(), null, Paths(), Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), Set(), Set(Root), null, Issuances()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(
                    table, Set(Root), Roots(Root), Set(), Set(Root), Paths(), null));
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
                Paths(),
                Issuances());
        }

        private static IDictionary<string, string> Paths(string typeName = null, string path = null)
        {
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
            if (typeName != null)
            {
                paths.Add(typeName, path);
            }

            return paths;
        }

        private static TypeRoleTable Table(params TypeRoleRecord[] records)
        {
            return new TypeRoleTable(records.ToList(), new List<HandleIssuanceRecord>());
        }

        private static TypeRoleTable Issued(params HandleIssuanceRecord[] issuances)
        {
            return new TypeRoleTable(
                new List<TypeRoleRecord> { Record(Root, TypeRole.Connector) }, issuances.ToList());
        }

        private static HandleIssuanceRecord Issuance(
            string signatureKey, bool issues, HandleIssuanceKind? kind)
        {
            return new HandleIssuanceRecord(signatureKey, issues, kind, signatureKey + " の根拠。");
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

        private static TypeRoleRecord Record(string typeName, TypeRole role)
        {
            return new TypeRoleRecord(typeName, role, typeName + " の根拠。");
        }

        private static TypeRoleRecord Connector(string typeName, string connectionPath)
        {
            return new TypeRoleRecord(
                typeName, TypeRole.Connector, typeName + " の根拠。", "thing", string.Empty, connectionPath);
        }

        private static ISet<string> Set(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.Ordinal);
        }
    }
}
