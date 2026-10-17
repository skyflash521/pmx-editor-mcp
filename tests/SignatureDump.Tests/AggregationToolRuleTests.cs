using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>プロパティを集めるツールの名前。行を持たないので役割から決まる。</summary>
    public sealed class AggregationToolRuleTests
    {
        [Fact]
        public void AConnectorCollectsItsItemsIntoGettingAndUpdating()
        {
            Assert.Equal(
                new[] { "session_get_form_connector", "session_update_form_connector" },
                AggregationToolRule.Of(Role(TypeRole.Connector, "form_connector", string.Empty))
                    .ToArray());
        }

        [Fact]
        public void AnOperationTargetCollectsItsItemsIntoListingAndUpdating()
        {
            Assert.Equal(
                new[] { "session_list_vertices", "session_update_vertices" },
                AggregationToolRule.Of(Role(TypeRole.OperationTarget, "vertex", "vertices"))
                    .ToArray());
        }

        [Fact]
        public void ATypeWithoutAGroupCollectsNothing()
        {
            Assert.Empty(AggregationToolRule.Names(
                new[]
                {
                    new TypeRoleRecord(
                        "Sdk.Args", TypeRole.EventArgs, "題材の根拠。", "args", "argses"),
                }));
        }

        [Fact]
        public void ATypeWithoutAnElementNounCollectsNothing()
        {
            Assert.Empty(AggregationToolRule.Names(
                new[] { new TypeRoleRecord("Sdk.Plain", TypeRole.Dto, "題材の根拠。") }));
        }

        [Fact]
        public void NamesGathersEveryTypeThatCollects()
        {
            Assert.Equal(
                new[]
                {
                    "session_get_form_connector", "session_list_vertices",
                    "session_update_form_connector", "session_update_vertices",
                },
                AggregationToolRule.Names(
                    new[]
                    {
                        Role(TypeRole.Connector, "form_connector", string.Empty),
                        Role(TypeRole.OperationTarget, "vertex", "vertices"),
                    })
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray());
        }

        private static TypeRoleRecord Role(TypeRole role, string noun, string plural)
        {
            return new TypeRoleRecord(
                "Sdk." + noun, role, "題材の根拠。", noun, plural, CapabilityOwner.Session);
        }
    }
}
