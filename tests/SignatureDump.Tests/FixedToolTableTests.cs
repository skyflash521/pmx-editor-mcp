using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class FixedToolTableTests
    {
        [Fact]
        public void TheLivenessToolIsPublishedWithTheTestEntryClosed()
        {
            Assert.Equal(
                new[] { FixedToolTable.PingName },
                FixedToolTable.Descriptions(debugHooks: false).Keys.ToArray());
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
