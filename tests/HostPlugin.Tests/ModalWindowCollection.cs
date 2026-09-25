using Xunit;

namespace PmxEditorMcp.Tests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ModalWindowCollection
    {
        public const string Name = "実物のモーダル表示を出す検査";
    }
}
