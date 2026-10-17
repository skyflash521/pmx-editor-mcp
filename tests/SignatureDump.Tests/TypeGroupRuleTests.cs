using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeGroupRuleTests
    {
        private const string Type = "N.IThing";

        [Fact]
        public void TheGroupOfATypeTheLedgerDecidesComesFromTheLedger()
        {
            TypeRoleTable resolved = TypeGroupRule.Resolve(
                Table(Record(CapabilityOwner.None)), Owners(CapabilityOwner.View));

            Assert.Equal(CapabilityOwner.View, Assert.Single(resolved.Types).Group);
        }

        [Fact]
        public void AWrittenGroupOnATypeTheLedgerDecidesStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeGroupRule.Resolve(
                    Table(Record(CapabilityOwner.Model)), Owners(CapabilityOwner.View)));

            Assert.Contains("担当群を書かない", error.Message, StringComparison.Ordinal);
            Assert.Contains(Type, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheGroupOfATypeTheLedgerDoesNotDecideIsTheWrittenOne()
        {
            foreach (IDictionary<string, ISet<CapabilityOwner>> owners in new[]
            {
                Owners(CapabilityOwner.View, CapabilityOwner.Session),
                Owners(),
                new Dictionary<string, ISet<CapabilityOwner>>(StringComparer.Ordinal),
            })
            {
                TypeRoleTable resolved = TypeGroupRule.Resolve(
                    Table(Record(CapabilityOwner.Model)), owners);

                Assert.Equal(CapabilityOwner.Model, Assert.Single(resolved.Types).Group);
            }
        }

        [Fact]
        public void AMissingGroupOnATypeTheLedgerDoesNotDecideStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeGroupRule.Resolve(Table(Record(CapabilityOwner.None)), Owners()));

            Assert.Contains("担当群を書く", error.Message, StringComparison.Ordinal);
            Assert.Contains(Type, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARoleWithoutAnIndependentToolIsLeftAsItIs()
        {
            TypeRoleTable resolved = TypeGroupRule.Resolve(
                Table(new TypeRoleRecord(Type, TypeRole.Dto, "根拠。")),
                Owners(CapabilityOwner.View));

            Assert.Equal(CapabilityOwner.None, Assert.Single(resolved.Types).Group);
        }

        [Fact]
        public void TheIssuancesAndTheCollectionsAreCarriedOver()
        {
            TypeRoleTable resolved = TypeGroupRule.Resolve(
                new TypeRoleTable(
                    new[] { Record(CapabilityOwner.None) },
                    new[] { new HandleIssuanceRecord("N.A.Make()", true, "根拠。") },
                    new[] { new ElementCollectionRecord("N.A.Items()", false, "根拠。") }),
                Owners(CapabilityOwner.View));

            Assert.Equal("N.A.Make()", Assert.Single(resolved.Issuances).SignatureKey);
            Assert.Equal("N.A.Items()", Assert.Single(resolved.Collections).SignatureKey);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => TypeGroupRule.Resolve(null, Owners()));
            Assert.Throws<ArgumentNullException>(
                () => TypeGroupRule.Resolve(Table(Record(CapabilityOwner.Model)), null));
        }

        private static TypeRoleRecord Record(CapabilityOwner group)
        {
            return new TypeRoleRecord(
                Type, TypeRole.OperationTarget, "根拠。", "thing", "things", group);
        }

        private static TypeRoleTable Table(TypeRoleRecord record)
        {
            return new TypeRoleTable(
                new[] { record },
                new HandleIssuanceRecord[0],
                new ElementCollectionRecord[0]);
        }

        private static IDictionary<string, ISet<CapabilityOwner>> Owners(
            params CapabilityOwner[] owners)
        {
            return new Dictionary<string, ISet<CapabilityOwner>>(StringComparer.Ordinal)
            {
                { Type, new HashSet<CapabilityOwner>(owners) },
            };
        }
    }
}
