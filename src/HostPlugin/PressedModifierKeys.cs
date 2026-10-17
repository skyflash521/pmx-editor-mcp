using System.Windows.Forms;

namespace PmxEditorMcp
{
    public sealed class PressedModifierKeys : IModifierKeys
    {
        public bool AnyHeld()
        {
            return (Control.ModifierKeys & (Keys.Shift | Keys.Control | Keys.Alt)) != Keys.None;
        }
    }
}
