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
                Paths(Root, "Host.Connector"));
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
                    Paths(Root, "Host.Connector")));

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
                    Paths(Root, "Host.Connector")));

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
                    Paths()));

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
                    Paths(Root, string.Empty)));

            Assert.Contains(Root, error.Message);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            IList<TypeRoleRecord> table = Table(Record(Root, TypeRole.Connector));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(null, Set(Root), Roots(Root), Set(), Set(Root), Paths()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, null, Roots(Root), Set(), Set(Root), Paths()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), null, Set(), Set(Root), Paths()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), null, Set(Root), Paths()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), Set(), null, Paths()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Roots(Root), Set(), Set(Root), null));
        }

        /// <summary>接続の経路を持たない題材のための呼び出し。</summary>
        private static void Require(
            IList<TypeRoleRecord> records,
            ISet<string> roleTypes,
            IEnumerable<string> connectionRoots,
            ISet<string> eventArgumentTypes,
            ICollection<string> connectorCandidates)
        {
            TypeRoleGate.Require(
                records, roleTypes, connectionRoots, eventArgumentTypes, connectorCandidates, Paths());
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

        private static IList<TypeRoleRecord> Table(params TypeRoleRecord[] records)
        {
            return records.ToList();
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
