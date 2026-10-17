using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class MotionApplyCurrentPoseTests
    {
        [Fact]
        public void WithoutAThresholdThePoseIsAppliedWithoutNormalizing()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    screen.Normalize.Checked = true;

                    ComposedScreenFixture.Value(Call(fixture, null));

                    Assert.Equal(new float?[] { null }, screen.Applied);
                    Assert.True(screen.Normalize.Checked, "正規化の入り切りを元へ戻していない。");
                }
            });
        }

        [Fact]
        public void WithAThresholdTheInputIsAnsweredAndThePoseIsAppliedWithIt()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    screen.Normalize.Checked = false;
                    string seen = "未確認";
                    screen.WhileAsking = () => seen = new DesktopModalWindowProbe(TimeSpan.FromSeconds(1)).TryDescribe();

                    ComposedScreenFixture.Value(Call(fixture, 0.001));

                    Assert.Equal(new float?[] { 0.001f }, screen.Applied);
                    Assert.False(screen.Normalize.Checked, "正規化の入り切りを元へ戻していない。");
                    Assert.Null(seen);
                }
            });
        }

        [Fact]
        public void TheInputDialogIsAnsweredWithoutShowingOnTheScreen()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    screen.Normalize.Checked = false;

                    ComposedScreenFixture.Value(Call(fixture, 0.001));

                    Assert.True(screen.AskedTransparently, "しきい値を訊く表示が透明でないまま出た。");
                }
            });
        }

        [Fact]
        public void AClosedTransformViewIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                {
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, null)));
                }
            });
        }

        [Fact]
        public void ATransformViewAlreadyShowingADialogIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    EnableWindow(screen.View.Handle, false);

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, null)));
                    Assert.Empty(screen.Applied);
                }
            });
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void AThresholdThatIsNotPositiveIsRefused(double threshold)
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Call(fixture, threshold)));
                    Assert.Empty(screen.Applied);
                }
            });
        }

        private static IDictionary<string, object> Call(ComposedScreenFixture fixture, double? threshold)
        {
            return threshold.HasValue
                ? fixture.Call(
                    MotionApplyCurrentPose.ToolName,
                    ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("morphThreshold", threshold.Value)))
                : fixture.Call(MotionApplyCurrentPose.ToolName, ComposedScreenFixture.Arguments());
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
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "表示が閉じられず、STAのスレッドが終わらなかった。");
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enable);

        private sealed class Screen : IDisposable
        {
            private readonly Form _view;

            internal Screen(ComposedScreenFixture fixture)
            {
                _view = Offscreen("TransformView");
                _view.Name = "TransformView";
                Normalize = new ToolStripMenuItem("正規化") { Name = "MenuItem_SaveNormalize", CheckOnClick = true };
                ToolStripMenuItem apply = new ToolStripMenuItem("現在の変形状態でモデル形状を更新")
                {
                    Name = "MenuItem_SetupCurrentPose",
                };
                apply.Click += (sender, e) => Apply();
                ToolStripMenuItem menu = new ToolStripMenuItem("ファイル") { Name = "MenuItem_File" };
                menu.DropDownItems.Add(Normalize);
                menu.DropDownItems.Add(apply);
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(menu);
                _view.Controls.Add(strip);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal Form View
            {
                get { return _view; }
            }

            internal ToolStripMenuItem Normalize { get; }

            internal List<float?> Applied { get; } = new List<float?>();

            internal Action WhileAsking { get; set; }

            internal bool AskedTransparently { get; private set; }

            public void Dispose()
            {
                _view.Dispose();
            }

            private void Apply()
            {
                if (!Normalize.Checked)
                {
                    Applied.Add(null);

                    return;
                }

                float threshold;
                using (Form asking = Offscreen("エディタの題と違う題"))
                {
                    asking.Name = "InputDialog";
                    TextBox text = new TextBox { Name = "txtMessage", Text = "0.00001" };
                    Button ok = new Button { Name = "btnOK", Text = "決める", DialogResult = DialogResult.OK };
                    Button cancel = new Button { Name = "btnCancel", Text = "Cancel", DialogResult = DialogResult.Cancel };
                    ok.Location = new Point(0, 30);
                    cancel.Location = new Point(80, 30);
                    asking.Controls.Add(text);
                    asking.Controls.Add(ok);
                    asking.Controls.Add(cancel);
                    asking.Shown += (sender, e) =>
                    {
                        AskedTransparently = DialogSightings.Hidden(asking.Handle);
                        WhileAsking?.Invoke();
                    };
                    if (asking.ShowDialog(_view) != DialogResult.OK)
                    {
                        return;
                    }

                    threshold = float.Parse(text.Text, CultureInfo.InvariantCulture);
                }

                using (Form progress = Offscreen("エディタの題と違う進み具合"))
                {
                    progress.Name = "CountProgressForm";
                    _view.Enabled = false;
                    progress.Show(_view);
                    for (int step = 0; step < 10; step++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(30);
                    }

                    _view.Enabled = true;
                }

                Applied.Add(threshold);
            }

            private static Form Offscreen(string text)
            {
                return new Form
                {
                    Text = text,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
            }
        }
    }
}
