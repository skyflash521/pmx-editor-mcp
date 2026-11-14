using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class FixedToolTableTests
    {
        [Fact]
        public void TheToolsThatDoNotNeedTheEditorArePublishedWithTheTestEntryClosed()
        {
            Assert.Equal(
                new[]
                {
                    FixedToolTable.FindToolName,
                    FixedToolTable.HowToUpdateName,
                    FixedToolTable.ListEditorsName,
                    FixedToolTable.PingName,
                    FixedToolTable.SdkStatusName,
                    FixedToolTable.SelectEditorName,
                },
                FixedToolTable.Descriptions(debugHooks: false).Keys
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }

        [Fact]
        public void TheTestOnlyToolComesWithTheTestEntryOpen()
        {
            Assert.Contains(
                FixedToolTable.LargeTextName,
                FixedToolTable.Descriptions(debugHooks: true).Keys);
        }
    }
}
