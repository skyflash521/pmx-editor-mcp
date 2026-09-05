using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureModelTests
    {
        private const string Key = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)";

        private const string Alternative = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)";

        [Fact]
        public void BaselineGroundsCarryOnlyTheCapabilityId()
        {
            ExcludedSignatureRecord record = ExcludedSignatureRecord.FromBaseline(Key, "CAP-390");

            Assert.Equal(Key, record.Key);
            Assert.Equal(ExclusionQualification.Baseline, record.Qualification);
            Assert.Equal("CAP-390", record.CapabilityId);
            Assert.Equal(ExclusionCategory.None, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
        }

        [Fact]
        public void BaselineGroundsWithEmptyKeyOrCapabilityIdThrow()
        {
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(string.Empty, "CAP-390"));
            Assert.Throws<ArgumentException>(() => ExcludedSignatureRecord.FromBaseline(Key, string.Empty));
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureRecord.FromBaseline(null, "CAP-390"));
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureRecord.FromBaseline(Key, null));
        }

        [Fact]
        public void CategoryGroundsCarryNoCapabilityId()
        {
            ExcludedSignatureRecord record =
                ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, Alternative);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.Pmd, record.Category);
            Assert.Equal(Alternative, record.Alternative);
            Assert.Equal(string.Empty, record.CapabilityId);
        }

        [Fact]
        public void CategoryRequiringAnAlternativeThrowsWhenItIsEmpty()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, string.Empty));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(
                    Key, ExclusionCategory.ConstructorDuplicate, string.Empty));
        }

        /// <summary>
        /// 列挙型は定義外の整数からも作れるので、既知の値の並びで受けて残りは弾く。
        /// </summary>
        [Fact]
        public void CategoryOutsideTheClosedSetThrows()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, (ExclusionCategory)999, string.Empty));
        }

        [Fact]
        public void CategoryNotRequiringAnAlternativeThrowsWhenGivenOne()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Delegate, Alternative));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.CPluginArgument, Alternative));
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.PmdModel, Alternative));
        }

        [Fact]
        public void CategoryGroundsWithoutACategoryThrow()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.None, string.Empty));
        }

        [Fact]
        public void WhitespaceOnlyKeyCapabilityIdOrAlternativeThrows()
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
        public void TheExcludedSignatureItselfCannotBeTheAlternative()
        {
            Assert.Throws<ArgumentException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Pmd, Key));
        }

        [Fact]
        public void CategoryGroundsWithNullKeyOrAlternativeThrow()
        {
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRecord.FromCategory(null, ExclusionCategory.Delegate, string.Empty));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRecord.FromCategory(Key, ExclusionCategory.Delegate, null));
        }
    }
}
