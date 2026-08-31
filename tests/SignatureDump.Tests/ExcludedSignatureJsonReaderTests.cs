using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureJsonReaderTests
    {
        private const string Pmd = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)";

        private const string Alternative = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)";

        private const string Frozen = "PEPlugin.IPEBuilder.CreateBone()";

        private const string Delegated = "PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(PEPlugin.Vme.PEVmeEvent)";

        [Fact]
        public void 書き出したものを読み戻すと同じ内容になる()
        {
            IList<ExcludedSignatureRecord> written = Records();

            IList<ExcludedSignatureRecord> read =
                ExcludedSignatureJsonReader.Read(ExcludedSignatureJson.Write(written));

            Assert.Equal(written.Count, read.Count);
            for (int i = 0; i < written.Count; i++)
            {
                Assert.Equal(written[i].Key, read[i].Key);
                Assert.Equal(written[i].Qualification, read[i].Qualification);
                Assert.Equal(written[i].CapabilityId, read[i].CapabilityId);
                Assert.Equal(written[i].Category, read[i].Category);
                Assert.Equal(written[i].Alternative, read[i].Alternative);
            }
        }

        [Fact]
        public void カテゴリの4つの綴りをすべて読める()
        {
            string json = "{\"signatures\":["
                + "{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"cPluginArgument\"},"
                + "{\"key\":\"B\",\"qualification\":\"category\",\"category\":\"constructorDuplicate\""
                + ",\"alternative\":\"X\"},"
                + "{\"key\":\"C\",\"qualification\":\"category\",\"category\":\"delegate\"},"
                + "{\"key\":\"D\",\"qualification\":\"category\",\"category\":\"pmd\",\"alternative\":\"Y\"}"
                + "]}";

            IList<ExcludedSignatureRecord> read = ExcludedSignatureJsonReader.Read(json);

            Assert.Equal(
                new[]
                {
                    ExclusionCategory.CPluginArgument,
                    ExclusionCategory.ConstructorDuplicate,
                    ExclusionCategory.Delegate,
                    ExclusionCategory.Pmd,
                },
                read.Select(r => r.Category).ToArray());
        }

        [Fact]
        public void 空の並びを読める()
        {
            Assert.Empty(ExcludedSignatureJsonReader.Read("{\"signatures\":[]}"));
        }

        [Fact]
        public void JSONとして読めないと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{"));
        }

        [Fact]
        public void 引数がnullだと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureJsonReader.Read(null));
        }

        [Fact]
        public void 最上位の項目が欠けていると例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"items\":[]}"));
        }

        [Fact]
        public void 最上位に知らない項目があると例外になる()
        {
            Assert.Throws<FormatException>(
                () => ExcludedSignatureJsonReader.Read("{\"signatures\":[],\"items\":[]}"));
        }

        [Fact]
        public void 並びでない値を渡すと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":{}}"));
        }

        [Fact]
        public void 並びの項目が項目の組でないと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[null]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[\"A\"]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[[]]}"));
        }

        [Fact]
        public void 資格が欠けているか知らない値だと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"capabilityId\":\"CAP-1\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"other\",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void 資格に応じた項目が欠けていると例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\"}]}"));
        }

        [Fact]
        public void 資格に合わない項目があると例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\""
                    + ",\"category\":\"pmd\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"delegate\""
                    + ",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void 知らないカテゴリだと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"stream\"}]}"));
        }

        [Fact]
        public void 代替の有無がカテゴリと噛み合わないと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"pmd\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"delegate\""
                    + ",\"alternative\":\"X\"}]}"));
        }

        [Fact]
        public void 値の型が違うか空だと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":1,\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"\"}]}"));
        }

        [Fact]
        public void 序数の昇順で並んでいないと例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"B\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}"
                    + ",{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void 同じ行キーが二度現れると例外になる()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}"
                    + ",{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-2\"}]}"));
        }

        private static IList<ExcludedSignatureRecord> Records()
        {
            return new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromBaseline(Frozen, "CAP-463"),
                ExcludedSignatureRecord.FromCategory(
                    Delegated, ExclusionCategory.Delegate, string.Empty),
                ExcludedSignatureRecord.FromCategory(Pmd, ExclusionCategory.Pmd, Alternative),
            }
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToList();
        }
    }
}
