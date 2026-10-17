using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerOutOfScopeJsonReaderTests
    {
        private const string Sample =
            "{\"types\":["
                + "{\"name\":\"PEPlugin.IPEConnector\"},"
                + "{\"name\":\"PEPlugin.Vme.OpType\"},"
                + "{\"name\":\"PEPlugin.Vme.PEVmeEvent\"},"
                + "{\"name\":\"PEPlugin.Vme.PEVmePreviewOption\"}"
                + "],\"signatures\":["
                + "{\"key\":\"PEPlugin.IPEBuilder.Pmx()\"},"
                + "{\"key\":\"PEPlugin.IPEBuilder.SC()\"}"
                + "]}";

        [Fact]
        public void ReadsBothTypesAndSignaturesInWrittenOrder()
        {
            LedgerOutOfScopeRecord record = LedgerOutOfScopeJsonReader.Read(Sample);

            Assert.Equal(4, record.Types.Count);
            Assert.Equal("PEPlugin.IPEConnector", record.Types[0].Name);
            Assert.Equal("PEPlugin.Vme.OpType", record.Types[1].Name);
            Assert.Equal("PEPlugin.Vme.PEVmeEvent", record.Types[2].Name);
            Assert.Equal("PEPlugin.Vme.PEVmePreviewOption", record.Types[3].Name);

            Assert.Equal(2, record.Signatures.Count);
            Assert.Equal("PEPlugin.IPEBuilder.Pmx()", record.Signatures[0].Key);
            Assert.Equal("PEPlugin.IPEBuilder.SC()", record.Signatures[1].Key);
        }

        [Fact]
        public void ReadsEmptyCollections()
        {
            LedgerOutOfScopeRecord record =
                LedgerOutOfScopeJsonReader.Read("{\"types\":[],\"signatures\":[]}");

            Assert.Empty(record.Types);
            Assert.Empty(record.Signatures);
        }

        [Fact]
        public void BodyThatIsNotJsonThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => LedgerOutOfScopeJsonReader.Read(null));
        }

        [Fact]
        public void MissingTopLevelMemberThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{\"types\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{\"signatures\":[]}"));
        }

        [Fact]
        public void ArrayItemMissingARequiredMemberThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{}]}"));
        }

        [Fact]
        public void ArrayItemThatIsNotAnObjectThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[null],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[\"A\"],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[[]],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[null]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[\"A\"]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[[]]}"));
        }

        [Fact]
        public void UnknownMemberThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[],\"note\":\"\"}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"note\":\"\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"note\":\"\"}]}"));
        }

        [Fact]
        public void NonArrayValueThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":{},\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":\"\"}"));
        }

        [Fact]
        public void WrongMemberTypeThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":1}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":1}]}"));
        }

        [Fact]
        public void EmptyNameOrKeyThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"\"}]}"));
        }

        [Fact]
        public void AWrittenReasonThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"route\"}]}"));
        }

        [Fact]
        public void OrderThatIsNotAscendingThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"B\"},{\"name\":\"A\"}]"
                    + ",\"signatures\":[]}"));
        }

        [Fact]
        public void DuplicateIdentifierThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\"},{\"name\":\"A\"}]"
                    + ",\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\"}"
                    + ",{\"key\":\"A\"}]}"));
        }
    }
}
