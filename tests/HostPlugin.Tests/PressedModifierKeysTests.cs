using System;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class PressedModifierKeysTests
    {
        private const int ShiftKey = 0x10;

        private const byte Down = 0x80;

        [Fact]
        public void AKeyHeldInTheInputStateOfTheCallingThreadIsHeld()
        {
            Assert.True(OnThread(new[] { ShiftKey }));
        }

        [Fact]
        public void NoKeyHeldInTheInputStateOfTheCallingThreadIsNotHeld()
        {
            Assert.False(OnThread(new int[0]));
        }

        private static bool OnThread(int[] held)
        {
            bool answered = false;
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    byte[] state = new byte[256];
                    foreach (int key in held)
                    {
                        state[key] = Down;
                    }

                    if (!SetKeyboardState(state))
                    {
                        throw new InvalidOperationException("キーボードの状態を書けなかった。");
                    }

                    answered = new PressedModifierKeys().AnyHeld();
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
                throw new InvalidOperationException("スレッドで落ちた。", caught);
            }

            return answered;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] state);
    }
}
