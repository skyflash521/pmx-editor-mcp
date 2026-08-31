using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedBaselineJsonTests
    {
        private static IList<ExcludedBaselineEntry> Entries()
        {
            return new List<ExcludedBaselineEntry>
            {
                Entry("CAP-339", "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)", "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)"),
                Entry("CAP-398", "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)"),
            };
        }

        private static ExcludedBaselineEntry Entry(string id, params string[] signatures)
        {
            return new ExcludedBaselineEntry(id, new ReadOnlyCollection<string>(new List<string>(signatures)));
        }

        [Fact]
        public void WritesOneSignaturePerLine()
        {
            Assert.Equal(
                "{\n"
                + "\"capabilities\":[\n"
                + "{\"capabilityId\":\"CAP-339\",\"signatures\":[\n"
                + "\"PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)\",\n"
                + "\"PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)\"\n"
                + "]},\n"
                + "{\"capabilityId\":\"CAP-398\",\"signatures\":[\n"
                + "\"PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)\"\n"
                + "]}\n"
                + "]\n"
                + "}\n",
                ExcludedBaselineJson.Write(Entries()));
        }

        [Fact]
        public void EmptyCollectionKeepsTheShape()
        {
            Assert.Equal("{\n\"capabilities\":[]\n}\n", ExcludedBaselineJson.Write(new List<ExcludedBaselineEntry>()));
        }

        [Fact]
        public void MissingCollectionThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedBaselineJson.Write(null));
        }
    }
}
