namespace PmxEditorMcp.Tests
{
    /// <summary>修飾キーが押されていない題材。</summary>
    internal sealed class StillModifierKeys : IModifierKeys
    {
        public bool AnyHeld()
        {
            return false;
        }
    }

    /// <summary>修飾キーが押されている題材。</summary>
    internal sealed class HeldModifierKeys : IModifierKeys
    {
        public bool AnyHeld()
        {
            return true;
        }
    }
}
