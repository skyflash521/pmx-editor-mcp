using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class FrozenExclusionJsonReaderTests
    {
        private const string Provided =
            @"{ ""capabilityId"": ""CAP-001"", ""status"": ""提供"", ""target"": ""IPXPmx"",
                ""remarks"": ""非対応件数: 2。"", ""signatures"": [""N.T.A()"", ""N.T.B()""], ""types"": [] }";

        private const string Typed =
            @"{ ""capabilityId"": ""CAP-002"", ""status"": ""非対応"", ""target"": ""IPEPlugin / PEPluginClass"",
                ""remarks"": null, ""signatures"": [], ""types"": [""N.IPEPlugin"", ""N.PEPluginClass""] }";

        private const string Patterned =
            @"{ ""capabilityId"": ""CAP-003"", ""status"": ""非対応"", ""target"": ""N.Pmd.* のまとめ"",
                ""remarks"": null, ""signatures"": [], ""types"": [] }";

        private static string Table(params string[] capabilities)
        {
            return "{ \"capabilities\": [" + string.Join(",", capabilities) + "] }";
        }

        [Fact]
        public void ReadsEachCapabilityWithHowItSelectsAndHowTheLedgerRecordedIt()
        {
            IList<FrozenExclusion> read = FrozenExclusionJsonReader.Read(Table(Provided, Typed, Patterned));

            Assert.Equal(new[] { "CAP-001", "CAP-002", "CAP-003" }, read.Select(e => e.CapabilityId));

            Assert.Equal(CapabilityStatus.Provided, read[0].Status);
            Assert.Equal("IPXPmx", read[0].Target);
            Assert.Equal("非対応件数: 2。", read[0].Remarks);
            Assert.Equal(new[] { "N.T.A()", "N.T.B()" }, read[0].Signatures);
            Assert.Empty(read[0].Types);

            Assert.Equal(CapabilityStatus.NotSupported, read[1].Status);
            Assert.Null(read[1].Remarks);
            Assert.Empty(read[1].Signatures);
            Assert.Equal(new[] { "N.IPEPlugin", "N.PEPluginClass" }, read[1].Types);

            Assert.Equal("N.Pmd.* のまとめ", read[2].Target);
            Assert.Empty(read[2].Signatures);
            Assert.Empty(read[2].Types);
        }

        [Theory]
        [InlineData("capabilityId")]
        [InlineData("status")]
        [InlineData("target")]
        [InlineData("remarks")]
        [InlineData("signatures")]
        [InlineData("types")]
        public void ACapabilityThatLacksAMemberIsRefused(string member)
        {
            string lacking = Typed.Replace("\"" + member + "\"", "\"absent\"");

            FormatException refused = Assert.Throws<FormatException>(
                () => FrozenExclusionJsonReader.Read(Table(lacking)));

            Assert.Contains(member, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ACapabilityListedTwiceIsRefused()
        {
            Assert.Throws<FormatException>(() => FrozenExclusionJsonReader.Read(Table(Typed, Typed)));
        }

        [Fact]
        public void CapabilitiesOutOfOrderAreRefused()
        {
            Assert.Throws<FormatException>(() => FrozenExclusionJsonReader.Read(Table(Typed, Provided)));
        }

        [Fact]
        public void ASignatureListedTwiceIsRefused()
        {
            string twice = Provided.Replace("\"N.T.B()\"", "\"N.T.A()\"");

            FormatException refused = Assert.Throws<FormatException>(
                () => FrozenExclusionJsonReader.Read(Table(twice)));

            Assert.Contains("N.T.A()", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATypeListedUnderTwoCapabilitiesIsRefused()
        {
            string again = Patterned.Replace("\"types\": []", "\"types\": [\"N.IPEPlugin\"]");

            FormatException refused = Assert.Throws<FormatException>(
                () => FrozenExclusionJsonReader.Read(Table(Typed, again)));

            Assert.Contains("N.IPEPlugin", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AProvidedCapabilityWithoutRemarksIsRefused()
        {
            string silent = Provided.Replace("\"非対応件数: 2。\"", "null");

            Assert.Throws<FormatException>(() => FrozenExclusionJsonReader.Read(Table(silent)));
        }

        [Fact]
        public void AnUnsupportedCapabilityWithRemarksIsRefused()
        {
            string remarked = Typed.Replace("\"remarks\": null", "\"remarks\": \"備考。\"");

            Assert.Throws<FormatException>(() => FrozenExclusionJsonReader.Read(Table(remarked)));
        }

        [Fact]
        public void AnUnknownStatusIsRefused()
        {
            string unknown = Typed.Replace("\"非対応\"", "\"保留\"");

            Assert.Throws<FormatException>(() => FrozenExclusionJsonReader.Read(Table(unknown)));
        }
    }
}
