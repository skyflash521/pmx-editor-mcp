using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 処理が時間の上限に収まることを確かめる検査を持つクラスの集まり。ほかの検査と並べて
    /// 走らせない。
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TimedCollection
    {
        public const string Name = "時間の上限を確かめる検査";
    }
}
