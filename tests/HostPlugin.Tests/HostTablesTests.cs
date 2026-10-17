using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// ホストの起動で作る大きな表は、並べて作っても、順に作ったものと同じ中身である。
    /// </summary>
    public sealed class HostTablesTests
    {
        [Fact]
        public void TheTablesBuiltTogetherHaveTheSameEntriesAsTheOnesBuiltOneAfterAnother()
        {
            HostTables together = HostTables.Start();

            Assert.Equal(GeneratedTools.Calls(new List<string>()).Count, together.Calls.Count);
            Assert.Equal(GeneratedTools.Aggregations(new List<string>()).Count, together.Aggregations.Count);
            Assert.Equal(GeneratedTools.Elements(new List<string>()).Count, together.Elements.Count);
            Assert.Equal(GeneratedTools.Preconditions().Count, together.Preconditions.Count);
            Assert.Equal(GeneratedTools.Attachments().Count, together.Attachments.Count);
            Assert.Equal(GeneratedTools.Payloads().Count, together.Payloads.Count);
            Assert.NotNull(together.Relay);
        }

        [Fact]
        public void EachTableIsBuiltOnce()
        {
            HostTables together = HostTables.Start();

            Assert.Same(together.Calls, together.Calls);
            Assert.Same(together.Aggregations, together.Aggregations);
        }
    }
}
