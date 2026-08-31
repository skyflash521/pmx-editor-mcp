using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureJsonTests
    {
        private static IList<ExcludedSignatureRecord> Records()
        {
            return new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromBaseline(
                    "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)", "CAP-398"),
                ExcludedSignatureRecord.FromCategory(
                    "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)",
                    ExclusionCategory.Pmd,
                    "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)"),
            };
        }

        [Fact]
        public void 除外を1行ずつ並べる()
        {
            Assert.Equal(
                "{\n"
                + "\"signatures\":[\n"
                + "{\"key\":\"PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)\","
                + "\"qualification\":\"baseline\",\"capabilityId\":\"CAP-398\"},\n"
                + "{\"key\":\"PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)\","
                + "\"qualification\":\"category\",\"category\":\"pmd\","
                + "\"alternative\":\"PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)\"}\n"
                + "]\n"
                + "}\n",
                ExcludedSignatureJson.Write(Records()));
        }

        [Fact]
        public void 代替を持たないカテゴリでは代替の欄を書かない()
        {
            IList<ExcludedSignatureRecord> records = new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromCategory(
                    "PXCPlugin.IPXSystemControl.SetAutoRelease(PXCPlugin.IPXCPlugin)",
                    ExclusionCategory.CPluginArgument,
                    string.Empty),
            };

            Assert.Equal(
                "{\n"
                + "\"signatures\":[\n"
                + "{\"key\":\"PXCPlugin.IPXSystemControl.SetAutoRelease(PXCPlugin.IPXCPlugin)\","
                + "\"qualification\":\"category\",\"category\":\"cPluginArgument\"}\n"
                + "]\n"
                + "}\n",
                ExcludedSignatureJson.Write(records));
        }

        [Fact]
        public void カテゴリは閉集合の綴りで書く()
        {
            Assert.Contains("\"category\":\"cPluginArgument\"", WithoutAlternative(ExclusionCategory.CPluginArgument));
            Assert.Contains("\"category\":\"delegate\"", WithoutAlternative(ExclusionCategory.Delegate));
            Assert.Contains("\"category\":\"pmd\"", WithAlternative(ExclusionCategory.Pmd));
            Assert.Contains(
                "\"category\":\"constructorDuplicate\"", WithAlternative(ExclusionCategory.ConstructorDuplicate));
        }

        private static string WithAlternative(ExclusionCategory category)
        {
            return ExcludedSignatureJson.Write(new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromCategory("T.M()", category, "T.N()"),
            });
        }

        private static string WithoutAlternative(ExclusionCategory category)
        {
            return ExcludedSignatureJson.Write(new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromCategory("T.M()", category, string.Empty),
            });
        }

        [Fact]
        public void 空の並びを渡しても形は崩れない()
        {
            Assert.Equal("{\n\"signatures\":[]\n}\n", ExcludedSignatureJson.Write(new List<ExcludedSignatureRecord>()));
        }

        [Fact]
        public void 並びを渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureJson.Write(null));
        }
    }
}
