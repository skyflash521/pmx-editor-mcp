using System;
using System.Runtime.InteropServices;

namespace PmxEditorMcp
{
    /// <summary>
    /// いまの物理的な押し方で修飾キーを見る。スレッドごとの入力の状態から見ると、メッセージを
    /// 受け取らないワーカースレッドでは押されていないことになってしまう。
    /// </summary>
    public sealed class PressedModifierKeys : IModifierKeys
    {
        private const int Shift = 0x10;

        private const int Control = 0x11;

        private const int Alt = 0x12;

        /// <summary>押されているかどうかを表す最上位の桁。</summary>
        private const short Down = unchecked((short)0x8000);

        /// <summary>ShiftかCtrlかAltが押されていれば真。</summary>
        public bool AnyHeld()
        {
            return Held(Shift) || Held(Control) || Held(Alt);
        }

        private static bool Held(int key)
        {
            return (GetAsyncKeyState(key) & Down) != 0;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
    }
}
