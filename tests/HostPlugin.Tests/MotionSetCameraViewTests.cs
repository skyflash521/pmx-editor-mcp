using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// TransformView の視点は、PMXView の視点をカメラ同期で写して決める。同期は Shift を押して入れた
    /// ときだけ、その場の PMXView の視点を相手へ写す。
    /// </summary>
    public sealed class MotionSetCameraViewTests
    {
        [Fact]
        public void TheGivenViewpointIsCopiedThroughTheSyncTurnedOnWithShift()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, false))
                {
                    Held(fixture);

                    ComposedScreenFixture.Value(Call(fixture));

                    Assert.Single(screen.Copied);
                    Assert.Equal(1f, screen.Copied[0].X);
                    Assert.Equal(2f, screen.Copied[0].Y);
                    Assert.Equal(3f, screen.Copied[0].Z);
                    Assert.False(screen.Sync.Checked, "同期を元へ戻していない。");
                    Assert.Equal(9f, fixture.View.CameraPositionSet.X);
                    Assert.Equal(8f, fixture.View.CameraPositionSet.Y);
                    Assert.Equal(7f, fixture.View.CameraPositionSet.Z);
                }
            });
        }

        [Fact]
        public void ASyncAlreadyOnIsTurnedOffFirstAndLeftOn()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, true))
                {
                    Held(fixture);

                    ComposedScreenFixture.Value(Call(fixture));

                    Assert.Single(screen.Copied);
                    Assert.True(screen.Sync.Checked, "同期を元へ戻していない。");
                }
            });
        }

        [Fact]
        public void TheSyncGoesBackEvenWhenTheEditorFailsWhileCopying()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, false))
                {
                    Held(fixture);
                    screen.FailOnCopy = true;

                    Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(Call(fixture)));

                    Assert.False(screen.Sync.Checked, "同期を元へ戻していない。");
                    Assert.Equal(9f, fixture.View.CameraPositionSet.X);
                }
            });
        }

        [Fact]
        public void TheSplitViewIsRefusedWithoutMovingAnything()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, false))
                {
                    Held(fixture);
                    screen.Split.Checked = true;

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture)));
                    Assert.Empty(screen.Copied);
                    Assert.Equal(0, screen.Clicks);
                    Assert.Null(fixture.View.CameraPositionSet);
                }
            });
        }

        [Fact]
        public void APartialViewpointIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, false))
                {
                    Held(fixture);

                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(
                            MotionSetCameraView.ToolName,
                            ComposedScreenFixture.Arguments(
                                ComposedScreenFixture.Given("position", new object[] { 1, 2, 3 })))));
                    Assert.Equal(0, screen.Clicks);
                }
            });
        }

        private static void Held(ComposedScreenFixture fixture)
        {
            fixture.View.CameraPosition = new V3(9f, 8f, 7f);
            fixture.View.CameraTarget = new V3(0f, 1f, 0f);
            fixture.View.CameraUpVector = new V3(0f, 1f, 0f);
        }

        private static IDictionary<string, object> Call(ComposedScreenFixture fixture)
        {
            return fixture.Call(
                MotionSetCameraView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given("position", new object[] { 1, 2, 3 }),
                    ComposedScreenFixture.Given("target", new object[] { 0, 0, 0 }),
                    ComposedScreenFixture.Given("upVector", new object[] { 0, 1, 0 })));
        }

        private static void OnSta(Action action)
        {
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }

        /// <summary>
        /// PMXView の表示メニューにある、カメラ同期と4画面モードの項目。同期の項目は、エディタと同じく
        /// 押すたびに入り切りし、Shift を押して入れたときだけ、その場の PMXView の位置を写した先へ控える。
        /// </summary>
        private sealed class Screen : IDisposable
        {
            private readonly Form _view;

            internal Screen(ComposedScreenFixture fixture, bool synced)
            {
                _view = new Form
                {
                    Name = "PMXView",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                Sync = new ToolStripMenuItem("カメラ同期") { Name = "MenuItem_CameraSync", Checked = synced };
                Sync.Click += (sender, e) =>
                {
                    Clicks++;
                    Sync.Checked = !Sync.Checked;
                    if (Sync.Checked && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        Copied.Add((V3)fixture.View.CameraPositionSet);
                        if (FailOnCopy)
                        {
                            throw new InvalidOperationException("写す途中で落ちた。");
                        }
                    }
                };
                Split = new ToolStripMenuItem("4画面モード") { Name = "MenuItem_MultiView" };
                ToolStripMenuItem menu = new ToolStripMenuItem("表示") { Name = "MenuItem_View" };
                menu.DropDownItems.Add(Sync);
                menu.DropDownItems.Add(Split);
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(menu);
                _view.Controls.Add(strip);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal ToolStripMenuItem Sync { get; }

            internal ToolStripMenuItem Split { get; }

            internal int Clicks { get; private set; }

            internal bool FailOnCopy { get; set; }

            internal List<V3> Copied { get; } = new List<V3>();

            public void Dispose()
            {
                _view.Dispose();
            }
        }
    }
}
