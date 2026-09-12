using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// PMXの中に並ぶ要素を相手にするツールの振り分け。親の列を辿る道と、実行時の型で絞る道を含む。
    /// </summary>
    public sealed class ElementToolDispatchTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string SdkVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private const string ConnectorType = "Sdk.PmxConnector";

        private const string StateReadKey = "Sdk.PmxConnector.GetCurrentState()";

        private const string CommitKey = "Sdk.PmxConnector.Update(Sdk.Pmx)";

        private const string StopUndoKey = "Sdk.PmxConnector.LockUndo()";

        private const string ResumeUndoKey = "Sdk.PmxConnector.UnlockUndo()";

        private const string ItemInfoKey = "Sdk.Item.Info()";

        private const string InfoLabelKey = "Sdk.ItemInfo.Label()";

        private const string ItemsKey = "Sdk.Pmx.Items()";

        private const string GroupsKey = "Sdk.Pmx.Groups()";

        private const string LeavesKey = "Sdk.Group.Leaves()";

        private const string NoteKey = "Sdk.Pmx.Note()";

        private const string LabelKey = "Sdk.Item.Label()";

        private const string TextKey = "Sdk.Note.Text()";

        private const string ClearKey = "Sdk.Item.Clear(System.Single)";

        private const string NoteOfSpareKey = "Sdk.Spare.Note()";

        private const string MarkKey = "Sdk.Group.Mark()";

        private const string VeinsKey = "Sdk.Mark.Veins()";

        private const string MakerType = "Sdk.Maker";

        private const string MakeKey = "Sdk.Maker.Make()";

        private const string MakeLabelledKey = "Sdk.Maker.Make(System.String)";

        private const string MakeMarkedKey = "Sdk.Maker.Make(System.String,System.Int32)";

        private const string MakeCountedKey = "Sdk.Maker.Make(System.Int32)";

        private const string AttachKey = "Sdk.Maker.Attach(Sdk.Model,Sdk.Item,System.String)";

        private const string BridgeReadKey = "Sdk.Bridge.GetModel(Sdk.Connector)";

        private const string BridgeCommitKey =
            "Sdk.Bridge.Update(Sdk.Connector,Sdk.Model,System.Boolean)";

        private const string WidthKey = "Sdk.Mark.Width()";

        private const string SplitKey = "Sdk.Item.Split(out System.String,out System.String)";

        private const string TagKey = "Sdk.Leaf.Tag()";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly Model _model = new Model();

        private int _commits;

        private object _attachedModel;

        private object _attachedItem;

        private string _madeBy;

        private readonly List<string> _undoCalls = new List<string>();

        private bool _resumeFails;

        private UndoSuppression _undo;

        private PmxSession _session;

        private UndoRecovery _recovery;

        private readonly Model _bridged = new Model();

        private int _bridgeCommits;

        private object[] _bridgeReflected;

        public ElementToolDispatchTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-element-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            _undo = new UndoSuppression(_log);
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
        public void TheWholeListComesBackWhenTheRequestPointsAtEveryElement()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_list_items", Arguments(TargetNames.Element.All, true));

            IDictionary<string, object> value = Value(envelope);
            Assert.Equal(2, value[ToolDispatch.TotalName]);
            Assert.Equal(
                new[] { "一", "二" },
                Items(value).Select(i => i["label"]).ToArray());
            Assert.False(value.ContainsKey(ToolDispatch.NextOffsetName));
        }

        [Fact]
        public void OnlyThePointedPositionsComeBack()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });
            _model.Items.Add(new Item { Label = "三" });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(TargetNames.Element.Indices, new object[] { 2, 0 }));

            Assert.Equal(
                new[] { "三", "一" },
                Items(Value(envelope)).Select(i => i["label"]).ToArray());
        }

        [Fact]
        public void AListingThatPointsAtNothingIsRefused()
        {
            IDictionary<string, object> envelope = Call("model_list_items", Arguments());

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheCutOutCarriesTheTotalAndWhereToGoOn()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });
            _model.Items.Add(new Item { Label = "三" });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.OffsetName, 1,
                    ToolDispatch.LimitName, 1));

            IDictionary<string, object> value = Value(envelope);
            Assert.Equal(3, value[ToolDispatch.TotalName]);
            Assert.Equal(new[] { "二" }, Items(value).Select(i => i["label"]).ToArray());
            Assert.Equal(2, value[ToolDispatch.NextOffsetName]);
        }

        [Fact]
        public void TheSameValueGoesToEveryElementTheRequestPointsAt()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同")));

            Assert.Equal(2, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal(new[] { "同", "同" }, _model.Items.Select(i => i.Label).ToArray());
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void EachElementTakesItsOwnValueWhenTheRequestCarriesTheList()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValuesName,
                    new object[] { Value("label", "壱"), Value("label", "弐") }));

            Assert.Equal(2, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal(new[] { "壱", "弐" }, _model.Items.Select(i => i.Label).ToArray());
        }

        [Fact]
        public void AValueListThatIsNotAsLongAsTheTargetsIsRefused()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValuesName, new object[] { Value("label", "壱") }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Equal(new[] { "一", "二" }, _model.Items.Select(i => i.Label).ToArray());
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void GivingBothWaysOfCarryingTheValueIsRefused()
        {
            _model.Items.Add(new Item { Label = "一" });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "壱"),
                    ToolDispatch.ValuesName, new object[] { Value("label", "弐") }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheChildOfTheModelIsReadWithoutPointingAtAnything()
        {
            _model.Note.Text = "覚え書き";

            IDictionary<string, object> envelope = Call("model_list_notes", Arguments());

            Assert.Equal(1, Value(envelope)[ToolDispatch.TotalName]);
            Assert.Equal("覚え書き", Items(Value(envelope))[0]["text"]);
        }

        [Fact]
        public void TheChildOfTheModelIsWrittenAndReflected()
        {
            IDictionary<string, object> envelope = Call(
                "model_update_notes",
                Arguments(ToolDispatch.ValueName, Value("text", "書き換え")));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("書き換え", _model.Note.Text);
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void TheElementsUnderEveryParentComeBackWithWhereTheyAre()
        {
            Group first = new Group();
            first.Leaves.Add(new Item { Label = "一" });
            Group second = new Group();
            second.Leaves.Add(new Item { Label = "二" });
            _model.Groups.Add(first);
            _model.Groups.Add(second);

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(TargetNames.Parent.All, true, TargetNames.Element.All, true));

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(new[] { "一", "二" }, items.Select(i => i["label"]).ToArray());
            Assert.Equal(
                new[] { 0, 1 },
                items.Select(i => (int)i[ToolDispatch.ParentIndexName]).ToArray());
            Assert.Equal(
                new[] { 0, 0 },
                items.Select(i => (int)i[ToolDispatch.IndexInParentName]).ToArray());
        }

        [Fact]
        public void AnElementOfAnotherRuntimeTypeIsRefusedWithItsPosition()
        {
            Group group = new Group();
            group.Leaves.Add(new Spare());
            group.Leaves.Add(new Item { Label = "一" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(TargetNames.Parent.All, true, TargetNames.Element.All, true));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("位置 0", Message(envelope));
        }

        [Fact]
        public void PositionsPointAtTheListItselfAndNotAtTheElementsOfOneType()
        {
            Group group = new Group();
            group.Leaves.Add(new Spare());
            group.Leaves.Add(new Item { Label = "一" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 1 }));

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(new[] { "一" }, items.Select(i => i["label"]).ToArray());
            Assert.Equal(
                new[] { 1 },
                items.Select(i => (int)i[ToolDispatch.IndexInParentName]).ToArray());
        }

        [Fact]
        public void EachElementCarriesItsRuntimeTypeAndTheItemsOfThatType()
        {
            Group group = new Group();
            group.Leaves.Add(new Spare { Note = "控え" });
            group.Leaves.Add(new Item { Label = "一" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_list_sprouts",
                Arguments(TargetNames.Parent.All, true, TargetNames.Element.All, true));

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(
                new[] { "spare", "item" },
                items.Select(i => (string)i[ToolDispatch.ItemTypeName]).ToArray());
            Assert.Equal("控え", items[0]["note"]);
            Assert.False(items[0].ContainsKey("label"));
            Assert.Equal("一", items[1]["label"]);
            Assert.False(items[1].ContainsKey("note"));
        }

        [Fact]
        public void AnUpdateThatDividesByRuntimeTypeWritesOnlyThatType()
        {
            Group group = new Group();
            group.Leaves.Add(new Item { Label = "一" });
            group.Leaves.Add(new Spare { Note = "控え" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_update_sprouts",
                Arguments(
                    ToolDispatch.ItemTypeName, "spare",
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 1 },
                    ToolDispatch.ValueName, Value("note", "書き換え")));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("書き換え", ((Spare)group.Leaves[1]).Note);
            Assert.Equal("一", ((Item)group.Leaves[0]).Label);
        }

        [Fact]
        public void AnUpdateThatPointsAtAnotherRuntimeTypeIsRefused()
        {
            Group group = new Group();
            group.Leaves.Add(new Item { Label = "一" });
            group.Leaves.Add(new Spare { Note = "控え" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_update_sprouts",
                Arguments(
                    ToolDispatch.ItemTypeName, "spare",
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 0 },
                    ToolDispatch.ValueName, Value("note", "書き換え")));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("位置 0 は item", Message(envelope));
            Assert.Equal("控え", ((Spare)group.Leaves[1]).Note);
        }

        [Fact]
        public void TheChildEachElementHoldsBecomesOneColumn()
        {
            _model.Groups.Add(new Group { Mark = new Mark { Width = 1 } });
            _model.Groups.Add(new Group());
            _model.Groups.Add(new Group { Mark = new Mark { Width = 3 } });

            IDictionary<string, object> envelope = Call(
                "model_list_marks",
                Arguments(TargetNames.Parent.All, true, TargetNames.Element.All, true));

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(2, Value(envelope)[ToolDispatch.TotalName]);
            Assert.Equal(new[] { 1, 3 }, items.Select(i => (int)i["width"]).ToArray());
            Assert.Equal(
                new[] { 0, 2 },
                items.Select(i => (int)i[ToolDispatch.ParentIndexName]).ToArray());
        }

        [Fact]
        public void OnlyThePointedChildIsUpdated()
        {
            Group first = new Group { Mark = new Mark { Width = 1 } };
            Group second = new Group { Mark = new Mark { Width = 3 } };
            _model.Groups.Add(first);
            _model.Groups.Add(second);

            IDictionary<string, object> envelope = Call(
                "model_update_marks",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 1 },
                    ToolDispatch.ValueName, Value("width", 7)));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal(1, first.Mark.Width);
            Assert.Equal(7, second.Mark.Width);
        }

        [Fact]
        public void TheOutputArgumentsComeBackAsOneSetPerTarget()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_split_item", Arguments(TargetNames.Element.All, true));

            object[] values = (object[])envelope["value"];
            Assert.Equal(2, values.Length);
            Assert.Equal("一の左", ((IDictionary<string, object>)values[0])["left"]);
            Assert.Equal("一の右", ((IDictionary<string, object>)values[0])["right"]);
            Assert.Equal("二の左", ((IDictionary<string, object>)values[1])["left"]);
        }

        [Fact]
        public void ItemsOfADividedListCarryTheirTypeEvenWhenTheItemsAreNotDividedByIt()
        {
            Group group = new Group();
            group.Leaves.Add(new Item { Tag = "一" });
            group.Leaves.Add(new Spare { Tag = "二" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_list_tags",
                Arguments(TargetNames.Parent.All, true, TargetNames.Element.All, true));

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(
                new[] { "item", "spare" },
                items.Select(i => (string)i[ToolDispatch.ItemTypeName]).ToArray());
            Assert.Equal(new[] { "一", "二" }, items.Select(i => (string)i["tag"]).ToArray());
        }

        [Fact]
        public void AMethodOnOneTypeOfADividedListRunsOnThatTypesElements()
        {
            Group group = new Group();
            group.Leaves.Add(new Item());
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_clear_sprout",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.All, true,
                    ToolDispatch.ArgsName, Value("v", 3)));

            Assert.Equal(1, Value(envelope)[SetResponse.InvokedName]);
            Assert.Equal(3f, ((Item)group.Leaves[0]).Filled);
        }

        [Fact]
        public void AMethodOnOneTypeRefusesAnElementOfAnotherType()
        {
            Group group = new Group();
            group.Leaves.Add(new Spare());
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_clear_sprout",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.All, true,
                    ToolDispatch.ArgsName, Value("v", 3)));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("求めるのは item", Message(envelope));
        }

        [Fact]
        public void TheRefusedPositionIsTheOneTheRequestPointedAt()
        {
            Group first = new Group();
            first.Leaves.Add(new Item { Label = "一" });
            first.Leaves.Add(new Item { Label = "二" });
            Group second = new Group();
            second.Leaves.Add(new Spare());
            _model.Groups.Add(first);
            _model.Groups.Add(second);

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 2 }));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("位置 2 は spare", Message(envelope));
        }

        [Fact]
        public void AHandleIssuedAsOneOfTheTypesTheListHoldsIsRead()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Spare).FullName, new Spare { Note = "控え" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_sprouts",
                Arguments(TargetNames.Element.Handles, new object[] { handle }),
                handles);

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal("spare", items[0][ToolDispatch.ItemTypeName]);
            Assert.Equal("控え", items[0]["note"]);
        }

        [Fact]
        public void AHandleOfAnotherTypeThanTheUpdateNamesIsRefused()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Spare).FullName, new Spare(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_update_sprouts",
                Arguments(
                    ToolDispatch.ItemTypeName, "item",
                    TargetNames.Element.Handles, new object[] { handle },
                    ToolDispatch.ValueName, Value("label", "書き換え")),
                handles);

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
        }

        [Fact]
        public void AnUpdateWithoutTheRuntimeTypeIsRefused()
        {
            _model.Groups.Add(new Group());

            IDictionary<string, object> envelope = Call(
                "model_update_sprouts",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("note", "書き換え")));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AnUpdateWithARuntimeTypeTheListDoesNotHoldIsRefused()
        {
            _model.Groups.Add(new Group());

            IDictionary<string, object> envelope = Call(
                "model_update_sprouts",
                Arguments(
                    ToolDispatch.ItemTypeName, "知らない型",
                    TargetNames.Parent.All, true,
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("note", "書き換え")));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AListingUnderParentsThatPointsAtNoParentIsRefused()
        {
            _model.Groups.Add(new Group());

            IDictionary<string, object> envelope = Call(
                "model_list_leaves", Arguments(TargetNames.Element.All, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void OnlyTheElementsOfThePointedParentAreUpdated()
        {
            Group first = new Group();
            first.Leaves.Add(new Item { Label = "一" });
            Group second = new Group();
            second.Leaves.Add(new Item { Label = "二" });
            _model.Groups.Add(first);
            _model.Groups.Add(second);

            IDictionary<string, object> envelope = Call(
                "model_update_leaves",
                Arguments(
                    TargetNames.Parent.Indices, new object[] { 1 },
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "書き換え")));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("一", ((Item)first.Leaves[0]).Label);
            Assert.Equal("書き換え", ((Item)second.Leaves[0]).Label);
        }

        [Fact]
        public void AddingPutsEachHandleUnderTheParentItsGroupNames()
        {
            _model.Groups.Add(new Group());
            _model.Groups.Add(new Group());
            HandleLedger handles = Ledger();
            int first = handles.Issue(typeof(Item).FullName, new Item { Label = "一" }, () => { });
            int second = handles.Issue(typeof(Item).FullName, new Item { Label = "二" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(
                    ToolDispatch.AssignmentsName,
                    new object[]
                    {
                        Assignment(1, second),
                        Assignment(0, first),
                    }),
                handles);

            IDictionary<string, object> value = Value(envelope);
            Assert.Equal(2, value[SetResponse.AddedName]);
            Assert.Equal(new[] { 0, 0 }, (int[])value[SetResponse.IndicesName]);
            Assert.Equal("二", ((Item)_model.Groups[1].Leaves[0]).Label);
            Assert.Equal("一", ((Item)_model.Groups[0].Leaves[0]).Label);
            Assert.Equal(1, _commits);
            object released;
            Assert.False(handles.TryGet(first, typeof(Item).FullName, out released));
        }

        [Fact]
        public void WritingTheSameParentIntoTwoGroupsIsRefused()
        {
            _model.Groups.Add(new Group());
            HandleLedger handles = Ledger();
            int first = handles.Issue(typeof(Item).FullName, new Item(), () => { });
            int second = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(0, first), Assignment(0, second) }),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Empty(_model.Groups[0].Leaves);
        }

        [Fact]
        public void AddingUnderAParentThatIsNotThereIsRefused()
        {
            _model.Groups.Add(new Group());
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(ToolDispatch.AssignmentsName, new object[] { Assignment(3, handle) }),
                handles);

            Assert.Equal(ToolEnvelope.IndexOutOfRange, Code(envelope));
            Assert.Empty(_model.Groups[0].Leaves);
        }

        [Fact]
        public void RemovingTakesTheElementsOutOfThePointedParents()
        {
            Group group = new Group();
            group.Leaves.Add(new Item { Label = "一" });
            group.Leaves.Add(new Item { Label = "二" });
            _model.Groups.Add(group);

            IDictionary<string, object> envelope = Call(
                "model_remove_leaves",
                Arguments(
                    TargetNames.Parent.All, true,
                    TargetNames.Element.Indices, new object[] { 0 }));

            Assert.Equal(1, Value(envelope)[SetResponse.RemovedName]);
            Assert.Equal(new[] { "二" }, group.Leaves.Cast<Item>().Select(i => i.Label).ToArray());
        }

        [Fact]
        public void TheMethodRunsOnEveryElementTheRequestPointsAt()
        {
            _model.Items.Add(new Item());
            _model.Items.Add(new Item());

            IDictionary<string, object> envelope = Call(
                "model_clear_item",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ArgsName, Value("v", 2)));

            Assert.Equal(2, Value(envelope)[SetResponse.InvokedName]);
            Assert.Equal(new[] { 2f, 2f }, _model.Items.Select(i => i.Filled).ToArray());
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void EachElementTakesItsOwnArgumentsWhenTheRequestCarriesTheList()
        {
            _model.Items.Add(new Item());
            _model.Items.Add(new Item());

            IDictionary<string, object> envelope = Call(
                "model_clear_item",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ArgsListName,
                    new object[] { Value("v", 1), Value("v", 3) }));

            Assert.Equal(2, Value(envelope)[SetResponse.InvokedName]);
            Assert.Equal(new[] { 1f, 3f }, _model.Items.Select(i => i.Filled).ToArray());
        }

        [Fact]
        public void AnArgumentListThatIsNotAsLongAsTheTargetsIsRefused()
        {
            _model.Items.Add(new Item());
            _model.Items.Add(new Item());

            IDictionary<string, object> envelope = Call(
                "model_clear_item",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ArgsListName, new object[] { Value("v", 1) }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Equal(new[] { 0f, 0f }, _model.Items.Select(i => i.Filled).ToArray());
        }

        [Fact]
        public void PointingAtElementsByHandleLeavesNoRoomForTheSwitchOfWhichModelToSee()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Item).FullName, new Item { Label = "一" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(
                    TargetNames.Element.Handles, new object[] { handle },
                    PmxSession.HandleName, 1),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AnElementPointedAtByHandleIsReadWithoutTouchingTheModel()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Item).FullName, new Item { Label = "一" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(TargetNames.Element.Handles, new object[] { handle }),
                handles);

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(new[] { "一" }, items.Select(i => i["label"]).ToArray());
            Assert.False(items[0].ContainsKey(ToolDispatch.ParentIndexName));
        }

        [Fact]
        public void AListingUnderAHeldParentCarriesThatHandleAndTheIndexInIt()
        {
            HandleLedger handles = Ledger();
            Group group = new Group();
            group.Leaves.Add(new Item { Label = "一" });
            group.Leaves.Add(new Item { Label = "二" });
            int handle = handles.Issue(typeof(Group).FullName, group, () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.All, true),
                handles);

            IList<IDictionary<string, object>> items = Items(Value(envelope));
            Assert.Equal(new[] { "一", "二" }, items.Select(i => i["label"]).ToArray());
            Assert.Equal(
                new object[] { (long)handle, (long)handle },
                items.Select(i => i[ToolDispatch.ParentHandleName]).ToArray());
            Assert.Equal(
                new object[] { 0, 1 },
                items.Select(i => i[ToolDispatch.IndexInParentName]).ToArray());
            Assert.False(items[0].ContainsKey(ToolDispatch.ParentIndexName));
        }

        [Fact]
        public void AnUpdateUnderAHeldParentChangesItWithoutReflectingThePmx()
        {
            HandleLedger handles = Ledger();
            Group held = new Group();
            held.Leaves.Add(new Item { Label = "一" });
            Group inside = new Group();
            inside.Leaves.Add(new Item { Label = "二" });
            _model.Groups.Add(inside);
            int handle = handles.Issue(typeof(Group).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_update_leaves",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "書き換え")),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("書き換え", ((Item)held.Leaves[0]).Label);
            Assert.Equal("二", ((Item)inside.Leaves[0]).Label);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void RemovingUnderAHeldParentTakesTheElementOutOfIt()
        {
            HandleLedger handles = Ledger();
            Group held = new Group();
            held.Leaves.Add(new Item { Label = "一" });
            held.Leaves.Add(new Item { Label = "二" });
            int handle = handles.Issue(typeof(Group).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_remove_leaves",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.Indices, new object[] { 0 }),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.RemovedName]);
            Assert.Equal(new[] { "二" }, held.Leaves.Cast<Item>().Select(i => i.Label).ToArray());
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void PointingTheParentByHandleAndSwitchingThePmxIsRefused()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Group).FullName, new Group(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(
                    PmxSession.HandleName, 1,
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.All, true),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(PmxSession.HandleName, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void AHandleOfAnotherTypeThanTheParentIsRefused()
        {
            HandleLedger handles = Ledger();
            int handle = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_leaves",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.All, true),
                handles);

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
        }

        [Fact]
        public void AParentThatIssuesNoHandleTakesNoHandleOfTheParent()
        {
            IDictionary<string, object> envelope = Call(
                "model_list_veins",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { 1 },
                    TargetNames.Element.All, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(TargetNames.Parent.Handles, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void AddingUnderAHeldParentPutsTheElementIntoItWithoutReflectingThePmx()
        {
            HandleLedger handles = Ledger();
            Group held = new Group();
            int parent = handles.Issue(typeof(Group).FullName, held, () => { });
            int child = handles.Issue(typeof(Item).FullName, new Item { Label = "一" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(
                    ToolDispatch.AssignmentsName,
                    new object[] { HeldAssignment(parent, child) }),
                handles);

            IDictionary<string, object> value = Value(envelope);
            Assert.Equal(1, value[SetResponse.AddedName]);
            Assert.Equal(new[] { 0 }, (int[])value[SetResponse.IndicesName]);
            Assert.Equal("一", ((Item)held.Leaves[0]).Label);
            Assert.Equal(0, _commits);
            object released;
            Assert.False(handles.TryGet(child, typeof(Item).FullName, out released));
        }

        [Fact]
        public void MixingHowTheParentIsPointedAcrossTheGroupsIsRefused()
        {
            _model.Groups.Add(new Group());
            HandleLedger handles = Ledger();
            int parent = handles.Issue(typeof(Group).FullName, new Group(), () => { });
            int first = handles.Issue(typeof(Item).FullName, new Item(), () => { });
            int second = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(0, first), HeldAssignment(parent, second) }),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Empty(_model.Groups[0].Leaves);
        }

        [Fact]
        public void AGroupThatPointsTheParentBothWaysIsRefused()
        {
            HandleLedger handles = Ledger();
            int parent = handles.Issue(typeof(Group).FullName, new Group(), () => { });
            int child = handles.Issue(typeof(Item).FullName, new Item(), () => { });
            IDictionary<string, object> group = HeldAssignment(parent, child);
            group.Add(ToolDispatch.ParentIndexName, 0);

            IDictionary<string, object> envelope = Call(
                "model_add_leaves",
                Arguments(ToolDispatch.AssignmentsName, new object[] { group }),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AddingUnderAParentThatIssuesNoHandleTakesNoHandleOfTheParent()
        {
            HandleLedger handles = Ledger();
            int child = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_veins",
                Arguments(
                    ToolDispatch.AssignmentsName, new object[] { HeldAssignment(1, child) }),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(
                ToolDispatch.ParentHandleName, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void TheElementsOfAParentWithNoPathFromTheModelComeBackUnderItsHandle()
        {
            HandleLedger handles = Ledger();
            Group held = new Group();
            held.Leaves.Add(new Item { Label = "一" });
            held.Leaves.Add(new Item { Label = "二" });
            int handle = handles.Issue(typeof(Group).FullName, held, () => { });

            IDictionary<string, object> envelope = Call(
                "model_list_sprigs",
                Arguments(
                    TargetNames.Parent.Handles, new object[] { handle },
                    TargetNames.Element.All, true),
                handles);

            Assert.Equal(
                new[] { "一", "二" },
                Items(Value(envelope)).Select(i => i["label"]).ToArray());
        }

        [Fact]
        public void PointingTheElementsOfSuchAParentWithoutItsHandleIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_list_sprigs", Arguments(TargetNames.Element.All, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(TargetNames.Parent.Handles, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void AddingUnderSuchAParentPutsTheElementIntoItWithoutReflectingThePmx()
        {
            HandleLedger handles = Ledger();
            Group held = new Group();
            int parent = handles.Issue(typeof(Group).FullName, held, () => { });
            int child = handles.Issue(typeof(Item).FullName, new Item { Label = "一" }, () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_sprigs",
                Arguments(
                    ToolDispatch.AssignmentsName,
                    new object[] { HeldAssignment(parent, child) }),
                handles);

            IDictionary<string, object> value = Value(envelope);
            Assert.Equal(1, value[SetResponse.AddedName]);
            Assert.Equal(new[] { 0 }, (int[])value[SetResponse.IndicesName]);
            Assert.Equal("一", ((Item)held.Leaves[0]).Label);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void AddingUnderSuchAParentByItsPositionIsRefused()
        {
            _model.Groups.Add(new Group());
            HandleLedger handles = Ledger();
            int child = handles.Issue(typeof(Item).FullName, new Item(), () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_sprigs",
                Arguments(ToolDispatch.AssignmentsName, new object[] { Assignment(0, child) }),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(
                ToolDispatch.ParentIndexName, Message(envelope), StringComparison.Ordinal);
            Assert.Empty(_model.Groups[0].Leaves);
        }

        [Fact]
        public void AnUpdateOfHeldElementsDoesNotReflectThePmx()
        {
            HandleLedger handles = Ledger();
            Item item = new Item { Label = "一" };
            int handle = handles.Issue(typeof(Item).FullName, item, () => { });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.Handles, new object[] { handle },
                    ToolDispatch.ValueName, Value("label", "書き換え")),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("書き換え", item.Label);
            Assert.Equal(0, _commits);
        }

        private static IDictionary<string, object> HeldAssignment(int parent, int handle)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ToolDispatch.ParentHandleName, parent },
                { TargetNames.Element.Handles, new object[] { handle } },
            };
        }

        [Fact]
        public void AnArgumentGivenAsAPositionReachesTheElementAtIt()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_attach_maker", Arguments("item", 1, "label", "付けた"));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Same(_model.Items[1], _attachedItem);
            Assert.Same(_model, _attachedModel);
            Assert.Equal(1, _commits);
        }

        [Fact]
        public void APositionOutsideTheListIsRefused()
        {
            _model.Items.Add(new Item());

            IDictionary<string, object> envelope = Call(
                "model_attach_maker", Arguments("item", 3, "label", "付けた"));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, Code(envelope));
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void APositionTooLargeForTheListIsRefusedInsteadOfWrappingRound()
        {
            _model.Items.Add(new Item { Label = "一" });

            IDictionary<string, object> envelope = Call(
                "model_attach_maker",
                Arguments("item", ((long)int.MaxValue + 1) * 2, "label", "付けた"));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_attachedItem);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void ANullPositionMeansThereIsNoRelation()
        {
            IDictionary<string, object> envelope = Call(
                "model_attach_maker", Arguments("item", null, "label", "付けた"));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Null(_attachedItem);
        }

        [Fact]
        public void AnArgumentTheHostFillsInIsNotPassedByTheCaller()
        {
            IDictionary<string, object> envelope = Call(
                "model_attach_maker", Arguments("pmx", 1, "item", null, "label", "付けた"));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("pmx", Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void ACallThatFillsInThePmxSwitchesWhichOneItLooksAt()
        {
            HandleLedger handles = Ledger();
            Model other = new Model();
            other.Items.Add(new Item { Label = "別" });
            int handle = handles.Issue(typeof(Model).FullName, other, () => { });

            IDictionary<string, object> envelope = Call(
                "model_attach_maker",
                Arguments(PmxSession.HandleName, handle, "item", 0, "label", "付けた"),
                handles);

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Same(other, _attachedModel);
            Assert.Same(other.Items[0], _attachedItem);
            Assert.Equal(0, _commits);
        }

        [Fact]
        public void ACallThroughTheBridgeTakesAndReflectsWithThatFlow()
        {
            _model.Items.Add(new Item { Label = "こちら" });
            _bridged.Items.Add(new Item { Label = "橋渡し" });

            IDictionary<string, object> envelope = Call(
                "model_attach_bridge", Arguments("item", 0, "label", "付けた"));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Same(_bridged, _attachedModel);
            Assert.Same(_bridged.Items[0], _attachedItem);
            Assert.Equal(0, _commits);
            Assert.Equal(1, _bridgeCommits);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void TheBridgeFlowFillsTheConnectorAndWhetherToStopTheUndo(
            bool? asked, bool stopped)
        {
            _bridged.Items.Add(new Item());
            IDictionary<string, object> arguments = Arguments("item", 0, "label", "付けた");
            if (asked.HasValue)
            {
                arguments.Add(ToolDispatch.SuppressName, asked.Value);
            }

            Assert.True((bool)Call("model_attach_bridge", arguments)["ok"], "包みが成功でない。");

            Assert.Equal(3, _bridgeReflected.Length);
            Assert.NotNull(_bridgeReflected[0]);
            Assert.Same(_bridged, _bridgeReflected[1]);
            Assert.Equal(stopped, _bridgeReflected[2]);
        }

        [Fact]
        public void AMemberThatMakesSomethingAnswersWithItsHandle()
        {
            HandleLedger handles = Ledger();

            IDictionary<string, object> envelope = Call("model_make_item", Arguments(), handles);

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            object made = envelope["value"];
            Assert.IsType<int>(made);
            object held;
            Assert.True(handles.TryGet((int)made, typeof(Item).FullName, out held));
            Assert.IsType<Item>(held);
        }

        [Fact]
        public void TheHandleOfSomethingMadeCanPointTheElementTools()
        {
            HandleLedger handles = Ledger();
            int handle = (int)Call("model_make_item", Arguments(), handles)["value"];

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(TargetNames.Element.Handles, new object[] { handle }),
                handles);

            Assert.Single(Items(Value(envelope)));
        }

        [Theory]
        [InlineData(MakeKey)]
        [InlineData(MakeLabelledKey)]
        [InlineData(MakeMarkedKey)]
        public void TheOverloadThatTakesTheGivenArgumentsIsTheOneThatRuns(string expected)
        {
            IDictionary<string, object> arguments = Arguments();
            if (expected != MakeKey)
            {
                arguments.Add("label", "名");
            }

            if (expected == MakeMarkedKey)
            {
                arguments.Add("mark", 3);
            }

            Assert.True((bool)Call("model_make_item", arguments)["ok"], "包みが成功でない。");
            Assert.Equal(expected, _madeBy);
        }

        [Theory]
        [InlineData("名", MakeLabelledKey)]
        [InlineData(3, MakeCountedKey)]
        public void OverloadsThatTakeTheSameNameAreToldApartByTheShapeOfTheValue(
            object given, string expected)
        {
            Assert.True(
                (bool)Call("model_make_item", Arguments("label", given))["ok"],
                "包みが成功でない。");
            Assert.Equal(expected, _madeBy);
        }

        [Fact]
        public void AValueThatNoOverloadCanTakeIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_make_item", Arguments("label", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_madeBy);
        }

        [Fact]
        public void AnArgumentOfAnOverloadWhoseOthersAreMissingIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_make_item", Arguments("mark", 3));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_madeBy);
        }

        [Fact]
        public void AskingToStopTheUndoWrapsTheReflectionInTheLockingRows()
        {
            _model.Items.Add(new Item { Label = "一" });

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal(new[] { StopUndoKey, CommitKey, ResumeUndoKey }, _undoCalls.ToArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        public void NotAskingToStopTheUndoLeavesTheLockingRowsAlone(bool? asked)
        {
            _model.Items.Add(new Item { Label = "一" });
            IDictionary<string, object> arguments = Arguments(
                TargetNames.Element.All, true, ToolDispatch.ValueName, Value("label", "同"));
            if (asked.HasValue)
            {
                arguments.Add(ToolDispatch.SuppressName, asked.Value);
            }

            Assert.True(
                (bool)Call("model_update_items", arguments)["ok"], "包みが成功でない。");

            Assert.Equal(new[] { CommitKey }, _undoCalls.ToArray());
        }

        [Fact]
        public void OnlyADuplicateEditMayAskToStopTheUndo()
        {
            _model.Items.Add(new Item { Label = "一" });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(TargetNames.Element.All, true, ToolDispatch.SuppressName, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Empty(_undoCalls);
        }

        [Fact]
        public void AReadMayCarryTheAskingArgumentWhenItDoesNotAsk()
        {
            _model.Items.Add(new Item { Label = "一" });

            IDictionary<string, object> envelope = Call(
                "model_list_items",
                Arguments(TargetNames.Element.All, true, ToolDispatch.SuppressName, false));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
        }

        [Fact]
        public void AnAskingArgumentThatIsNotATruthValueIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Empty(_undoCalls);
        }

        [Fact]
        public void PointingThePmxByHandleMayNotAskToStopTheUndo()
        {
            HandleLedger handles = Ledger();
            Model other = new Model();
            other.Items.Add(new Item { Label = "別" });
            int handle = handles.Issue(typeof(Model).FullName, other, () => { });

            IDictionary<string, object> envelope = Call(
                "model_attach_maker",
                Arguments(
                    PmxSession.HandleName, handle,
                    "item", 0,
                    "label", "付けた",
                    ToolDispatch.SuppressName, true),
                handles);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Empty(_undoCalls);
        }

        [Fact]
        public void ARecordThatCouldNotBeResumedIsToldInTheAnswerThatLeftIt()
        {
            _model.Items.Add(new Item { Label = "一" });
            _resumeFails = true;

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Contains(
                UndoGate.LeftoverWarning,
                ((object[])envelope[ToolEnvelope.WarningsName]).Select(w => (string)w));
        }

        [Fact]
        public void AnEditThatWouldRunWithALeftoverIsRefusedWithoutTouchingTheModel()
        {
            _model.Items.Add(new Item { Label = "一" });
            _resumeFails = true;
            Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));
            _undoCalls.Clear();

            IDictionary<string, object> envelope = Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "別")));

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("戻せていない", Message(envelope), StringComparison.Ordinal);
            Assert.Equal("同", _model.Items[0].Label);
        }

        [Fact]
        public void AReadRunsWithALeftoverAndSaysSo()
        {
            _model.Items.Add(new Item { Label = "一" });
            _resumeFails = true;
            Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));

            IDictionary<string, object> envelope = Call(
                "model_list_items", Arguments(TargetNames.Element.All, true));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Contains(
                UndoGate.LeftoverWarning,
                ((object[])envelope[ToolEnvelope.WarningsName]).Select(w => (string)w));
        }

        [Fact]
        public void ARecordResumedLaterIsToldOnceInTheNextAnswer()
        {
            _model.Items.Add(new Item { Label = "一" });
            _resumeFails = true;
            Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));
            _resumeFails = false;

            IDictionary<string, object> resumed = Call(
                "model_list_items", Arguments(TargetNames.Element.All, true));
            IDictionary<string, object> after = Call(
                "model_list_items", Arguments(TargetNames.Element.All, true));

            Assert.Contains(
                UndoGate.RecoveredWarning,
                ((object[])resumed[ToolEnvelope.WarningsName]).Select(w => (string)w));
            Assert.False(
                after.ContainsKey(ToolEnvelope.WarningsName), "警告が二度目にも載っている。");
        }

        [Fact]
        public void AFailingAnswerCarriesTheLeftoverInItsExplanation()
        {
            _model.Items.Add(new Item { Label = "一" });
            _resumeFails = true;
            Call(
                "model_update_items",
                Arguments(
                    TargetNames.Element.All, true,
                    ToolDispatch.ValueName, Value("label", "同"),
                    ToolDispatch.SuppressName, true));

            IDictionary<string, object> envelope = Call(
                "model_list_items", Arguments(TargetNames.Element.Indices, new object[] { 99 }));

            Assert.False((bool)envelope["ok"], "包みが成功になっている。");
            Assert.Contains(
                UndoGate.LeftoverWarning, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void EachElementAnswersWithTheItemsOfWhatItReturns()
        {
            _model.Items.Add(new Item { Label = "一" });
            _model.Items.Add(new Item { Label = "二" });

            IDictionary<string, object> envelope = Call(
                "model_info_items", Arguments(TargetNames.Element.All, true));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            object[] listed = (object[])envelope["value"];
            Assert.Equal(
                new[] { "一", "二" },
                listed.Select(v => (string)((IDictionary<string, object>)v)["label"]).ToArray());
        }

        private static IDictionary<string, object> Assignment(int parent, int handle)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ToolDispatch.ParentIndexName, parent },
                { TargetNames.Element.Handles, new object[] { handle } },
            };
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

        private static IList<IDictionary<string, object>> Items(IDictionary<string, object> value)
        {
            return ((object[])value[ToolDispatch.ItemsName])
                .Cast<IDictionary<string, object>>()
                .ToList();
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
                    { MakerType, connection => new object() },
                };
            ResidentConnection connection = Connection();
            if (_session == null)
            {
                _session = Session(relay, receivers, connection);
                _recovery = new UndoRecovery(_undo, _session.UndoLock);
            }

            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                Lists(),
                connection,
                _session,
                new PmxSession(
                    relay,
                    receivers,
                    connection,
                    new PmxFlow(
                        BridgeReadKey,
                        BridgeCommitKey,
                        null,
                        new[] { FlowSlot.Connector },
                        new[] { FlowSlot.Connector, FlowSlot.Pmx, FlowSlot.UndoLock }),
                    typeof(Model),
                    _undo),
                _recovery,
                Calls(),
                Aggregations(),
                Elements(),
                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal),
                new StillModifierKeys(),
                EventBindingFixture.Empty());

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
                    new[] { FlowSlot.Pmx },
                    StopUndoKey,
                    ResumeUndoKey),
                typeof(Model),
                _undo);
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
                    { StateReadKey, (target, arguments) => _model },
                    {
                        CommitKey,
                        (target, arguments) =>
                        {
                            _commits++;
                            _undoCalls.Add(CommitKey);
                            return null;
                        }
                    },
                    {
                        StopUndoKey,
                        (target, arguments) =>
                        {
                            _undoCalls.Add(StopUndoKey);
                            return null;
                        }
                    },
                    {
                        ResumeUndoKey,
                        (target, arguments) =>
                        {
                            _undoCalls.Add(ResumeUndoKey);
                            if (_resumeFails)
                            {
                                throw new InvalidOperationException("戻せない。");
                            }

                            return null;
                        }
                    },
                    { NoteKey, (target, arguments) => ((Model)target).Note },
                    {
                        ItemInfoKey,
                        (target, arguments) => new ItemInfo { Label = ((Item)target).Label }
                    },
                    { InfoLabelKey, (target, arguments) => ((ItemInfo)target).Label },
                    { MakeKey, (target, arguments) => Made(MakeKey) },
                    { MakeLabelledKey, (target, arguments) => Made(MakeLabelledKey) },
                    { MakeMarkedKey, (target, arguments) => Made(MakeMarkedKey) },
                    { MakeCountedKey, (target, arguments) => Made(MakeCountedKey) },
                    { BridgeReadKey, (target, arguments) => _bridged },
                    {
                        BridgeCommitKey,
                        (target, arguments) =>
                        {
                            _bridgeCommits++;
                            _bridgeReflected = arguments;
                            return null;
                        }
                    },
                    {
                        AttachKey,
                        (target, arguments) =>
                        {
                            _attachedModel = arguments[0];
                            _attachedItem = arguments[1];
                            ((Model)arguments[0]).Note.Text = (string)arguments[2];
                            return null;
                        }
                    },
                    {
                        LabelKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Item)target).Label
                            : Written(((Item)target), (string)arguments[0])
                    },
                    {
                        TextKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Note)target).Text
                            : Written(((Note)target), (string)arguments[0])
                    },
                    {
                        ClearKey,
                        (target, arguments) =>
                        {
                            ((Item)target).Filled = (float)arguments[0];
                            return null;
                        }
                    },
                    {
                        TagKey,
                        (target, arguments) => ((Leaf)target).Tag
                    },
                    {
                        MarkKey,
                        (target, arguments) => ((Group)target).Mark
                    },
                    {
                        WidthKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Mark)target).Width
                            : Written(((Mark)target), (int)arguments[0])
                    },
                    {
                        SplitKey,
                        (target, arguments) => new object[]
                        {
                            ((Item)target).Label + "の左",
                            ((Item)target).Label + "の右",
                        }
                    },
                    {
                        NoteOfSpareKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Spare)target).Note
                            : Written(((Spare)target), (string)arguments[0])
                    },
                };

            return new SdkRelayTable(SdkVersion, Digest, calls, new string[0]);
        }

        /// <summary>どの呼び分けが走ったかを控えて、生成物を返す。</summary>
        private object Made(string rowKey)
        {
            _madeBy = rowKey;

            return new Item();
        }

        private static object Written(Item item, string label)
        {
            item.Label = label;

            return null;
        }

        private static object Written(Note note, string text)
        {
            note.Text = text;

            return null;
        }

        private static object Written(Spare spare, string note)
        {
            spare.Note = note;

            return null;
        }

        private static object Written(Mark mark, int width)
        {
            mark.Width = width;

            return null;
        }

        private static IDictionary<string, SdkList> Lists()
        {
            return new Dictionary<string, SdkList>(StringComparer.Ordinal)
            {
                {
                    ItemsKey,
                    new SdkList(
                        owner => ((Model)owner).Items.Count,
                        (owner, index) => ((Model)owner).Items[index],
                        (owner, item) => ((Model)owner).Items.Add((Item)item),
                        (owner, index) => ((Model)owner).Items.RemoveAt(index))
                },
                {
                    GroupsKey,
                    new SdkList(
                        owner => ((Model)owner).Groups.Count,
                        (owner, index) => ((Model)owner).Groups[index],
                        (owner, item) => ((Model)owner).Groups.Add((Group)item),
                        (owner, index) => ((Model)owner).Groups.RemoveAt(index))
                },
                {
                    LeavesKey,
                    new SdkList(
                        owner => ((Group)owner).Leaves.Count,
                        (owner, index) => ((Group)owner).Leaves[index],
                        (owner, item) => ((Group)owner).Leaves.Add((Leaf)item),
                        (owner, index) => ((Group)owner).Leaves.RemoveAt(index))
                },
                {
                    VeinsKey,
                    new SdkList(
                        owner => ((Mark)owner).Veins.Count,
                        (owner, index) => ((Mark)owner).Veins[index],
                        (owner, item) => ((Mark)owner).Veins.Add((Item)item),
                        (owner, index) => ((Mark)owner).Veins.RemoveAt(index))
                },
            };
        }

        /// <summary>実行時の型で分かれるツールの、型ごとの項目の組。</summary>
        private static IList<ToolFieldSet> Sprouts(IList<ToolField> labels)
        {
            return new[]
            {
                new ToolFieldSet("item", labels),
                new ToolFieldSet(
                    "spare", new[] { new ToolField("note", NoteOfSpareKey, typeof(string)) }),
            };
        }

        /// <summary>型で分かれないツールの、1つだけの項目の組。</summary>
        private static IList<ToolFieldSet> Set(IList<ToolField> fields)
        {
            return new[] { new ToolFieldSet(null, fields) };
        }

        private static ToolReceiver Rooted(EditKind edit)
        {
            return new ToolReceiver(ToolReceiverKind.Pmx, null, edit);
        }

        /// <summary>PMXが直に持つ要素のリストへ至る道。</summary>
        private static ToolAccess Direct()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                ItemsKey,
                null,
                true,
                typeof(Item),
                item => item is Item,
                "item");
        }

        /// <summary>親のリストを1つ挟んだ先の、要素のリストへ至る道。</summary>
        private static ToolAccess Nested()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                LeavesKey,
                new[] { new ToolHop(GroupsKey, true) },
                true,
                typeof(Item),
                item => item is Item,
                "item",
                Kinds(),
                typeof(Group));
        }

        /// <summary>PMXから辿る道が無く、ハンドルで指した親の下にだけある要素のリストへ至る道。</summary>
        private static ToolAccess Sprigged()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                LeavesKey,
                null,
                true,
                typeof(Item),
                item => item is Item,
                "item",
                null,
                typeof(Group));
        }

        /// <summary>親が1つだけ持つ子の、さらに下にある要素のリストへ至る道。</summary>
        private static ToolAccess Veined()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                VeinsKey,
                new[] { new ToolHop(GroupsKey, true), new ToolHop(MarkKey, false) },
                true,
                typeof(Item),
                item => item is Item,
                "item");
        }

        /// <summary>親のリストを1つ挟んだ先の、親ごとに1つだけ持つ子へ至る道。</summary>
        private static ToolAccess Marked()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                MarkKey,
                new[] { new ToolHop(GroupsKey, true) },
                false,
                typeof(Mark),
                item => item is Mark,
                "mark",
                null,
                typeof(Group));
        }

        /// <summary>そのリストが並べうる具象の型。</summary>
        private static IList<ToolItem> Kinds()
        {
            return new[]
            {
                new ToolItem("item", typeof(Item), item => item is Item),
                new ToolItem("spare", typeof(Spare), item => item is Spare),
            };
        }

        /// <summary>型で分かれるリストの上で、1つの具象の型だけを相手にする道。</summary>
        private static ToolAccess Sprout()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                LeavesKey,
                new[] { new ToolHop(GroupsKey, true) },
                true,
                typeof(Item),
                item => item is Item,
                "item",
                Kinds(),
                typeof(Group));
        }

        /// <summary>同じリストを、並べうる具象の型で分けて相手にする道。</summary>
        private static ToolAccess Divided()
        {
            return new ToolAccess(
                ToolAccessKind.Element,
                LeavesKey,
                new[] { new ToolHop(GroupsKey, true) },
                true,
                typeof(Leaf),
                item => item is Leaf,
                "leaf",
                Kinds(),
                typeof(Group));
        }

        /// <summary>生成物を返す呼び分け1つ。引数はそのまま受け取る。</summary>
        private static ToolCall Making(string rowKey, params ToolArgument[] arguments)
        {
            return new ToolCall(
                rowKey,
                new ToolReceiver(ToolReceiverKind.Connection, MakerType, EditKind.Read),
                ToolAccess.Whole(),
                DangerKind.None,
                arguments,
                new ToolArgument[0],
                typeof(Item),
                typeof(Item));
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
            IDictionary<string, IList<ToolCall>> calls =
                Singles(new Dictionary<string, ToolCall>(StringComparer.Ordinal)
            {
                {
                    "model_clear_item",
                    new ToolCall(
                        ClearKey,
                        Rooted(EditKind.DuplicateEdit),
                        Direct(),
                        DangerKind.None,
                        new[] { new ToolArgument("v", typeof(float)) },
                        new ToolArgument[0],
                        null)
                },
                {
                    "model_info_items",
                    new ToolCall(
                        ItemInfoKey,
                        Rooted(EditKind.Read),
                        Direct(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(ItemInfo),
                        null,
                        new[] { new ToolField("label", InfoLabelKey, typeof(string)) })
                },
                {
                    "model_split_item",
                    new ToolCall(
                        SplitKey,
                        Rooted(EditKind.Read),
                        Direct(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new[]
                        {
                            new ToolArgument("left", typeof(string)),
                            new ToolArgument("right", typeof(string)),
                        },
                        null)
                },
                {
                    "model_attach_bridge",
                    new ToolCall(
                        AttachKey,
                        new ToolReceiver(
                            ToolReceiverKind.Connection, MakerType, EditKind.DuplicateEdit, true),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[]
                        {
                            new ToolArgument("pmx", typeof(Model), true),
                            new ToolArgument("item", typeof(Item), false, Direct()),
                            new ToolArgument("label", typeof(string)),
                        },
                        new ToolArgument[0],
                        null)
                },
                {
                    "model_attach_maker",
                    new ToolCall(
                        AttachKey,
                        new ToolReceiver(
                            ToolReceiverKind.Connection, MakerType, EditKind.DuplicateEdit),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[]
                        {
                            new ToolArgument("pmx", typeof(Model), true),
                            new ToolArgument("item", typeof(Item), false, Direct()),
                            new ToolArgument("label", typeof(string)),
                        },
                        new ToolArgument[0],
                        null)
                },
                {
                    "model_clear_sprout",
                    new ToolCall(
                        ClearKey,
                        Rooted(EditKind.DuplicateEdit),
                        Sprout(),
                        DangerKind.None,
                        new[] { new ToolArgument("v", typeof(float)) },
                        new ToolArgument[0],
                        null)
                },
            });

            calls.Add(
                "model_make_item",
                new[]
                {
                    Making(MakeKey),
                    Making(MakeLabelledKey, new ToolArgument("label", typeof(string))),
                    Making(
                        MakeMarkedKey,
                        new ToolArgument("label", typeof(string)),
                        new ToolArgument("mark", typeof(int))),
                    Making(MakeCountedKey, new ToolArgument("label", typeof(int))),
                });

            return calls;
        }

        private static IDictionary<string, ToolFields> Aggregations()
        {
            ToolField[] labels = { new ToolField("label", LabelKey, typeof(string)) };
            ToolField[] texts = { new ToolField("text", TextKey, typeof(string)) };
            ToolAccess note = new ToolAccess(
                ToolAccessKind.Child, NoteKey, null, false, null, null);

            return new Dictionary<string, ToolFields>(StringComparer.Ordinal)
            {
                {
                    "model_list_items",
                    new ToolFields(false, true, Rooted(EditKind.Read), Direct(), Set(labels))
                },
                {
                    "model_update_items",
                    new ToolFields(
                        true, true, Rooted(EditKind.DuplicateEdit), Direct(), Set(labels))
                },
                {
                    "model_list_leaves",
                    new ToolFields(false, true, Rooted(EditKind.Read), Nested(), Set(labels))
                },
                {
                    "model_update_leaves",
                    new ToolFields(
                        true, true, Rooted(EditKind.DuplicateEdit), Nested(), Set(labels))
                },
                {
                    "model_list_tags",
                    new ToolFields(
                        false,
                        true,
                        Rooted(EditKind.Read),
                        Divided(),
                        Set(new[] { new ToolField("tag", TagKey, typeof(string)) }))
                },
                {
                    "model_list_sprouts",
                    new ToolFields(false, true, Rooted(EditKind.Read), Divided(), Sprouts(labels))
                },
                {
                    "model_update_sprouts",
                    new ToolFields(
                        true, true, Rooted(EditKind.DuplicateEdit), Divided(), Sprouts(labels))
                },
                {
                    "model_list_marks",
                    new ToolFields(
                        false,
                        true,
                        Rooted(EditKind.Read),
                        Marked(),
                        Set(new[] { new ToolField("width", WidthKey, typeof(int)) }))
                },
                {
                    "model_update_marks",
                    new ToolFields(
                        true,
                        true,
                        Rooted(EditKind.DuplicateEdit),
                        Marked(),
                        Set(new[] { new ToolField("width", WidthKey, typeof(int)) }))
                },
                {
                    "model_list_veins",
                    new ToolFields(false, true, Rooted(EditKind.Read), Veined(), Set(labels))
                },
                {
                    "model_list_sprigs",
                    new ToolFields(false, true, Rooted(EditKind.Read), Sprigged(), Set(labels))
                },
                {
                    "model_list_notes",
                    new ToolFields(false, true, Rooted(EditKind.Read), note, Set(texts))
                },
                {
                    "model_update_notes",
                    new ToolFields(true, true, Rooted(EditKind.DuplicateEdit), note, Set(texts))
                },
            };
        }

        private static IDictionary<string, ToolElements> Elements()
        {
            return new Dictionary<string, ToolElements>(StringComparer.Ordinal)
            {
                { "model_add_leaves", new ToolElements(false, Rooted(EditKind.DuplicateEdit), Nested()) },
                { "model_remove_leaves", new ToolElements(true, Rooted(EditKind.DuplicateEdit), Nested()) },
                { "model_add_veins", new ToolElements(false, Rooted(EditKind.DuplicateEdit), Veined()) },
                { "model_add_sprigs", new ToolElements(false, Rooted(EditKind.DuplicateEdit), Sprigged()) },
            };
        }

        /// <summary>複製して編集する相手の題材。</summary>
        private sealed class Model
        {
            public List<Item> Items { get; } = new List<Item>();

            public List<Group> Groups { get; } = new List<Group>();

            public Note Note { get; } = new Note();
        }

        /// <summary>要素を並べるリストを持つ、親の題材。</summary>
        private sealed class Group
        {
            public List<Leaf> Leaves { get; } = new List<Leaf>();

            /// <summary>その親が1つだけ持つ子。持たない親では null。</summary>
            public Mark Mark { get; set; }
        }

        /// <summary>リストの要素が1つだけ持つ子の題材。</summary>
        private sealed class Mark
        {
            public int Width { get; set; }

            public List<Item> Veins { get; } = new List<Item>();
        }

        /// <summary>PMXが1つだけ持つ子の題材。</summary>
        private sealed class Note
        {
            public string Text { get; set; }
        }

        /// <summary>並びが受け入れる基の型。</summary>
        private abstract class Leaf
        {
            public string Tag { get; set; }
        }

        /// <summary>独立したツールを持たず、返す値の中だけに現れる題材。</summary>
        private sealed class ItemInfo
        {
            public string Label { get; set; }
        }

        /// <summary>ツールが相手にする具象の型。</summary>
        private sealed class Item : Leaf
        {
            public string Label { get; set; }

            public float Filled { get; set; }
        }

        /// <summary>同じ並びに混じる、もう一つの具象の型。</summary>
        private sealed class Spare : Leaf
        {
            public string Note { get; set; }
        }
    }
}
