using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedBaselineJsonReaderTests
    {

        private static IList<ExcludedBaselineEntry> Entries()
        {
            return new List<ExcludedBaselineEntry>
            {
                Entry(
                    "CAP-114",
                    "PEPlugin.View.IPEPMDViewConnector.BootupVmdView(PEPlugin.Pmd.IPEPmd,PEPlugin.Vmd.IPEVmd)"),
                Entry(
                    "CAP-339",
                    "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)",
                    "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)"),
                Entry("CAP-398", "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)"),
            };
        }

        private static ExcludedBaselineEntry Entry(string id, params string[] signatures)
        {
            return new ExcludedBaselineEntry(id, new ReadOnlyCollection<string>(signatures.ToList()));
        }

        private static void AssertSame(
            IList<ExcludedBaselineEntry> expected, IList<ExcludedBaselineEntry> actual)
        {
            Assert.Equal(
                expected.Select(e => e.CapabilityId).ToArray(), actual.Select(e => e.CapabilityId).ToArray());

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Signatures.ToArray(), actual[i].Signatures.ToArray());
            }
        }

        [Fact]
        public void WrittenJsonIsReadBackAsTheSameSet()
        {
            AssertSame(Entries(), ExcludedBaselineJsonReader.Read(ExcludedBaselineJson.Write(Entries())));
        }

        [Fact]
        public void ReturnsCapabilityIdsAndKeysInAscendingOrder()
        {
            string json = ExcludedBaselineJson.Write(new List<ExcludedBaselineEntry>
            {
                Entry("CAP-398", "T.B()", "T.A()"),
                Entry("CAP-339", "T.C()"),
            });

            AssertSame(
                new List<ExcludedBaselineEntry> { Entry("CAP-339", "T.C()"), Entry("CAP-398", "T.A()", "T.B()") },
                ExcludedBaselineJsonReader.Read(json));
        }

        [Fact]
        public void ReadsAnEmptySet()
        {
            Assert.Empty(ExcludedBaselineJsonReader.Read("{\"capabilities\":[]}\n"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("{\"capabilities\":{}}")]
        [InlineData("{\"capabilities\":[{\"signatures\":[\"T.A()\"]}]}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\"}]}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\",\"signatures\":\"T.A()\"}]}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"\",\"signatures\":[\"T.A()\"]}]}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\",\"signatures\":[\"\"]}]}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\",\"signatures\":[1]}]}")]
        [InlineData("{\"capabilities\":[null]}")]
        [InlineData("{\"capabilities\":[\"CAP-1\"]}")]
        [InlineData("{\"capabilities\":[],\"extra\":1}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.A()\"],\"extra\":1}]}")]
        public void MalformedShapeThrows(string json)
        {
            Assert.Throws<FormatException>(() => ExcludedBaselineJsonReader.Read(json));
        }

        [Fact]
        public void DuplicateCapabilityIdThrows()
        {
            string json = "{\"capabilities\":["
                + "{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.A()\"]},"
                + "{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.B()\"]}]}";

            Assert.Throws<FormatException>(() => ExcludedBaselineJsonReader.Read(json));
        }

        [Fact]
        public void DuplicateKeyIsAnInputErrorNotAnEnumerationMismatch()
        {
            string inOneCapability = "{\"capabilities\":["
                + "{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.A()\",\"T.A()\"]}]}";
            string acrossCapabilities = "{\"capabilities\":["
                + "{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.A()\"]},"
                + "{\"capabilityId\":\"CAP-2\",\"signatures\":[\"T.A()\"]}]}";

            Assert.Throws<FormatException>(() => ExcludedBaselineJsonReader.Read(inOneCapability));
            Assert.Throws<FormatException>(() => ExcludedBaselineJsonReader.Read(acrossCapabilities));
        }

        [Fact]
        public void MissingContentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedBaselineJsonReader.Read(null));
        }
    }
}
