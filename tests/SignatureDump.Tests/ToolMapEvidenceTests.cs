using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolMapEvidenceTests
    {
        [Fact]
        public void OnlyTheRolesWithoutTheirOwnToolAreEmbeddedTypes()
        {
            ISet<string> embedded = ToolMapEvidence.EmbeddedTypeNames(
                new TypeRoleTable(
                    new[]
                    {
                        Type("PEPlugin.Pmx.IPXVertex", TypeRole.OperationTarget),
                        Type("PEPlugin.PEVmePreviewOption", TypeRole.Dto),
                        Type("PEPlugin.View.PXViewClickEventArgs", TypeRole.EventArgs),
                    },
                    new HandleIssuanceRecord[0],
                    new ElementCollectionRecord[0]));

            Assert.Equal(
                new[] { "PEPlugin.PEVmePreviewOption", "PEPlugin.View.PXViewClickEventArgs" },
                new SortedSet<string>(embedded, StringComparer.Ordinal));
        }

        /// <summary>
        /// 型役割表は総称型を引数の数で書き、引き当てる側は列挙の表記から写すので、鍵は同じ形へ
        /// そろえて持つ。
        /// </summary>
        [Fact]
        public void AGenericTypeIsKeptByItsDefinitionName()
        {
            ISet<string> embedded = ToolMapEvidence.EmbeddedTypeNames(
                new TypeRoleTable(
                    new[] { Type("PEPlugin.Vme.IPEValue<1>", TypeRole.Dto) },
                    new HandleIssuanceRecord[0],
                    new ElementCollectionRecord[0]));

            Assert.Contains(
                TypeDefinitionName.OfElement("PEPlugin.Vme.IPEValue<T>"), embedded);
        }

        [Fact]
        public void TheArgumentIsChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolMapEvidence.EmbeddedTypeNames(null));
        }

        /// <summary>役割を1つ持つ型。独立したツールを持つ役割だけが群とツール名を持つ。</summary>
        private static TypeRoleRecord Type(string typeName, TypeRole role)
        {
            bool independent = TypeRoleRecord.HasIndependentTool(role);
            return new TypeRoleRecord(
                typeName,
                role,
                "根拠。",
                "element",
                "elements",
                independent ? CapabilityOwner.Model : CapabilityOwner.None);
        }
    }
}
