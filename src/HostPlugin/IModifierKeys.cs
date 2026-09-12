namespace PmxEditorMcp
{
    /// <summary>
    /// 修飾キーがいま押されているかを見る。UIスレッドの外から見るので、実装は呼び出し側のスレッドの
    /// 入力の状態ではなく、いまの物理的な押し方を見ること。
    /// </summary>
    public interface IModifierKeys
    {
        /// <summary>ShiftかCtrlかAltが押されていれば真。</summary>
        bool AnyHeld();
    }
}
