using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>人の応答を待つ表示を窓の一覧から見分ける範囲。</summary>
    public sealed class ModalWindowsTests
    {
        [Fact]
        public void AVisibleWindowThatHoldsItsOwnerIsSaid()
        {
            Assert.Equal(
                "確認: 未保存の編集項目があります",
                ModalWindows.Describe(
                    new[] { Window("確認", "未保存の編集項目があります", true, true) }));
        }

        [Fact]
        public void AWindowThatIsNotVisibleIsNotSaid()
        {
            Assert.Null(ModalWindows.Describe(new[] { Window("確認", "本文", false, true) }));
        }

        [Fact]
        public void AWindowThatLeavesItsOwnerUsableIsNotSaid()
        {
            Assert.Null(ModalWindows.Describe(new[] { Window("設定", "本文", true, false) }));
        }

        [Fact]
        public void AWindowWithoutABodyIsSaidByItsCaption()
        {
            Assert.Equal(
                "確認", ModalWindows.Describe(new[] { Window("確認", string.Empty, true, true) }));
        }

        [Fact]
        public void AWindowWithoutACaptionIsSaidByItsBody()
        {
            Assert.Equal(
                "本文", ModalWindows.Describe(new[] { Window(string.Empty, "本文", true, true) }));
        }

        [Fact]
        public void AWindowWithNeitherCaptionNorBodyIsPassedOver()
        {
            Assert.Equal(
                "確認: 本文",
                ModalWindows.Describe(
                    new[]
                    {
                        Window(" ", "	", true, true),
                        Window("確認", "本文", true, true),
                    }));
        }

        [Fact]
        public void AWindowWithNeitherCaptionNorBodyAloneIsNotSaid()
        {
            Assert.Null(ModalWindows.Describe(new[] { Window(" ", "	", true, true) }));
        }

        [Fact]
        public void NoWindowThatHoldsItsOwnerIsSaidAsNothing()
        {
            Assert.Null(ModalWindows.Describe(new List<WindowNote>()));
        }

        [Fact]
        public void TheWindowListIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ModalWindows.Describe(null));
        }

        private static WindowNote Window(string caption, string body, bool visible, bool holdsOwner)
        {
            return new WindowNote(caption, body, visible, holdsOwner);
        }
    }
}
