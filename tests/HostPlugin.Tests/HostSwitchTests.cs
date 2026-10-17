using System;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// ホストの停止と開始を、メニューを通さずに頼む入口。開くのは環境変数がちょうど開く値のときだけで、
    /// 開いていなければ、頼むメッセージが届いても何もしない。
    /// </summary>
    public sealed class HostSwitchTests
    {
        [Theory]
        [InlineData("1", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData(" 1", false)]
        [InlineData("true", false)]
        [InlineData("0", false)]
        public void TheSwitchOpensOnlyForTheExactValue(string value, bool opens)
        {
            Assert.Equal(opens, HostSwitch.IsEnabled(value));
        }

        [Fact]
        public void AnOpenSwitchStopsAndStartsTheHostOnTheMessage()
        {
            OnSta(() =>
            {
                int stopped = 0;
                int started = 0;
                using (HostSwitchForm form = new HostSwitchForm(true, () => stopped++, () => started++))
                {
                    IntPtr handle = form.Handle;

                    SendMessage(handle, HostSwitch.Message, IntPtr.Zero, IntPtr.Zero);
                    Assert.Equal(1, stopped);
                    Assert.Equal(0, started);

                    SendMessage(handle, HostSwitch.Message, new IntPtr(1), IntPtr.Zero);
                    Assert.Equal(1, stopped);
                    Assert.Equal(1, started);
                }
            });
        }

        [Fact]
        public void AClosedSwitchIgnoresTheMessage()
        {
            OnSta(() =>
            {
                int called = 0;
                using (HostSwitchForm form = new HostSwitchForm(false, () => called++, () => called++))
                {
                    IntPtr handle = form.Handle;

                    SendMessage(handle, HostSwitch.Message, IntPtr.Zero, IntPtr.Zero);
                    SendMessage(handle, HostSwitch.Message, new IntPtr(1), IntPtr.Zero);

                    Assert.Equal(0, called);
                }
            });
        }

        [Fact]
        public void AnotherMessageIsNotATurnOfTheSwitch()
        {
            OnSta(() =>
            {
                int called = 0;
                using (HostSwitchForm form = new HostSwitchForm(true, () => called++, () => called++))
                {
                    SendMessage(form.Handle, HostSwitch.Message + 1, IntPtr.Zero, IntPtr.Zero);

                    Assert.Equal(0, called);
                }
            });
        }

        [Fact]
        public void TheFormCanBeFoundByItsTitle()
        {
            OnSta(() =>
            {
                using (HostSwitchForm form = new HostSwitchForm(true, () => { }, () => { }))
                {
                    Assert.Equal(HostSwitch.WindowTitle, form.Text);
                }
            });
        }

        private static void OnSta(Action action)
        {
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
