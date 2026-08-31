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
        public void 型とシグネチャの両方を書かれた順に読む()
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
        public void 空の並びを読める()
        {
            LedgerOutOfScopeRecord record =
                LedgerOutOfScopeJsonReader.Read("{\"types\":[],\"signatures\":[]}");

            Assert.Empty(record.Types);
            Assert.Empty(record.Signatures);
        }

        [Fact]
        public void JSONとして読めないと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{"));
        }

        [Fact]
        public void 引数がnullだと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => LedgerOutOfScopeJsonReader.Read(null));
        }

        [Fact]
        public void 最上位の項目が欠けていると例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{\"types\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read("{\"signatures\":[]}"));
        }

        [Fact]
        public void 並びの項目の必須の項目が欠けていると例外になる()
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
        public void 並びの項目が項目の組でないと例外になる()
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
        public void 知らない項目があると例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[],\"note\":\"\"}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"reason\":\"route\",\"note\":\"\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"route\",\"note\":\"\"}]}"));
        }

        [Fact]
        public void 並びでない値を渡すと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":{},\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":\"\"}"));
        }

        [Fact]
        public void 項目の型が違うと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":1,\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":2}]}"));
        }

        [Fact]
        public void 名前や行キーが空だと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"\",\"reason\":\"route\"}],\"signatures\":[]}"));
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"\",\"reason\":\"route\"}]}"));
        }

        [Fact]
        public void 閉集合に無い理由だと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"A\",\"reason\":\"pluginMechanism\"}],\"signatures\":[]}"));
        }

        [Fact]
        public void 型ごと対象外になる理由をシグネチャへ書くと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[],\"signatures\":[{\"key\":\"A\",\"reason\":\"enumType\"}]}"));
        }

        [Fact]
        public void 序数の昇順で並んでいないと例外になる()
        {
            Assert.Throws<FormatException>(() => LedgerOutOfScopeJsonReader.Read(
                "{\"types\":[{\"name\":\"B\",\"reason\":\"route\"},{\"name\":\"A\",\"reason\":\"route\"}]"
                    + ",\"signatures\":[]}"));
        }

        [Fact]
        public void 同じ識別子が二度現れると例外になる()
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
