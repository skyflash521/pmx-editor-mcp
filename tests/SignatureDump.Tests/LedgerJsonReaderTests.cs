using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerJsonReaderTests
    {
        [Fact]
        public void ATargetThatNamesOneThingYieldsOneName()
        {
            CapabilityRecord record = Single("IPXPmxConnector.GetCurrentState");

            Assert.Equal(CapabilityTargetKind.Single, record.TargetKind);
            Assert.Equal(new[] { "IPXPmxConnector.GetCurrentState" }, record.TargetNames.ToArray());
        }

        [Fact]
        public void ATargetThatListsNamesIsSplitPerName()
        {
            CapabilityRecord record = Single("IPXBone / IPXIK / IPXIKLink");

            Assert.Equal(CapabilityTargetKind.Group, record.TargetKind);
            Assert.Equal(
                new[] { "IPXBone", "IPXIK", "IPXIKLink" }, record.TargetNames.ToArray());
        }

        [Fact]
        public void TheDotSeparatorDoesNotSplitATarget()
        {
            Assert.Equal(
                new[] { "PXEventArgs.UIModelMouse" },
                Single("PXEventArgs.UIModelMouse").TargetNames.ToArray());
        }

        [Fact]
        public void ATargetThatNamesAPatternHasNoNamesAndKeepsItsText()
        {
            CapabilityRecord record = Single("PEPlugin.Pmd.* のまとめ", "非対応", string.Empty);

            Assert.Equal(CapabilityTargetKind.Pattern, record.TargetKind);
            Assert.Empty(record.TargetNames);
            Assert.Equal("PEPlugin.Pmd.* のまとめ", record.Target);
        }

        [Fact]
        public void TheGenericArityIsDroppedFromNamesButKeptInTheTargetText()
        {
            CapabilityRecord record = Single("IPEVmePrimaryValue`1");

            Assert.Equal(new[] { "IPEVmePrimaryValue" }, record.TargetNames.ToArray());
            Assert.Equal("IPEVmePrimaryValue`1", record.Target);
        }

        [Fact]
        public void ASuffixThatCannotBeAGenericArityIsKept()
        {
            Assert.Equal(new[] { "IPXBody`0" }, Single("IPXBody`0").TargetNames.ToArray());
        }

        [Fact]
        public void TheColumnsOfARowAreRead()
        {
            CapabilityRecord record = Single(
                "IPXPmx", "提供", "モデル", category: "PMXデータ", remarks: "全公開メンバー");

            Assert.Equal("CAP-001", record.Id);
            Assert.Equal("PMXデータ", record.Category);
            Assert.Equal(CapabilityStatus.Provided, record.Status);
            Assert.Equal(CapabilityOwner.Model, record.Owner);
            Assert.Equal("全公開メンバー", record.Remarks);
        }

        [Theory]
        [InlineData("非対応", "")]
        [InlineData("要調査", "")]
        public void AStatusThatTakesNoOwnerIsRead(string status, string owner)
        {
            Assert.Equal(CapabilityOwner.None, Single("IPXPmx", status, owner).Owner);
        }

        [Fact]
        public void ALedgerWithoutCapabilitiesReadsAsNothing()
        {
            Assert.Empty(LedgerJsonReader.Read(new LedgerJsonBuilder().ToString()));
        }

        [Fact]
        public void AStatusOrOwnerThatIsNotKnownStops()
        {
            FormatException status = Assert.Throws<FormatException>(
                () => Single("IPXPmx", "見送り", "モデル"));
            Assert.Contains("分類", status.Message, StringComparison.Ordinal);

            FormatException owner = Assert.Throws<FormatException>(
                () => Single("IPXPmx", "提供", "描画"));
            Assert.Contains("担当", owner.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWhoseStatusAndOwnerDisagreeStops()
        {
            Assert.Throws<FormatException>(() => Single("IPXPmx", "提供", string.Empty));
            Assert.Throws<FormatException>(() => Single("IPXPmx", "非対応", "モデル"));
        }

        [Fact]
        public void ARowThatLacksAFieldStops()
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read(
                Document("{\"id\":\"CAP-001\",\"category\":\"標本\",\"target\":\"IPXPmx\","
                    + "\"status\":\"提供\",\"owner\":\"モデル\"}")));
        }

        [Fact]
        public void ARowThatCarriesAFieldNobodyKnowsStops()
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read(
                Document("{\"id\":\"CAP-001\",\"category\":\"標本\",\"target\":\"IPXPmx\","
                    + "\"status\":\"提供\",\"owner\":\"モデル\",\"remarks\":\"\",\"note\":\"x\"}")));
        }

        [Fact]
        public void ARowWhoseFieldIsNotTextStops()
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read(
                Document("{\"id\":1,\"category\":\"標本\",\"target\":\"IPXPmx\","
                    + "\"status\":\"提供\",\"owner\":\"モデル\",\"remarks\":\"\"}")));
        }

        [Fact]
        public void ADocumentThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read("これはJSONではない"));
        }

        [Fact]
        public void ADocumentWithoutTheSourceStops()
        {
            Assert.Throws<FormatException>(
                () => LedgerJsonReader.Read("{\"capabilities\":[]}"));
        }

        [Theory]
        [InlineData("1")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("\"\"")]
        public void ASourceFieldThatIsNotTextStops(string value)
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read(
                "{\"source\":{\"distribution\":" + value + ",\"assembly\":\"PEPlugin.dll\","
                    + "\"assemblyVersion\":\"0.0.0.0\",\"framework\":\".NET Framework 4.0\"},"
                    + "\"capabilities\":[]}"));
        }

        [Fact]
        public void ASourceThatCarriesAFieldNobodyKnowsStops()
        {
            Assert.Throws<FormatException>(() => LedgerJsonReader.Read(
                "{\"source\":{\"distribution\":\"標本\",\"assembly\":\"PEPlugin.dll\","
                    + "\"assemblyVersion\":\"0.0.0.0\",\"framework\":\".NET Framework 4.0\","
                    + "\"note\":\"x\"},\"capabilities\":[]}"));
        }

        [Fact]
        public void TheDocumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => LedgerJsonReader.Read(null));
        }

        private static CapabilityRecord Single(
            string target,
            string status = "提供",
            string owner = "モデル",
            string category = "標本",
            string remarks = "")
        {
            IList<CapabilityRecord> records = LedgerJsonReader.Read(new LedgerJsonBuilder()
                .Add("CAP-001", category, target, status, owner, remarks)
                .ToString());

            return Assert.Single(records);
        }

        /// <summary>能力の行を1つだけ持つ正本。行の形そのものを崩すために素のJSONで書く。</summary>
        private static string Document(string capability)
        {
            return "{\"source\":{\"distribution\":\"標本\",\"assembly\":\"PEPlugin.dll\","
                + "\"assemblyVersion\":\"0.0.0.0\",\"framework\":\".NET Framework 4.0\"},"
                + "\"capabilities\":[" + capability + "]}";
        }
    }
}
