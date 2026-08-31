using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerOutOfScopeJsonReaderTests
    {
        private const string Sample =
            "{\"types\":["
                + "{\"name\":\"PEPlugin.IPEConnector\",\"reason\":\"route\"},"
                + "{\"name\":\"PEPlugin.Vme.OpType\",\"reason\":\"enumType\"},"
                + "{\"name\":\"PEPlugin.Vme.PEVmeEvent\",\"reason\":\"delegateType\"},"
                + "{\"name\":\"PEPlugin.Vme.PEVmePreviewOption\",\"reason\":\"argumentOnly\"}"
                + "],\"signatures\":["
                + "{\"key\":\"PEPlugin.IPEBuilder.Pmx()\",\"reason\":\"route\"},"
                + "{\"key\":\"PEPlugin.IPEBuilder.SC()\",\"reason\":\"route\"}"
                + "]}";

        [Fact]
        public void ReadsBothTypesAndSignaturesInWrittenOrder()
        {
            LedgerOutOfScopeRecord record = LedgerOutOfScopeJsonReader.Read(Sample);

            Assert.Equal(4, record.Types.Count);
            Assert.Equal("PEPlugin.IPEConnector", record.Types[0].Name);
            Assert.Equal(OutOfScopeReason.Route, record.Types[0].Reason);
            Assert.Equal(OutOfScopeReason.EnumType, record.Types[1].Reason);
            Assert.Equal(OutOfScopeReason.DelegateType, record.Types[2].Reason);
            Assert.Equal(OutOfScopeReason.ArgumentOnly, record.Types[3].Reason);

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
                "{\"types\":[{\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"reason\":\"route\"}]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\"}]}"));
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
                "{\"types\":[{\"name\":\"A\",\"reason\":\"route\",\"note\":\"\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"route\",\"note\":\"\"}]}"));
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
                "{\"types\":[{\"name\":1,\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":2}]}"));
        }

        [Fact]
        public void EmptyNameOrKeyThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"\",\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"\",\"reason\":\"route\"}]}"));
        }

        [Fact]
        public void ReasonOutsideTheClosedSetThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"reason\":\"pluginMechanism\"}],\"signatures\":[]}"));
        }

        [Fact]
        public void TypeOnlyReasonWrittenOnASignatureThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"enumType\"}]}"));
        }

        [Fact]
        public void OrderThatIsNotAscendingThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"B\",\"reason\":\"route\"},{\"name\":\"A\",\"reason\":\"route\"}]"
                    + ",\"signatures\":[]}"));
        }

        [Fact]
        public void DuplicateIdentifierThrows()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"reason\":\"route\"},{\"name\":\"A\",\"reason\":\"route\"}]"
                    + ",\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"route\"}"
                    + ",{\"key\":\"A\",\"reason\":\"route\"}]}"));
        }
    }
}
