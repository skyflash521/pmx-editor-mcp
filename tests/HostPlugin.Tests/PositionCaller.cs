using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PmxEditorMcp.Tests
{
    internal abstract class PositionCaller : IDisposable
    {
        public const string TransformViewNeed = "transformView";

        public static PositionCaller Open(string tool, string need)
        {
            if (GeneratedTools.Calls(new List<string>()).ContainsKey(tool))
            {
                return new GeneratedCaller();
            }

            ModelCaller model = new ModelCaller();
            if (model.Has(tool))
            {
                return model;
            }

            model.Dispose();

            return new ScreenCaller(need);
        }

        public abstract IDictionary<string, object> Call(string tool, IDictionary<string, object> arguments);

        public abstract object Held(string token);

        public abstract void Dispose();

        private sealed class ModelCaller : PositionCaller
        {
            private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

            public ModelCaller()
            {
                PositionPremise.Fill(_fixture.Model);
            }

            public bool Has(string tool)
            {
                try
                {
                    _fixture.Method(tool);

                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            public override IDictionary<string, object> Call(string tool, IDictionary<string, object> arguments)
            {
                return _fixture.Call(tool, arguments);
            }

            public override object Held(string token)
            {
                return _fixture.HoldModel().Value;
            }

            public override void Dispose()
            {
                _fixture.Dispose();
            }
        }

        private sealed class GeneratedCaller : PositionCaller
        {
            private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

            public GeneratedCaller()
            {
                PositionPremise.Fill(_fixture.Model);
                _fixture.Parts.MaterialItemsCount = _fixture.Model.Material.Count;
                _fixture.Parts.BoneItemsCount = _fixture.Model.Bone.Count;
                _fixture.Parts.ExpressionItemsCount = _fixture.Model.Morph.Count;
            }

            public override IDictionary<string, object> Call(string tool, IDictionary<string, object> arguments)
            {
                return _fixture.Call(tool, arguments.ToArray());
            }

            public override object Held(string token)
            {
                switch (token)
                {
                    case "@vmd":
                        return Issue(
                            "PEPlugin.Vmd.IPEVmd",
                            new FakeVmd
                            {
                                BoneNames = Names(PositionPremise.Counts["vmd.boneName"]),
                                MorphNames = Names(PositionPremise.Counts["vmd.morphName"]),
                            });
                    case "@vme":
                        return Issue(
                            "PEPlugin.Vme.IPEVme",
                            new FakeVme().Named(Names(PositionPremise.Counts["vme.bone"]), Names(PositionPremise.Counts["vme.morph"])));
                    case "@vmeResult":
                        return Issue(
                            "PEPlugin.Vme.IPEVmeResult",
                            new FakeVmeResult
                            {
                                Bones = PositionPremise.Counts["vmeResult.bone"],
                                Morphs = PositionPremise.Counts["vmeResult.morph"],
                            });
                    case "@groupBone":
                        return Issue("PEPlugin.Vme.IPEVmeGroupBone", new FakeVmeGroup(PositionPremise.Counts["vmeGroup.element"]));
                    case "@groupMorph":
                        return Issue("PEPlugin.Vme.IPEVmeGroupMorph", new FakeVmeGroup(PositionPremise.Counts["vmeGroup.element"]));
                    case "@ui":
                        FakeUiModel ui = new FakeUiModel();
                        UiModelCounts.Remember(
                            UiModelCounts.RegisterKey, new[] { "pmx" }, new object[] { PositionPremise.UiModelSource() }, ui);

                        return Issue("PXCPlugin.UIModel.IPXUIModel", ui);
                    case "@listener":
                        return Issue("PXCPlugin.Event.IPXUIModelEventListener", new FakeUiListener());
                    case "@event":
                        return Issue("PXCPlugin.Event.IPXEventConnector", new FakeEventConnector());
                    case "@ikKey":
                        return Issue("PEPlugin.Vmd.IPEVmdVisibleIKKey", new FakeVisibleIkKey());
                    case "@image":
                        using (System.Drawing.Bitmap image = new System.Drawing.Bitmap(16, 16))
                        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                        {
                            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

                            return Convert.ToBase64String(stream.ToArray());
                        }

                    case "@path":
                        return Issue(
                            "PEPlugin.Vme.IPEVmePath",
                            new VmePathPointsTests.FakeVmePath(PEPlugin.Vme.PEVmePathType.Linear, PositionPremise.Counts["vmePath.point"]));
                    case "@pose":
                        return Issue("PEPlugin.Vmd.IPEVmdBonePoseState", new FakePoseState());
                    case "@group":
                        return Issue("PEPlugin.Vme.IPEVmeGroup", new FakeVmeGroup(PositionPremise.Counts["vmeGroup.element"]));
                    default:
                        throw new NotSupportedException(token);
                }
            }

            public override void Dispose()
            {
                _fixture.Dispose();
            }

            private static string[] Names(int count)
            {
                return Enumerable.Range(0, count).Select(at => "n" + at).ToArray();
            }

            private object Issue(string type, object held)
            {
                return (long)_fixture.Handles.Issue(type, held, () => { });
            }
        }

        private sealed class ScreenCaller : PositionCaller
        {
            private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

            private Form _view;

            public ScreenCaller(string need)
            {
                PositionPremise.Fill(_fixture.Model);
                if (need == TransformViewNeed)
                {
                    _view = TransformView(_fixture.Model);
                    _fixture.Forms.Add(_view);
                }
            }

            public override IDictionary<string, object> Call(string tool, IDictionary<string, object> arguments)
            {
                return _fixture.Call(tool, arguments);
            }

            public override object Held(string token)
            {
                throw new NotSupportedException();
            }

            public override void Dispose()
            {
                if (_view != null)
                {
                    _view.Dispose();
                }

                _fixture.Dispose();
            }

            private static Form TransformView(FakePmx model)
            {
                Form view = new Form
                {
                    Name = "TransformView",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                CheckedListBox list = new CheckedListBox { Name = "clIK", CheckOnClick = true };
                int listed = 0;
                foreach (PEPlugin.Pmx.IPXBone bone in model.Bone)
                {
                    if (bone.IsIK)
                    {
                        list.Items.Add(listed + " : " + bone.Name);
                        list.SetItemChecked(listed, true);
                        listed++;
                    }
                }

                Panel panel = new Panel { Name = "panel1" };
                panel.Controls.Add(list);
                view.Controls.Add(panel);
                view.Show();

                return view;
            }
        }
    }
}
