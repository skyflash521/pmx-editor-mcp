using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PEPlugin;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>PMXのデータを相手にするツールの振り分け。複製編集型は反映まで1回で閉じる。</summary>
    public sealed class PmxToolDispatchTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string SdkVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private const string ConnectorType = "Sdk.PmxConnector";

        private const string SetMarksKey = "Sdk.PmxConnector.SetMarks(System.Int32[])";

        private const string GetMarksKey = "Sdk.PmxConnector.GetMarks()";

        private const string StateReadKey = "Sdk.PmxConnector.GetCurrentState()";

        private const string CommitKey = "Sdk.PmxConnector.Update(Sdk.Pmx)";

        private const string FilePathKey = "Sdk.Pmx.FilePath()";

        private const string ListKey = "Sdk.Pmx.Vertex()";

        private const string ClearKey = "Sdk.Pmx.Clear()";

        private const string CompactKey = "Sdk.Pmx.Compact()";

        private const string MaterialsKey = "Sdk.Pmx.Material()";

        private const string FacesKey = "PEPlugin.Pmx.IPXMaterial.Faces()";

        private const string MorphsKey = "PEPlugin.Pmx.IPXPmx.Morph()";

        private const string PanelKey = "PEPlugin.Pmx.IPXMorph.Panel()";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly Model _model = new Model();

        /// <summary>現在のPMXとして返すモデル。null なら <see cref="_model"/> を返す。</summary>
        private FakePmx _pmx;

        private readonly FakePmxView _view = new FakePmxView();

        private readonly FakeFormConnector _form = new FakeFormConnector();

        private int _commits;

        /// <summary>反映の行を呼んだ回数。反映が落ちた回も数える。</summary>
        private int _reflections;

        private int _clones;

        private int[] _marks = new int[0];

        private bool _marksReadable = true;

        private bool _reflectionBreaks;

        public PmxToolDispatchTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-pmx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void TheListingReturnsTheTotalAndTheOneItem()
        {
            _model.FilePath = @"C:\models\a.pmx";

            IDictionary<string, object> value = Value(Call("model_list_pmxes", Arguments()));

            Assert.Equal(1, value[ToolDispatch.TotalName]);
            IDictionary<string, object> item =
                (IDictionary<string, object>)((object[])value[ToolDispatch.ItemsName])[0];
            Assert.Equal(@"C:\models\a.pmx", item["filePath"]);
        }

        [Fact]
        public void TheFacesTheScreenPicksAreListedAtTheirPlaceInTheirMaterial()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            for (int at = 0; at < 3; at++)
            {
                first.Faces.Add(new FakeFace());
            }

            for (int at = 0; at < 2; at++)
            {
                second.Faces.Add(new FakeFace());
            }

            _model.Materials.Add(first);
            _model.Materials.Add(second);
            // 画面は選んだ面を3つの頂点の位置の組で持つ。通し番号4と1の面を選ぶ。
            _view.Selected[ElementKinds.Face] = new[] { 12, 13, 14, 3, 4, 5 };

            IDictionary<string, object> value = Value(Call(
                "model_list_faces", Arguments("parentAll", true, "selected", true)));
            object[] items = (object[])value[ToolDispatch.ItemsName];

            Assert.Equal(2, items.Length);
            Assert.Equal(1, ((IDictionary<string, object>)items[0])["indexInParent"]);
            Assert.Equal(1, ((IDictionary<string, object>)items[0])["parentIndex"]);
            Assert.Equal(1, ((IDictionary<string, object>)items[1])["indexInParent"]);
            Assert.Equal(0, ((IDictionary<string, object>)items[1])["parentIndex"]);
        }

        [Fact]
        public void TheFacesTheScreenPicksOutsideThePointedMaterialsAreLeftOut()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            _model.Materials.Add(first);
            _model.Materials.Add(second);
            _view.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 3, 4, 5 };

            IDictionary<string, object> value = Value(Call(
                "model_list_faces", Arguments("parentIndices", new object[] { 1 }, "selected", true)));
            object[] items = (object[])value[ToolDispatch.ItemsName];

            Assert.Single(items);
            Assert.Equal(0, ((IDictionary<string, object>)items[0])["indexInParent"]);
            Assert.Equal(1, ((IDictionary<string, object>)items[0])["parentIndex"]);
        }

        [Fact]
        public void TheFacesTheScreenPicksOnlyInOtherMaterialsListNothing()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            _model.Materials.Add(first);
            _model.Materials.Add(second);
            _view.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = Value(Call(
                "model_list_faces", Arguments("parentIndices", new object[] { 1 }, "selected", true)));

            Assert.Empty((object[])value[ToolDispatch.ItemsName]);
        }

        [Fact]
        public void ListingWithOnlyTheParentPointedListsEveryOneUnderIt()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            _model.Materials.Add(first);
            _model.Materials.Add(second);

            IDictionary<string, object> value = Value(Call(
                "model_list_faces", Arguments("parentIndices", new object[] { 1 })));
            object[] items = (object[])value[ToolDispatch.ItemsName];

            Assert.Equal(2, value[ToolDispatch.TotalName]);
            Assert.Equal(2, items.Length);
            Assert.All(
                items,
                item => Assert.Equal(1, ((IDictionary<string, object>)item)["parentIndex"]));
        }

        [Fact]
        public void SettingASelectionAnswersHowManyAreSelectedAfterward()
        {
            IDictionary<string, object> value = Value(Call(
                "view_set_marks", Arguments("indices", new object[] { 3, 1, 3 })));

            Assert.Equal(2, value["selected"]);
        }

        [Fact]
        public void ASelectionThatCannotBeReadBackIsNotAnsweredAsEmpty()
        {
            _marksReadable = false;

            IDictionary<string, object> envelope = Call(
                "view_set_marks", Arguments("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
        }

        [Fact]
        public void ListingWithOnlyAllListsEveryOneUnderEveryParent()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            _model.Materials.Add(first);
            _model.Materials.Add(second);

            IDictionary<string, object> value = Value(Call("model_list_faces", Arguments("all", true)));

            Assert.Equal(3, value[ToolDispatch.TotalName]);
            Assert.Equal(3, ((object[])value[ToolDispatch.ItemsName]).Length);
        }

        [Fact]
        public void ListingWithNeitherTheParentNorTheElementsPointedIsRefused()
        {
            FakeMaterial first = new FakeMaterial("一");
            first.Faces.Add(new FakeFace());
            _model.Materials.Add(first);

            IDictionary<string, object> envelope = Call("model_list_faces", Arguments());

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheScreenSelectionTogetherWithPositionsIsRefusedEvenWhenItLiesElsewhere()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(new FakeFace());
            second.Faces.Add(new FakeFace());
            _model.Materials.Add(first);
            _model.Materials.Add(second);
            _view.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> envelope = Call(
                "model_list_faces",
                Arguments(
                    "parentIndices", new object[] { 1 },
                    "selected", true,
                    "indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void APositionPastTheEndReturnsAnEmptyListing()
        {
            IDictionary<string, object> value = Value(
                Call("model_list_pmxes", Arguments(ToolDispatch.OffsetName, 1)));

            Assert.Equal(1, value[ToolDispatch.TotalName]);
            Assert.Empty((object[])value[ToolDispatch.ItemsName]);
        }

        [Fact]
        public void ACountBelowOneIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_list_pmxes", Arguments(ToolDispatch.LimitName, 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheReflectedChangeIsShownOnTheScreen()
        {
            Call("model_update_pmxes", Arguments(ToolDispatch.ValueName, Value("filePath", "b.pmx")));

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, _form.Updated);
            Assert.Equal(1, _view.Redraws);
            Assert.Equal(1, _view.Repaints);
        }

        [Fact]
        public void AReflectedChangeThatCannotBeShownStillCountsAsDoneAndSaysSoInAWarning()
        {
            _view.RefusesToPaint = true;

            IDictionary<string, object> envelope = Call(
                "model_update_pmxes", Arguments(ToolDispatch.ValueName, Value("filePath", "b.pmx")));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal("b.pmx", _model.FilePath);
            Assert.Contains(
                ScreenRefresh.NotShownWarning,
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
        }

        [Fact]
        public void HidingAMorphOnTheExpressionNodeIsWarnedAbout()
        {
            FakeMorph morph = ExpressionMorph("笑い", 4);

            IDictionary<string, object> envelope = Call(
                "model_update_morphs",
                Arguments(
                    SetResponse.IndicesName, new object[] { 0 },
                    ToolDispatch.ValueName, Value("panel", 0)));

            Assert.Equal(0, FakeEditorState.Now(_pmx, morph).Panel);
            Assert.Equal(1, _commits);
            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("「笑い」"));
        }

        [Fact]
        public void AHiddenMorphAlreadyOnTheExpressionNodeIsNotWarnedAboutAgain()
        {
            ExpressionMorph("笑い", 0);

            IDictionary<string, object> envelope = Call(
                "model_update_morphs",
                Arguments(
                    SetResponse.IndicesName, new object[] { 0 },
                    ToolDispatch.ValueName, Value("panel", 0)));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName), "警告が付いている。");
        }

        [Fact]
        public void ShowingAMorphOnTheExpressionNodeIsNotWarnedAbout()
        {
            FakeMorph morph = ExpressionMorph("笑い", 0);

            IDictionary<string, object> envelope = Call(
                "model_update_morphs",
                Arguments(
                    SetResponse.IndicesName, new object[] { 0 },
                    ToolDispatch.ValueName, Value("panel", 4)));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName), "警告が付いている。");
            Assert.Equal(4, FakeEditorState.Now(_pmx, morph).Panel);
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void TheUpdateWritesTheValueAndReflectsItOnce()
        {
            IDictionary<string, object> envelope = Call(
                "model_update_pmxes", Arguments(ToolDispatch.ValueName, Value("filePath", "b.pmx")));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("b.pmx", _model.FilePath);
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void AnItemTheUpdateDoesNotCarryIsRefusedWithoutWriting()
        {
            IDictionary<string, object> envelope = Call(
                "model_update_pmxes", Arguments(ToolDispatch.ValueName, Value("depth", 1)));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_model.FilePath);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void ACallOnTheCurrentModelReflectsAfterItRuns()
        {
            IDictionary<string, object> envelope = Call(
                "model_clear_pmx", Arguments(ToolDispatch.ConfirmName, true));

            Assert.True((bool)envelope["ok"]);
            Assert.True(_model.Cleared);
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void ACallOnTheCurrentModelWithoutTheConfirmationIsRefused()
        {
            IDictionary<string, object> envelope = Call("model_clear_pmx", Arguments());

            Assert.Equal(ToolEnvelope.ConfirmRequired, Code(envelope));
            Assert.False(_model.Cleared);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void ACallOnAModelTheRequestPointsAtDoesNotAskForTheConfirmation()
        {
            HandleLedger handles = Ledger();
            Model held = new Model();
            int handle = handles.Issue(typeof(Model).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_clear_pmx", Arguments(PmxSession.HandleName, handle), handles);

            Assert.True((bool)envelope["ok"]);
            Assert.True(held.Cleared);
            Assert.False(_model.Cleared);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void AnUpdateOnAModelTheRequestPointsAtWritesWithoutReflecting()
        {
            HandleLedger handles = Ledger();
            Model held = new Model();
            int handle = handles.Issue(typeof(Model).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_update_pmxes",
                Arguments(
                    PmxSession.HandleName, handle, ToolDispatch.ValueName, Value("filePath", "c.pmx")),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("c.pmx", held.FilePath);
            Assert.Null(_model.FilePath);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void RemovingEverythingFromAModelTheRequestPointsAtEmptiesIt()
        {
            HandleLedger handles = Ledger();
            Model held = new Model();
            held.Items.Add(new Item());
            held.Items.Add(new Item());
            int handle = handles.Issue(typeof(Model).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_remove_vertices",
                Arguments(PmxSession.HandleName, handle, TargetNames.Element.All, true),
                handles);

            Assert.Equal(2, Value(envelope)[SetResponse.RemovedName]);
            Assert.Empty(held.Items);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void RemovingOnePositionFromAModelTheRequestPointsAtTakesItOut()
        {
            HandleLedger handles = Ledger();
            Model held = new Model();
            held.Items.Add(new Item());
            int handle = handles.Issue(typeof(Model).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_remove_vertices",
                Arguments(
                    PmxSession.HandleName, handle,
                    TargetNames.Element.Indices, new object[] { 0 }),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.RemovedName]);
            Assert.Empty(held.Items);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void AddingDoesNotTakeTheSwitchOfWhichModelToSee()
        {
            IDictionary<string, object> envelope = Call(
                "model_add_vertices",
                Arguments(
                    PmxSession.HandleName, 1, TargetNames.Element.Handles, new object[] { 1 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheDuplicateOfTheCurrentModelIsNotTheOneTheListingReads()
        {
            _clones = 0;

            Call("model_list_pmxes", Arguments());

            Assert.Equal(1, _clones);
        }

        [Fact]
        public void AFailureWhileChangingTheDuplicateAnswersThatNothingChanged()
        {
            IDictionary<string, object> envelope = Call("model_compact_pmx", Arguments());

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("SDKが落ちた。", Message(envelope));
            Assert.Contains("未変更", Message(envelope));
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void AFailureOnAModelTheRequestPointsAtLeavesTheResultUnknown()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Model).FullName, new Model(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_compact_pmx", Arguments(PmxSession.HandleName, handle), handles);

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("結果不明", Message(envelope));
        }

        [Fact]
        public void AFailureWhileReflectingLeavesTheResultUnknown()
        {
            _reflectionBreaks = true;

            IDictionary<string, object> envelope = Call(
                "model_clear_pmx", Arguments(ToolDispatch.ConfirmName, true));

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("結果不明", Message(envelope));
            Assert.Equal(1, _reflections);
        }

        [Fact]
        public void AHandleTheLedgerDoesNotCarryIsRefusedAsAnInvalidHandle()
        {
            IDictionary<string, object> envelope = Call(
                "model_list_pmxes", Arguments(PmxSession.HandleName, 1));

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
        }

        [Fact]
        public void AddingPutsTheHeldElementAtTheEndAndConsumesTheHandle()
        {
            HandleLedger handles = Ledger();
            _model.Items.Add(new Item { Name = "元" });
            Item item = new Item { Name = "預けた" };
            int handle = handles.Issue(typeof(Item).FullName, item, () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_vertices",
                Arguments(TargetNames.Element.Handles, new object[] { handle }),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.AddedName]);
            Assert.Equal(new[] { 1 }, (int[])Value(envelope)[SetResponse.IndicesName]);
            Assert.Equal(new[] { "元", "預けた" }, ItemNames());
            Assert.False(handles.IsValid(handle));
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void AddingAHandleTheLedgerDoesNotCarryPutsNothingIn()
        {
            IDictionary<string, object> envelope = Call(
                "model_add_vertices",
                Arguments(TargetNames.Element.Handles, new object[] { 1 }));

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
            Assert.Empty(_model.Items);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void RemovingTakesOutThePositionsItIsGiven()
        {
            _model.Items.Add(new Item { Name = "一" });
            _model.Items.Add(new Item { Name = "二" });
            _model.Items.Add(new Item { Name = "残す" });

            IDictionary<string, object> envelope = Call(
                "model_remove_vertices",
                Arguments(TargetNames.Element.Indices, new object[] { 0, 1 }));

            Assert.Equal(2, Value(envelope)[SetResponse.RemovedName]);
            Assert.Equal(new[] { "残す" }, ItemNames());
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void RemovingAPositionOutsideTheListIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_remove_vertices",
                Arguments(TargetNames.Element.Indices, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, Code(envelope));
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void RemovingWithoutAWayToPointIsRefused()
        {
            IDictionary<string, object> envelope = Call("model_remove_vertices", Arguments());

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void RemovingEverythingEmptiesTheList()
        {
            _model.Items.Add(new Item());
            _model.Items.Add(new Item());

            IDictionary<string, object> envelope = Call(
                "model_remove_vertices", Arguments(TargetNames.Element.All, true));

            Assert.Equal(2, Value(envelope)[SetResponse.RemovedName]);
            Assert.Empty(_model.Items);
            Assert.Equal(1, _commits);
            Assert.Equal(1, _clones);
        }

        private static IDictionary<string, object> Arguments(params object[] pairs)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            for (int at = 0; at < pairs.Length; at += 2)
            {
                arguments.Add((string)pairs[at], pairs[at + 1]);
            }

            return arguments;
        }

        /// <summary>現在のPMXを、表情枠にモーフを1つだけ載せたモデルにする。</summary>
        private FakeMorph ExpressionMorph(string name, int panel)
        {
            FakeMorph morph = new FakeMorph(name) { Panel = panel };
            _pmx = new FakePmx();
            _pmx.Morph.Add(morph);
            _pmx.ExpressionNode.Items.Add(new FakeMorphNodeItem(morph));

            return morph;
        }

        private static IDictionary<string, object> Value(string name, object value)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { name, value } };
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["code"];
        }

        private static string Message(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["message"];
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            Assert.True((bool)envelope["ok"], "包みが成功でない。");

            return (IDictionary<string, object>)envelope["value"];
        }

        private IDictionary<string, object> Call(
            string tool, IDictionary<string, object> arguments, HandleLedger handles = null)
        {
            McpMethodTable methods = new McpMethodTable();
            SdkRelayTable relay = Relay();
            IDictionary<string, SdkReceiver> receivers =
                new Dictionary<string, SdkReceiver>(StringComparer.Ordinal)
                {
                    { ConnectorType, connection => new object() },
                };
            ResidentConnection connection = Connection();
            PmxSession session = Session(relay, receivers, connection);
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                Lists(),
                connection,
                session,
                Session(relay, receivers, connection),
                new UndoRecovery(new UndoSuppression(_log), session.UndoLock),
                Calls(),
                Aggregations(),
                Elements(),
                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal),
                new StillModifierKeys(),
                EventBindingFixture.Empty(),
                Refresh(),
                Screen(),
                new Dictionary<string, Func<object, object, object, IDictionary<string, object>>>(StringComparer.Ordinal));

            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていないツール: " + tool);

            return (IDictionary<string, object>)method(
                new McpMethodContext(
                    arguments,
                    new InlineInvoker(),
                    100000,
                    handles ?? Ledger(),
                    new EventQueue(new EventSequenceIssuer())));
        }

        /// <summary>題材の複製編集の流れ。受け手を取り、複製を1つだけ渡す形とする。</summary>
        private PmxSession Session(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection)
        {
            return new PmxSession(
                relay,
                receivers,
                connection,
                new PmxFlow(
                    StateReadKey,
                    CommitKey,
                    ConnectorType,
                    new FlowSlot[0],
                    new[] { FlowSlot.Pmx }),
                typeof(Model),
                new UndoSuppression(_log));
        }

        private HandleLedger Ledger()
        {
            return new HandleLedger(_log, new HandleIdIssuer());
        }

        private ResidentConnection Connection()
        {
            return ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(
                        new StubConnector(
                            new StubSystemConnector(
                                new StubCPluginRunArgs(new StubCPluginConnector())))),
                    ModulePath),
                _log);
        }

        private SdkRelayTable Relay()
        {
            Dictionary<string, SdkCall> calls =
                new Dictionary<string, SdkCall>(StringComparer.Ordinal)
                {
                    {
                        SetMarksKey,
                        (target, arguments) =>
                        {
                            _marks = ((int[])arguments[0]).Distinct().ToArray();
                            return null;
                        }
                    },
                    {
                        GetMarksKey,
                        (target, arguments) => _marksReadable ? _marks : null
                    },
                    {
                        StateReadKey,
                        (target, arguments) =>
                        {
                            _clones++;
                            return _pmx != null
                                ? FakeEditorState.Duplicate(_pmx)
                                : (object)_model.Duplicate();
                        }
                    },
                    {
                        CommitKey,
                        (target, arguments) =>
                        {
                            _reflections++;
                            if (_reflectionBreaks)
                            {
                                throw new InvalidOperationException("反映が落ちた。");
                            }

                            _commits++;
                            if (_pmx != null)
                            {
                                FakeEditorState.Reflect(
                                    _pmx,
                                    (PEPlugin.Pmx.IPXPmx)arguments[0],
                                    PEPlugin.Pmx.PmxUpdateObject.All,
                                    -1);
                            }
                            else
                            {
                                _model.Take((Model)arguments[0]);
                            }

                            return null;
                        }
                    },
                    {
                        CompactKey,
                        (target, arguments) =>
                        {
                            throw new InvalidOperationException("SDKが落ちた。");
                        }
                    },
                    {
                        FilePathKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Model)target).FilePath
                            : Write((Model)target, (string)arguments[0])
                    },
                    {
                        ClearKey,
                        (target, arguments) =>
                        {
                            ((Model)target).Cleared = true;
                            return null;
                        }
                    },
                    {
                        PanelKey,
                        (target, arguments) =>
                        {
                            PEPlugin.Pmx.IPXMorph morph = (PEPlugin.Pmx.IPXMorph)target;
                            if (arguments.Length == 0)
                            {
                                return morph.Panel;
                            }

                            morph.Panel = Convert.ToInt32(arguments[0]);
                            return null;
                        }
                    },
                };

            return new SdkRelayTable(SdkVersion, Digest, calls, new string[0]);
        }

        private static object Write(Model model, string path)
        {
            model.FilePath = path;

            return null;
        }

        private static IDictionary<string, SdkList> Lists()
        {
            return new Dictionary<string, SdkList>(StringComparer.Ordinal)
            {
                {
                    MaterialsKey,
                    new SdkList(
                        owner => ((Model)owner).Materials.Count,
                        (owner, index) => ((Model)owner).Materials[index],
                        (owner, item) => ((Model)owner).Materials.Add((FakeMaterial)item),
                        (owner, index) => ((Model)owner).Materials.RemoveAt(index))
                },
                {
                    FacesKey,
                    new SdkList(
                        owner => ((FakeMaterial)owner).Faces.Count,
                        (owner, index) => ((FakeMaterial)owner).Faces[index],
                        (owner, item) => ((FakeMaterial)owner).Faces.Add((PEPlugin.Pmx.IPXFace)item),
                        (owner, index) => ((FakeMaterial)owner).Faces.RemoveAt(index))
                },
                {
                    ListKey,
                    new SdkList(
                        owner => ((Model)owner).Items.Count,
                        (owner, index) => ((Model)owner).Items[index],
                        (owner, item) => ((Model)owner).Items.Add((Item)item),
                        (owner, index) => ((Model)owner).Items.RemoveAt(index))
                },
                {
                    MorphsKey,
                    new SdkList(
                        owner => ((FakePmx)owner).Morph.Count,
                        (owner, index) => ((FakePmx)owner).Morph[index],
                        (owner, item) => ((FakePmx)owner).Morph.Add((PEPlugin.Pmx.IPXMorph)item),
                        (owner, index) => ((FakePmx)owner).Morph.RemoveAt(index))
                },
            };
        }

        /// <summary>型で分かれないツールの、1つだけの項目の組。</summary>
        private static IList<ToolFieldSet> Set(IList<ToolField> fields)
        {
            return new[] { new ToolFieldSet(null, fields) };
        }

        /// <summary>PMXが直に持つ要素のリストへ至る道。</summary>
        private static ToolAccess Listed()
        {
            return new ToolAccess(
                ToolAccessKind.Element, ListKey, null, true, typeof(Item), item => item is Item);
        }

        private static ToolReceiver Rooted(EditKind edit)
        {
            return new ToolReceiver(ToolReceiverKind.Pmx, null, edit);
        }

        /// <summary>呼び分けを1つだけ持つツールの表。題材はどれも1つだけを持つ。</summary>
        private static IDictionary<string, IList<ToolCall>> Singles(
            IDictionary<string, ToolCall> calls)
        {
            Dictionary<string, IList<ToolCall>> built =
                new Dictionary<string, IList<ToolCall>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ToolCall> call in calls)
            {
                built.Add(call.Key, new[] { call.Value });
            }

            return built;
        }

        private static IDictionary<string, IList<ToolCall>> Calls()
        {
            return Singles(new Dictionary<string, ToolCall>(StringComparer.Ordinal)
            {
                {
                    "model_clear_pmx",
                    new ToolCall(
                        ClearKey,
                        Rooted(EditKind.DuplicateEdit),
                        ToolAccess.Whole(),
                        DangerKind.Reset,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        null)
                },
                {
                    "view_set_marks",
                    new ToolCall(
                        SetMarksKey,
                        new ToolReceiver(ToolReceiverKind.Connection, ConnectorType, EditKind.ViewSession),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[] { new ToolArgument("indices", typeof(int[])) },
                        new ToolArgument[0],
                        null,
                        readBack: GetMarksKey)
                },
                {
                    "model_compact_pmx",
                    new ToolCall(
                        CompactKey,
                        Rooted(EditKind.DuplicateEdit),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        null)
                },
            });
        }

        private static IDictionary<string, ToolFields> Aggregations()
        {
            ToolField[] fields = { new ToolField("filePath", FilePathKey, typeof(string)) };

            return new Dictionary<string, ToolFields>(StringComparer.Ordinal)
            {
                {
                    "model_list_pmxes",
                    new ToolFields(
                        false, true, Rooted(EditKind.Read), ToolAccess.Whole(), Set(fields))
                },
                {
                    "model_list_faces",
                    new ToolFields(
                        false,
                        true,
                        Rooted(EditKind.Read),
                        new ToolAccess(
                            ToolAccessKind.Element,
                            FacesKey,
                            new[] { new ToolHop(MaterialsKey, true) },
                            true,
                            typeof(PEPlugin.Pmx.IPXFace),
                            item => item is PEPlugin.Pmx.IPXFace,
                            "face",
                            null,
                            typeof(PEPlugin.Pmx.IPXMaterial)),
                        Set(new ToolField[0]))
                },
                {
                    "model_update_pmxes",
                    new ToolFields(
                        true, true, Rooted(EditKind.DuplicateEdit), ToolAccess.Whole(), Set(fields))
                },
                {
                    "model_update_morphs",
                    new ToolFields(
                        true,
                        true,
                        Rooted(EditKind.DuplicateEdit),
                        new ToolAccess(
                            ToolAccessKind.Element,
                            MorphsKey,
                            null,
                            true,
                            typeof(PEPlugin.Pmx.IPXMorph),
                            item => item is PEPlugin.Pmx.IPXMorph),
                        Set(new[] { new ToolField("panel", PanelKey, typeof(int)) }))
                },
            };
        }

        private static IDictionary<string, ToolElements> Elements()
        {
            return new Dictionary<string, ToolElements>(StringComparer.Ordinal)
            {
                {
                    "model_add_vertices",
                    new ToolElements(ToolElementKind.Add, Rooted(EditKind.DuplicateEdit), Listed())
                },
                {
                    "model_remove_vertices",
                    new ToolElements(ToolElementKind.Remove, Rooted(EditKind.DuplicateEdit), Listed())
                },
            };
        }

        /// <summary>
        /// 複製して編集する相手の題材。エディタが持つ現在のモデルとして置くと、複製の要求には
        /// 別のオブジェクト一式を返し、反映では渡された複製の中身をここへ写す。
        /// </summary>
        private sealed class Model
        {
            public string FilePath { get; set; }

            public bool Cleared { get; set; }

            public List<Item> Items { get; } = new List<Item>();

            public List<FakeMaterial> Materials { get; } = new List<FakeMaterial>();

            /// <summary>複製の要求に返す、要素まで別のオブジェクトにした写し。</summary>
            public Model Duplicate()
            {
                Model made = new Model { FilePath = FilePath, Cleared = Cleared };
                made.Items.AddRange(Items.Select(item => item.Clone()));
                made.Materials.AddRange(Materials.Select(material => (FakeMaterial)material.Clone()));

                return made;
            }

            /// <summary>反映で渡された複製の中身を写す。</summary>
            public void Take(Model passed)
            {
                FilePath = passed.FilePath;
                Cleared = passed.Cleared;
                List<Item> items = passed.Items.Select(item => item.Clone()).ToList();
                Items.Clear();
                Items.AddRange(items);
                List<FakeMaterial> materials =
                    passed.Materials.Select(material => (FakeMaterial)material.Clone()).ToList();
                Materials.Clear();
                Materials.AddRange(materials);
            }
        }

        /// <summary>リストが並べる要素の題材。</summary>
        private sealed class Item
        {
            public string Name { get; set; }

            public Item Clone()
            {
                return new Item { Name = Name };
            }
        }

        /// <summary>エディタのいまのモデルの要素の名前。</summary>
        private string[] ItemNames()
        {
            return _model.Items.Select(item => item.Name).ToArray();
        }

        /// <summary>画面へ映す段。題材の口を通して、映し直しの結末を数える。</summary>
        private ScreenRefresh Refresh()
        {
            return new ScreenRefresh(() => _view, () => _form);
        }

        private ScreenTargets Screen()
        {
            return new ScreenTargets(() => _view, () => _form);
        }
    }
}
