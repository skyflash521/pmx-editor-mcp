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
            TypeRoleGate.Require(
                Table(
                    Record(Root, TypeRole.Connector),
                    Record("N.IArgs", TypeRole.EventArgs),
                    Record("N.IThing", TypeRole.OperationTarget)),
                Set(Root, "N.IArgs", "N.IThing"),
                Set("N.IArgs"),
                Set(Root));
        }

        [Fact]
        public void ATypeMissingFromTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector)),
                    Set(Root, "N.IOther"),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IOther", error.Message);
        }

        [Fact]
        public void ATypeThatIsNotARoleTypeStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IExtra", TypeRole.Dto)),
                    Set(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IExtra", error.Message);
        }

        [Fact]
        public void TheSameTypeListedTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector), Record(Root, TypeRole.Dto)),
                    Set(Root),
                    Set(),
                    Set(Root)));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void ARootThatIsNotInTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    new TypeRoleTable(
                        new[] { Root },
                        new List<TypeRoleRecord> { Record("N.IThing", TypeRole.Dto) }),
                    Set("N.IThing"),
                    Set(),
                    Set(Root)));

            Assert.Contains(Root, error.Message);
        }

        [Fact]
        public void ARootThatIsGivenAnotherRoleStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Dto)), Set(Root), Set(), Set(Root)));

            Assert.Contains("コネクタ型", error.Message);
        }

        [Fact]
        public void ARootAfterTheFirstIsCheckedToo()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    new TypeRoleTable(
                        new[] { Root, "N.ISecond" },
                        new List<TypeRoleRecord>
                        {
                            Record(Root, TypeRole.Connector),
                            Record("N.ISecond", TypeRole.Dto),
                        }),
                    Set(Root, "N.ISecond"),
                    Set(),
                    Set(Root, "N.ISecond")));

            Assert.Contains("N.ISecond", error.Message);
        }

        [Fact]
        public void AnEventArgumentThatIsGivenAnotherRoleStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IArgs", TypeRole.Dto)),
                    Set(Root, "N.IArgs"),
                    Set("N.IArgs"),
                    Set(Root)));

            Assert.Contains("N.IArgs", error.Message);
        }

        [Fact]
        public void ATypeThatIsNotAnEventArgumentCannotTakeThatRole()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.EventArgs)),
                    Set(Root, "N.IThing"),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void EvidenceForATypeOutsideTheTableIsIgnored()
        {
            TypeRoleGate.Require(
                Table(Record(Root, TypeRole.Connector)),
                Set(Root),
                Set("N.IOutside"),
                Set(Root));
        }

        [Fact]
        public void AConnectorThatIsNotReachableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleGate.Require(
                    Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.Connector)),
                    Set(Root, "N.IThing"),
                    Set(),
                    Set(Root)));

            Assert.Contains("N.IThing", error.Message);
        }

        [Fact]
        public void AReachableTypeMayTakeAnotherRole()
        {
            TypeRoleGate.Require(
                Table(Record(Root, TypeRole.Connector), Record("N.IThing", TypeRole.OperationTarget)),
                Set(Root, "N.IThing"),
                Set(),
                Set(Root, "N.IThing"));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            TypeRoleTable table = Table(Record(Root, TypeRole.Connector));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(null, Set(Root), Set(), Set(Root)));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, null, Set(), Set(Root)));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), null, Set(Root)));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleGate.Require(table, Set(Root), Set(), null));
        }

        private static TypeRoleTable Table(params TypeRoleRecord[] records)
        {
            return new TypeRoleTable(new[] { Root }, records.ToList());
        }

        private static TypeRoleRecord Record(string typeName, TypeRole role)
        {
            return new TypeRoleRecord(typeName, role, typeName + " の根拠。");
        }

        private static ISet<string> Set(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.Ordinal);
        }
    }
}
