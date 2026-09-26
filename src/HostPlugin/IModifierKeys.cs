namespace PmxEditorMcp
{
    /// <summary>
    /// 修飾キーがいま押されているかを、エディタが量の向きと倍率を決めるときと同じく、呼び出したスレッドの入力の
    /// 状態で見る。エディタのUIスレッドから呼ぶこと。
    /// </summary>
    public interface IModifierKeys
    {
        /// <summary>ShiftかCtrlかAltが押されていれば真。</summary>
        bool AnyHeld();
    }
}
