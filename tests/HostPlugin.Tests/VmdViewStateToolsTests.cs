using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdViewStateToolsTests
    {
        private const string GetTool = "view_get_vmd_view_state";

        private const string SetTool = "view_set_vmd_view_state";

        [Fact]
        public void TheStateOfAnOpenVmdViewIsReturned()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.Frame.Value = 7;
                    screen.RangeStart.Text = "2";
                    screen.RangeEnd.Text = "90";
                    screen.Loop.Checked = true;

                    IDictionary<string, object> value = ComposedScreenFixture.Value(
                        fixture.Call(GetTool, ComposedScreenFixture.Arguments()));

                    Assert.Equal(7, value["frame"]);
                    Assert.Equal(120, value["lastFrame"]);
                    Assert.Equal(false, value["playing"]);
                    Assert.Equal(2, value["playRangeStart"]);
                    Assert.Equal(90, value["playRangeEnd"]);
                    Assert.Equal(true, value["loop"]);
                    Assert.Equal(false, value["startAtCurrentFrame"]);
                }
            });
        }

        [Fact]
        public void APlayingVmdViewIsReportedAsPlaying()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.FileMenu.Enabled = false;

                    IDictionary<string, object> value = ComposedScreenFixture.Value(
                        fixture.Call(GetTool, ComposedScreenFixture.Arguments()));

                    Assert.Equal(true, value["playing"]);
                }
            });
        }

        [Fact]
        public void AClosedVmdViewIsRefusedByEveryTool()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                {
                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(GetTool, ComposedScreenFixture.Arguments())));
                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 1)))));
                }
            });
        }

        [Fact]
        public void TheFrameIsMovedAndReturned()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    IDictionary<string, object> value = ComposedScreenFixture.Value(fixture.Call(
                        SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 45))));

                    Assert.Equal(45, screen.Frame.Value);
                    Assert.Equal(45, value["frame"]);
                    Assert.Equal(120, value["lastFrame"]);
                }
            });
        }

        [Fact]
        public void APlayRangeAndTheSwitchesAreSetTogether()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    IDictionary<string, object> value = ComposedScreenFixture.Value(fixture.Call(
                        SetTool,
                        ComposedScreenFixture.Arguments(
                            ComposedScreenFixture.Given("playRangeStart", 10),
                            ComposedScreenFixture.Given("playRangeEnd", 60),
                            ComposedScreenFixture.Given("loop", true),
                            ComposedScreenFixture.Given("startAtCurrentFrame", true))));

                    Assert.Equal("10", screen.RangeStart.Text);
                    Assert.Equal("60", screen.RangeEnd.Text);
                    Assert.True(screen.Loop.Checked);
                    Assert.True(screen.StartAtCurrent.Checked);
                    Assert.Equal(10, value["playRangeStart"]);
                    Assert.Equal(60, value["playRangeEnd"]);
                    Assert.Equal(true, value["loop"]);
                    Assert.Equal(true, value["startAtCurrentFrame"]);
                }
            });
        }

        [Fact]
        public void OnlyTheGivenItemsChange()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.RangeStart.Text = "5";
                    fixture.Call(
                        SetTool,
                        ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("playRangeEnd", 30)));

                    Assert.Equal("5", screen.RangeStart.Text);
                    Assert.Equal("30", screen.RangeEnd.Text);
                    Assert.False(screen.Loop.Checked);
                    Assert.Equal(0, screen.Frame.Value);
                }
            });
        }

        [Fact]
        public void AFrameBeyondTheLastFrameIsRefusedWithoutChangingAnything()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool,
                            ComposedScreenFixture.Arguments(
                                ComposedScreenFixture.Given("loop", true),
                                ComposedScreenFixture.Given("frame", 121)))));
                    Assert.Equal(0, screen.Frame.Value);
                    Assert.False(screen.Loop.Checked);
                }
            });
        }

        [Fact]
        public void APlayRangeThatEndsBeforeItStartsIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool,
                            ComposedScreenFixture.Arguments(
                                ComposedScreenFixture.Given("playRangeStart", 50),
                                ComposedScreenFixture.Given("playRangeEnd", 10)))));
                    Assert.Equal("0", screen.RangeStart.Text);
                    Assert.Equal("120", screen.RangeEnd.Text);
                }
            });
        }

        [Fact]
        public void APlayRangeEndBeyondTheLastFrameIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool,
                            ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("playRangeEnd", 121)))));
                    Assert.Equal("120", screen.RangeEnd.Text);
                }
            });
        }

        [Fact]
        public void NothingIsChangedWhilePlaying()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.FileMenu.Enabled = false;

                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 3)))));
                    Assert.Equal(0, screen.Frame.Value);
                }
            });
        }

        [Fact]
        public void ACallWithoutAnyItemChangesNothingAndReturnsTheState()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.Frame.Value = 4;

                    IDictionary<string, object> value = ComposedScreenFixture.Value(
                        fixture.Call(SetTool, ComposedScreenFixture.Arguments()));

                    Assert.Equal(4, screen.Frame.Value);
                    Assert.Equal(4, value["frame"]);
                    Assert.Equal("120", screen.RangeEnd.Text);
                }
            });
        }

        [Fact]
        public void ItemsOfTheWrongKindAreRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    foreach (KeyValuePair<string, object> wrong in new[]
                    {
                        ComposedScreenFixture.Given("frame", -1),
                        ComposedScreenFixture.Given("frame", 1.5),
                        ComposedScreenFixture.Given("frame", "1"),
                        ComposedScreenFixture.Given("playRangeStart", -1),
                        ComposedScreenFixture.Given("playRangeEnd", "9"),
                        ComposedScreenFixture.Given("loop", 1),
                        ComposedScreenFixture.Given("startAtCurrentFrame", "true"),
                    })
                    {
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(fixture.Call(SetTool, ComposedScreenFixture.Arguments(wrong))));
                    }

                    Assert.Equal(0, screen.Frame.Value);
                }
            });
        }

        [Fact]
        public void AnUnreadablePlayRangeFieldIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.RangeEnd.Text = "x";

                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(GetTool, ComposedScreenFixture.Arguments())));
                }
            });
        }

        [Fact]
        public void AnEventDrivenVmdViewRefusesToMoveTheFrame()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.FrameRateMenu.Enabled = false;

                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 3)))));
                    Assert.Equal(0, screen.Frame.Value);
                    Assert.True(ComposedScreenFixture.Value(
                        fixture.Call(GetTool, ComposedScreenFixture.Arguments())).ContainsKey("frame"));
                }
            });
        }

        [Fact]
        public void BothPlayRangeItemsAreSetOverUnreadableFields()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.RangeStart.Text = "x";
                    screen.RangeEnd.Text = "y";

                    IDictionary<string, object> value = ComposedScreenFixture.Value(fixture.Call(
                        SetTool,
                        ComposedScreenFixture.Arguments(
                            ComposedScreenFixture.Given("playRangeStart", 10),
                            ComposedScreenFixture.Given("playRangeEnd", 60))));

                    Assert.Equal("10", screen.RangeStart.Text);
                    Assert.Equal("60", screen.RangeEnd.Text);
                    Assert.Equal(10, value["playRangeStart"]);
                    Assert.Equal(60, value["playRangeEnd"]);
                }
            });
        }

        [Fact]
        public void AnUnreadableFieldIsRefusedWhenItsCurrentValueIsNeeded()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.RangeStart.Text = "x";

                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        ComposedScreenFixture.Code(fixture.Call(
                            SetTool,
                            ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("playRangeEnd", 60)))));
                    Assert.Equal("120", screen.RangeEnd.Text);
                }
            });
        }

        [Fact]
        public void TheRefusalForAnEventDrivenVmdViewNamesTheWayOut()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.FrameRateMenu.Enabled = false;

                    string text = ComposedEditFixture.Message(fixture.Call(
                        SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 3))));

                    Assert.Contains("view_load_vmd_view", text);
                }
            });
        }

        [Fact]
        public void ADisabledVmdViewIsNotReportedAsPlayingAndNamesWhatToWaitFor()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.View.Enabled = false;

                    Assert.Equal(
                        false,
                        ComposedScreenFixture.Value(fixture.Call(GetTool, ComposedScreenFixture.Arguments()))["playing"]);
                    IDictionary<string, object> refused = fixture.Call(
                        SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 3)));
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(refused));
                    Assert.Contains("映像出力", ComposedEditFixture.Message(refused));
                    Assert.DoesNotContain("view_stop_vmd_view", ComposedEditFixture.Message(refused));
                }
            });
        }

        [Fact]
        public void AVmdViewBuildingAFixMotionIsNotReportedAsPlayingAndNamesWhatToWaitFor()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.Group.Enabled = false;
                    screen.FileMenu.Enabled = false;

                    Assert.Equal(
                        false,
                        ComposedScreenFixture.Value(fixture.Call(GetTool, ComposedScreenFixture.Arguments()))["playing"]);
                    IDictionary<string, object> refused = fixture.Call(
                        SetTool, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 3)));
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(refused));
                    Assert.Contains("Fixモーション", ComposedEditFixture.Message(refused));
                    Assert.DoesNotContain("view_stop_vmd_view", ComposedEditFixture.Message(refused));
                }
            });
        }

        [Fact]
        public void TheRefusalForAnUnreadablePlayRangeNamesTheWayOut()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    screen.RangeEnd.Text = "x";

                    Assert.Contains(
                        "playRangeStart",
                        ComposedEditFixture.Message(fixture.Call(GetTool, ComposedScreenFixture.Arguments())));
                }
            });
        }

        [Fact]
        public void TheLastFrameItselfIsAcceptedForTheFrameAndThePlayRangeEnd()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 120))
                {
                    IDictionary<string, object> value = ComposedScreenFixture.Value(fixture.Call(
                        SetTool,
                        ComposedScreenFixture.Arguments(
                            ComposedScreenFixture.Given("frame", 120),
                            ComposedScreenFixture.Given("playRangeEnd", 120))));

                    Assert.Equal(120, screen.Frame.Value);
                    Assert.Equal("120", screen.RangeEnd.Text);
                    Assert.Equal(120, value["frame"]);
                    Assert.Equal(120, value["playRangeEnd"]);
                }
            });
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

        private sealed class Screen : IDisposable
        {
            private readonly Form _view;

            internal Screen(ComposedScreenFixture fixture, int lastFrame)
            {
                _view = new Form
                {
                    Name = "VMDViewForm",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                Frame = new TrackBar { Name = "trkFrame", Minimum = 0, Maximum = lastFrame };
                RangeStart = new TextBox { Name = "txtPlayRangeSt", Text = "0" };
                RangeEnd = new TextBox { Name = "txtPlayRangeEd", Text = lastFrame.ToString() };
                Loop = new CheckBox { Name = "chkLoop" };
                StartAtCurrent = new CheckBox { Name = "chkCurStart" };
                Panel inner = new Panel { Name = "pnlControlWithoutPlay" };
                inner.Controls.AddRange(new Control[] { Frame, RangeStart, RangeEnd, Loop, StartAtCurrent });
                Group = new Panel { Name = "pnlControlGroup" };
                Group.Controls.Add(inner);
                _view.Controls.Add(Group);
                FileMenu = new ToolStripMenuItem { Name = "MenuItem_File" };
                FrameRateMenu = new ToolStripMenuItem { Name = "MenuItem_FPS_Frame30" };
                ToolStripMenuItem info = new ToolStripMenuItem { Name = "MenuItem_Info" };
                info.DropDownItems.Add(FrameRateMenu);
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(FileMenu);
                strip.Items.Add(info);
                _view.Controls.Add(strip);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal Form View
            {
                get { return _view; }
            }

            internal Panel Group { get; }

            internal TrackBar Frame { get; }

            internal TextBox RangeStart { get; }

            internal TextBox RangeEnd { get; }

            internal CheckBox Loop { get; }

            internal CheckBox StartAtCurrent { get; }

            internal ToolStripMenuItem FileMenu { get; }

            internal ToolStripMenuItem FrameRateMenu { get; }

            public void Dispose()
            {
                _view.Dispose();
            }
        }
    }
}
