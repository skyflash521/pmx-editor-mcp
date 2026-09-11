using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>所有するリストの要素が持つ、追加と削除のツールの名前。</summary>
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
        public void TheRowKeyLeadsToTheElementTypeOfThatList()
        {
            KeyValuePair<string, TypeRoleRecord> found = Assert.Single(
                ElementToolRule.Elements(Map(ListKey), Signatures(), Roles(true)));

            Assert.Equal(ListKey, found.Key);
            Assert.Equal(Element, found.Value.TypeName);
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
