using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 所有するリストの要素が持つ、追加と削除と、在る要素をハンドルで指すツールの名前。
    /// </summary>
    public sealed class ElementToolRuleTests
    {
        private const string Owner = "PEPlugin.Pmx.IPXPmx";

        private const string ListKey = Owner + ".Vertex()";

        private const string Element = "PEPlugin.Pmx.IPXVertex";

        [Fact]
        public void TheElementTypeTakesAddingAndRemoving()
        {
            Assert.Equal(
                new[] { "model_add_vertices", "model_remove_vertices" },
                ElementToolRule.Of(Role(Element, "vertex", "vertices")).ToArray());
        }

        [Fact]
        public void AnOwningListOnTheTableBringsBothNames()
        {
            Assert.Equal(
                new[] { "model_add_vertices", "model_remove_vertices" },
                ElementToolRule.Names(Map(ListKey), Signatures(), Roles(true))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray());
        }

        [Fact]
        public void AListThatOnlyPointsAtElementsBringsNoName()
        {
            Assert.Empty(ElementToolRule.Names(Map(ListKey), Signatures(), Roles(false)));
        }

        [Fact]
        public void AListWithoutARowBringsNoName()
        {
            Assert.Empty(ElementToolRule.Names(Map(), Signatures(), Roles(true)));
        }

        [Fact]
        public void TheElementTypeTakesHoldingByOneName()
        {
            Assert.Equal(
                "model_hold_vertex",
                ElementToolRule.Holding(Role(Element, "vertex", "vertices")));
        }

        [Fact]
        public void AnElementTypeNothingMakesBringsTheHoldingName()
        {
            Assert.Equal(
                new[] { "model_hold_vertex" },
                ElementToolRule.HoldingNames(
                        Map(ListKey), Signatures(), Roles(true), Nothing(), Issued())
                    .ToArray());
        }

        [Fact]
        public void AnElementTypeSomethingMakesBringsNoHoldingName()
        {
            Assert.Empty(ElementToolRule.HoldingNames(
                Map(ListKey),
                Signatures(),
                Roles(true),
                new HashSet<string>(new[] { Element }, StringComparer.Ordinal),
                Issued()));
        }

        /// <summary>
        /// 親をハンドルで指せない道では、位置で辿った相手が複製になる。その中の要素を預けても
        /// 書き換えが元のモデルへ届かないので、名前を立てても呼べないツールになる。
        /// </summary>
        [Fact]
        public void AListWhoseOwnerTakesNoHandleBringsNoHoldingName()
        {
            Assert.Empty(ElementToolRule.HoldingNames(
                Map(ListKey), Signatures(), Roles(true), Nothing(), Nothing()));
        }

        [Fact]
        public void AListThatOnlyPointsAtElementsBringsNoHoldingName()
        {
            Assert.Empty(ElementToolRule.HoldingNames(
                Map(ListKey), Signatures(), Roles(false), Nothing(), Issued()));
        }

        [Fact]
        public void TheElementOfHoldingIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ElementToolRule.Holding(null));
        }

        [Fact]
        public void EveryArgumentOfHoldingNamesIsRequired()
        {
            ToolMap map = Map(ListKey);
            IDictionary<string, SignatureRecord> signatures = Signatures();
            TypeRoleTable roles = Roles(true);

            Assert.Throws<ArgumentNullException>(
                () => ElementToolRule.HoldingNames(null, signatures, roles, Nothing(), Issued()));
            Assert.Throws<ArgumentNullException>(
                () => ElementToolRule.HoldingNames(map, null, roles, Nothing(), Issued()));
            Assert.Throws<ArgumentNullException>(
                () => ElementToolRule.HoldingNames(map, signatures, null, Nothing(), Issued()));
            Assert.Throws<ArgumentNullException>(
                () => ElementToolRule.HoldingNames(map, signatures, roles, null, Issued()));
            Assert.Throws<ArgumentNullException>(
                () => ElementToolRule.HoldingNames(map, signatures, roles, Nothing(), null));
        }

        [Fact]
        public void TheRowKeyLeadsToTheElementTypeThatTakesHolding()
        {
            KeyValuePair<string, TypeRoleRecord> found = Assert.Single(
                ElementToolRule.Holdings(
                    Map(ListKey), Signatures(), Roles(true), Nothing(), Issued()));

            Assert.Equal(ListKey, found.Key);
            Assert.Equal(Element, found.Value.TypeName);
        }

        [Fact]
        public void ARowWhoseElementIsMadeLeadsToNoHolding()
        {
            Assert.Empty(ElementToolRule.Holdings(
                Map(ListKey),
                Signatures(),
                Roles(true),
                new HashSet<string>(new[] { Element }, StringComparer.Ordinal),
                Issued()));
        }

        [Fact]
        public void TheMadeAndTheIssuedOfHoldingsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ElementToolRule.Holdings(
                Map(ListKey), Signatures(), Roles(true), null, Issued()));
            Assert.Throws<ArgumentNullException>(() => ElementToolRule.Holdings(
                Map(ListKey), Signatures(), Roles(true), Nothing(), null));
        }

        [Fact]
        public void TheRowKeyLeadsToTheElementTypeOfThatList()
        {
            KeyValuePair<string, TypeRoleRecord> found = Assert.Single(
                ElementToolRule.Elements(Map(ListKey), Signatures(), Roles(true)));

            Assert.Equal(ListKey, found.Key);
            Assert.Equal(Element, found.Value.TypeName);
        }

        /// <summary>何も持たない名前の集まり。</summary>
        private static ISet<string> Nothing()
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>ハンドルが出る型。このリストを持つ型が入っている。</summary>
        private static ISet<string> Issued()
        {
            return new HashSet<string>(new[] { Owner }, StringComparer.Ordinal);
        }

        private static TypeRoleRecord Role(string typeName, string noun, string plural)
        {
            return new TypeRoleRecord(
                typeName,
                TypeRole.OperationTarget,
                "題材の根拠。",
                noun,
                plural,
                CapabilityOwner.Model);
        }

        private static TypeRoleTable Roles(bool owns)
        {
            return new TypeRoleTable(
                new[] { Role(Owner, "pmx", "pmxes"), Role(Element, "vertex", "vertices") },
                new HandleIssuanceRecord[0],
                new[]
                {
                    owns
                        ? new ElementCollectionRecord(
                            ListKey, true, "題材の根拠。", new[] { ListKey })
                        : new ElementCollectionRecord(ListKey, false, "題材の根拠。"),
                });
        }

        private static ToolMap Map(params string[] keys)
        {
            return new ToolMap(
                keys.Select(k => new ToolMapRow(
                    k, ToolMapEditKind.Read, null, "題材の根拠。", null, null, null)).ToList());
        }

        private static IDictionary<string, SignatureRecord> Signatures()
        {
            return new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
            {
                {
                    ListKey,
                    new SignatureRecord(
                        ListKey,
                        Owner,
                        MemberKind.Property,
                        "Vertex",
                        false,
                        0,
                        new ParameterRecord[0],
                        "System.Collections.Generic.IList<" + Element + ">",
                        true,
                        false,
                        OperationDirection.Read)
                },
            };
        }
    }
}
