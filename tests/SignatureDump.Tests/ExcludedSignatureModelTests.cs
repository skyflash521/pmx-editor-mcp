using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureModelTests
    {
        private const string Key = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)";

        private const string Alternative = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)";

        [Fact]
        public void ベースラインを根拠にする1件は能力IDだけを持つ()
        {
            ExcludedSignatureRecord record = ExcludedSignatureRecord.FromBaseline(Key, "CAP-390");

            Assert.Equal(Key, record.Key);
            Assert.Equal(ExclusionQualification.Baseline, record.Qualification);
            Assert.Equal("CAP-390", record.CapabilityId);
            Assert.Equal(ExclusionCategory.None, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
        }

        [Fact]
        public void ベースラインを根拠にするのに行キーか能力IDが空だと例外になる()
        {
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(string.Empty, "CAP-390"));
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(Key, string.Empty));
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureRecord.FromBaseline(null, "CAP-390"));
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureRecord.FromBaseline(Key, null));
        }

        [Fact]
        public void カテゴリを根拠にする1件は能力IDを持たない()
        {
            ExcludedSignatureRecord record =
                ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, Alternative);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.Pmd, record.Category);
            Assert.Equal(Alternative, record.Alternative);
            Assert.Equal(string.Empty, record.CapabilityId);
        }

        [Fact]
        public void 代替の存在を条件とするカテゴリで代替が空だと例外になる()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, string.Empty));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(
                    Key, ExclusionCategory.ConstructorDuplicate, string.Empty));
        }

        [Fact]
        public void 閉集合の外のカテゴリを渡すと例外になる()
        {
            // 列挙型は定義外の整数からも作れるので、既知の値の並びで受けて残りは弾く。
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, (ExclusionCategory)999, string.Empty));
        }

        [Fact]
        public void 代替の存在を条件としないカテゴリに代替を与えると例外になる()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Delegate, Alternative));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.CPluginArgument, Alternative));
        }

        [Fact]
        public void カテゴリを根拠にするのにカテゴリが無いと例外になる()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.None, string.Empty));
        }

        [Fact]
        public void 空白だけの行キーや能力IDや代替は例外になる()
        {
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(" ", "CAP-390"));
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(Key, " "));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(" ", ExclusionCategory.Delegate, string.Empty));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, " "));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Delegate, " "));
        }

        [Fact]
        public void 除外するシグネチャ自身を代替にすると例外になる()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, Key));
        }

        [Fact]
        public void カテゴリを根拠にするのに行キーか代替を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRecord.FromCategory(null, ExclusionCategory.Delegate, string.Empty));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Delegate, null));
        }
    }
}
