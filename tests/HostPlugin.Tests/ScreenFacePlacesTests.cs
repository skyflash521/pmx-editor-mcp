using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 面の選択を受け渡す中継の換算。外から渡す位置も外へ返す位置も面の通し番号で、画面が数える
    /// 位置はこの境界の内側だけに現れる。
    /// </summary>
    public sealed class ScreenFacePlacesTests
    {
        [Fact]
        public void TheFaceNumbersHandedToAWritingRowReachTheMemberAsTheirThreeCorners()
        {
            foreach (string rowKey in ScreenFacePlaces.Writing)
            {
                int[] reached = null;
                Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
                {
                    { rowKey, (target, arguments) => reached = (int[])arguments[0] },
                };

                ScreenFacePlaces.Fit(calls);
                calls[rowKey](null, new object[] { new[] { 0, 2 } });

                Assert.Equal(new[] { 0, 1, 2, 6, 7, 8 }, reached);
            }
        }

        [Fact]
        public void TheThreeCornersAReadingRowReturnsComeBackAsFaceNumbers()
        {
            foreach (string rowKey in ScreenFacePlaces.Reading)
            {
                Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
                {
                    { rowKey, (target, arguments) => new[] { 6, 7, 8, 0, 1, 2 } },
                };

                ScreenFacePlaces.Fit(calls);

                Assert.Equal(new[] { 2, 0 }, (int[])calls[rowKey](null, new object[0]));
            }
        }

        [Fact]
        public void ARowThatDoesNotCarryFacePlacesIsLeftAsItWas()
        {
            SdkCall untouched = (target, arguments) => "そのまま";
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { "Sdk.Type.Other()", untouched },
            };

            ScreenFacePlaces.Fit(calls);

            Assert.Same(untouched, calls["Sdk.Type.Other()"]);
            Assert.Single(calls);
        }

        [Fact]
        public void TheFaceSelectionThatTheGeneratedRelayWritesAndReadsIsCountedInFaceNumbers()
        {
            FakePmxView view = new FakePmxView();
            SdkRelayTable table = GeneratedSdkRelay.Create();

            object ignored;
            SdkRelayRefusal refusal;
            Assert.True(table.TryInvoke(
                "PEPlugin.View.IPEPMDViewConnector.SetSelectedFaceIndices(System.Int32[])",
                view,
                new object[] { new[] { 1 } },
                out ignored,
                out refusal));
            Assert.Equal(new[] { 3, 4, 5 }, view.Selected[ElementKinds.Face]);

            object held;
            Assert.True(table.TryInvoke(
                "PEPlugin.View.IPEPMDViewConnector.GetSelectedFaceIndices()",
                view,
                new object[0],
                out held,
                out refusal));
            Assert.Equal(new[] { 1 }, (int[])held);
        }
    }
}
