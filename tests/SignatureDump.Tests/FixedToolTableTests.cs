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
                    FixedToolTable.PingName,
                    FixedToolTable.SdkStatusName,
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

        [Fact]
        public void EveryPublishedFixedToolCarriesAText()
        {
            Assert.All(
                FixedToolTable.Descriptions(debugHooks: true).Values,
                text => Assert.False(string.IsNullOrWhiteSpace(text)));
        }
    }
}
