using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class MotionSetTransformViewIkTests
    {
        private const string ToolName = "motion_set_transform_view_ik";

        [Fact]
        public void EveryIkIsSwitchedWhenNoBoneIsGiven()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    IDictionary<string, object> value = ComposedScreenFixture.Value(Call(fixture, false, null));

                    Assert.Equal(new[] { false, false }, screen.Enabled);
                    Assert.Equal(
                        new[] { Entry(1, false), Entry(3, false) },
                        States(value));
                }
            });
        }

        [Fact]
        public void SwitchingTwiceTakesOneClone()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    ComposedScreenFixture.Value(Call(fixture, false, null));
                    ComposedScreenFixture.Value(Call(fixture, true, null));

                    Assert.Equal(1, fixture.Clones);
                }
            });
        }

        [Fact]
        public void OnlyTheGivenIkBoneIsSwitchedAndTheOthersStay()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    IDictionary<string, object> value =
                        ComposedScreenFixture.Value(Call(fixture, false, new object[] { 3 }));

                    Assert.Equal(new[] { true, false }, screen.Enabled);
                    Assert.Equal(new[] { Entry(3, false) }, States(value));
                }
            });
        }

        [Fact]
        public void AnIkSwitchedOffIsSwitchedBackOn()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    ComposedScreenFixture.Value(Call(fixture, false, null));

                    IDictionary<string, object> value =
                        ComposedScreenFixture.Value(Call(fixture, true, new object[] { 1 }));

                    Assert.Equal(new[] { true, false }, screen.Enabled);
                    Assert.Equal(new[] { Entry(1, true) }, States(value));
                }
            });
        }

        [Fact]
        public void ABoneWithoutAnIkIsRefusedWithoutSwitchingAnything()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(Call(fixture, false, new object[] { 3, 2 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void ABoneOutsideTheModelIsRefusedWithoutSwitchingAnything()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    Assert.Equal(
                        ToolEnvelope.IndexOutOfRange,
                        ComposedScreenFixture.Code(Call(fixture, false, new object[] { 1, 99 })));
                    Assert.Equal(
                        ToolEnvelope.IndexOutOfRange,
                        ComposedScreenFixture.Code(Call(fixture, false, new object[] { -1 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void AListThatDoesNotMatchTheModelsIkCountIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 3))
                {
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, null)));
                    Assert.Equal(new[] { true, true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void AListItemThatMerelyEndsWithTheModelsBoneNameIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, "旧"))
                {
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 1 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Theory]
        [InlineData("旧・")]
        [InlineData("旧 ")]
        [InlineData("旧-")]
        [InlineData("★")]
        [InlineData("＋")]
        public void AListItemWhoseLongerNameMeetsTheModelsNameAtASymbolOrSpaceIsRefused(string decorate)
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, decorate))
                {
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 1 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void AnEmptyNamedBoneIsToldApartFromAListItemEndingInASymbol()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, null, true))
                {
                    screen.Rename(0, "旧ＩＫ＋");

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 3 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void AnEmptyNamedBoneIsToldApartFromAListItemNamingOnlyASymbol()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, null, true))
                {
                    screen.Rename(0, "★");

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 3 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void ABoneWithAnEmptyNameIsToldApartFromAListItemNamingAnotherBone()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, null, true))
                {
                    screen.Rename(0, "旧");

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 3 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void ABoneWithAnEmptyNameIsSwitchedWhenTheListItemNamesNothing()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, false, null, true))
                {
                    ComposedScreenFixture.Value(Call(fixture, false, new object[] { 1 }));

                    Assert.Equal(new[] { false, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void AListWhoseIkBonesAreNotTheModelsIsRefusedEvenWhenTheCountMatches()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2, true))
                {
                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, new object[] { 3 })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
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
                    Model(fixture);

                    Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, false, null)));
                }
            });
        }

        [Fact]
        public void AMissingOrNonBooleanEnabledIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(ToolName, ComposedScreenFixture.Arguments())));
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(fixture.Call(
                            ToolName, ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("enabled", 0)))));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        [Fact]
        public void BoneIndicesThatAreNotIntegersAreRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture, 2))
                {
                    Assert.Equal(
                        ToolEnvelope.InvalidArgument,
                        ComposedScreenFixture.Code(Call(fixture, false, new object[] { "1" })));
                    Assert.Equal(new[] { true, true }, screen.Enabled);
                }
            });
        }

        private static IDictionary<string, object> Call(
            ComposedScreenFixture fixture, bool enabled, object[] boneIndices)
        {
            return boneIndices == null
                ? fixture.Call(
                    ToolName,
                    ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("enabled", enabled)))
                : fixture.Call(
                    ToolName,
                    ComposedScreenFixture.Arguments(
                        ComposedScreenFixture.Given("enabled", enabled),
                        ComposedScreenFixture.Given("boneIndices", boneIndices)));
        }

        private static void Model(ComposedScreenFixture fixture)
        {
            fixture.Model.Bone.Add(new FakeBone("センター"));
            fixture.Model.Bone.Add(new FakeBone("左足ＩＫ") { IsIK = true });
            fixture.Model.Bone.Add(new FakeBone("左ひざ"));
            fixture.Model.Bone.Add(new FakeBone("右足ＩＫ") { IsIK = true });
        }

        private static string Entry(int boneIndex, bool enabled)
        {
            return boneIndex + ":" + enabled;
        }

        private static string[] States(IDictionary<string, object> value)
        {
            object[] iks = (object[])value["iks"];
            string[] states = new string[iks.Length];
            for (int at = 0; at < iks.Length; at++)
            {
                IDictionary<string, object> ik = (IDictionary<string, object>)iks[at];
                states[at] = Entry((int)ik["boneIndex"], (bool)ik["enabled"]);
            }

            return states;
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

            private bool _building = true;

            private readonly CheckedListBox _list;

            internal Screen(
                ComposedScreenFixture fixture, int listed, bool stale = false, string decorate = null, bool emptyFirst = false)
            {
                Model(fixture);
                if (emptyFirst)
                {
                    fixture.Model.Bone[1].Name = string.Empty;
                }

                _view = new Form
                {
                    Name = "TransformView",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                Enabled = new bool[listed];
                CheckedListBox list = new CheckedListBox { Name = "clIK", CheckOnClick = true };
                list.ItemCheck += (sender, e) =>
                {
                    if (!_building)
                    {
                        Enabled[e.Index] = e.NewValue == CheckState.Checked;
                    }
                };
                _list = list;
                // 一覧の並びは、IKを持つボーンをボーンの並びの順に並べたもの。
                string[] names = { fixture.Model.Bone[1].Name, fixture.Model.Bone[3].Name };
                for (int at = 0; at < listed; at++)
                {
                    list.Items.Add(at + " : " + (stale ? "古いＩＫ" : (at == 0 ? decorate ?? string.Empty : string.Empty) + (at < names.Length ? names[at] : "IK")));
                    list.SetItemChecked(at, true);
                    Enabled[at] = true;
                }

                _building = false;
                Panel panel = new Panel { Name = "panel1" };
                panel.Controls.Add(list);
                _view.Controls.Add(panel);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal bool[] Enabled { get; }

            internal void Rename(int position, string name)
            {
                _list.Items[position] = position + " : " + name;
            }

            public void Dispose()
            {
                _view.Dispose();
            }
        }
    }
}
