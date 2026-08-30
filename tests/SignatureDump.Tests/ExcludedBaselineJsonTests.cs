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

        [Fact(Skip = "impl pending: 凍結した除外の組を決まった形のJSONへ書き出す")]
        public void シグネチャを1行ずつ並べる()
        {
            // 1つの能力が数百のシグネチャを持つので、1行にまとめると行単位の差分でどれが動いたかを
            // 追えなくなる。
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

        [Fact(Skip = "impl pending: 空の並びでも形を崩さずに書き出す")]
        public void 空の並びを渡しても形は崩れない()
        {
            Assert.Equal("{\n\"capabilities\":[]\n}\n", ExcludedBaselineJson.Write(new List<ExcludedBaselineEntry>()));
        }

        [Fact(Skip = "impl pending: 並びを渡さないときは例外にする")]
        public void 並びを渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedBaselineJson.Write(null));
        }
    }
}
