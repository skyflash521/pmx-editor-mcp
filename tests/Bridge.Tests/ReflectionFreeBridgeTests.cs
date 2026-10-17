using System.Linq;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// ブリッジも配布物なので、名前で型やメンバーを引く経路を持たない。判じ方はホストへ掛けるもの
    /// と同じ本文で、ここではブリッジのアセンブリへ当てる。
    /// </summary>
    public sealed class ReflectionFreeBridgeTests
    {
        [Fact]
        public void TheBridgeHasNoPathThatLooksUpByName()
        {
            ReflectionScan scan = ReflectionFreeGate.Scan(
                typeof(BridgeServer).Assembly.ManifestModule);

            Assert.Equal(string.Empty, string.Join("\n", scan.Found));
        }

        [Fact]
        public void EveryMemberReferenceOfTheBridgeIsJudged()
        {
            ReflectionScan scan = ReflectionFreeGate.Scan(
                typeof(BridgeServer).Assembly.ManifestModule);

            Assert.Equal(
                string.Empty,
                string.Join("\n", scan.Unreadable.Select(row => row.ToString())));
        }
    }
}
