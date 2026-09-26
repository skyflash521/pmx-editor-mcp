using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PEPlugin;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>ツールの要求を結び付きの表が指す行へ振り分ける。</summary>
    public sealed class ToolDispatchTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string SdkVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private const string TargetType = "Sdk.Form";

        /// <summary>ビューごとに実体の分かれる受け手。どのビューの設定かを view で選ぶ。</summary>
        private const string SettingType = "PEPlugin.View.IPEViewSettingConnector";

        private const string SaveKey = "Sdk.Form.Save(System.String)";

        /// <summary>画面を撮る行。返る画像はその呼び出しが作ったものである。</summary>
        private const string ShotKey = "PEPlugin.View.IPEPMDViewConnector.GetClientImage()";

        private const string SelectionKey =
            "PEPlugin.View.IPEPMDViewConnector.SetSelectedVertexIndices(System.Int32[])";

        private const string CountKey = "Sdk.Form.Count()";

        private const string PickedKey = "Sdk.Form.Picked()";

        /// <summary>番号の並びを丸ごと返す行。応答は位置と件数で切り出す。</summary>
        private const string PagedKey = "Sdk.Form.Paged()";

        private const string FlagKey = "Sdk.Form.Flag()";

        private const string WriteDroppingKey = "Sdk.Form.Stuck()";

        private const string TextHeldSizeKey = "Sdk.Form.Size()";

        private const string LostKey = "Sdk.Form.Lost()";

        private const string ThrowKey = "Sdk.Form.Throw()";

        private const string InfoKey = "Sdk.Form.Info()";

        private const string InfosKey = "Sdk.Form.Infos()";

        private const string InfoNameKey = "Sdk.Info.Name()";

        private const string InfoOptionKey = "Sdk.Info.Option()";

        private const string OptionBootupKey = "Sdk.Option.Bootup()";

        private const string InfoLostKey = "Sdk.Info.Lost()";

        private const string InfoRawKey = "Sdk.Info.Raw()";

        private const string NotedKey = "Sdk.Form.Noted(Sdk.Note[])";

        private const string TakesKey = "Sdk.Form.Takes(System.Object)";

        private const string MakeKey = "Sdk.Form.Make(Sdk.Form)";

        private const string MakeManyKey = "Sdk.Form.MakeMany()";

        private const string MakeOneKey = "Sdk.Form.MakeOne()";

        private const string DropKey = "Sdk.Form.Drop()";

        private const string ShareTextKey = "Sdk.Form.Share(System.String,System.String)";

        private const string ShareBytesKey = "Sdk.Form.Share(System.String,System.Byte[])";

        private readonly string _root;

        private readonly HostLog _log;

        /// <summary>最後に撮った画像。手放されたかをここで見る。</summary>
        private System.Drawing.Bitmap _shot;

        /// <summary>撮ったものを詰められる形で返すか。偽なら詰める段が落ちる。</summary>
        private bool _packable = true;

        /// <summary>詰められない持ち物。詰める段が落ちた回に手放されたかを見る。</summary>
        private Throwaway _unpackable;

        private readonly Target _target = new Target();

        private readonly Target _pmxSetting = new Target();

        private readonly Target _transformSetting = new Target();

        private readonly Target _subSetting = new Target();

        private readonly FakePmxView _view = new FakePmxView();

        private readonly FakeFormConnector _form = new FakeFormConnector();

        private Info _info;

        private Info[] _infos;

        public ToolDispatchTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-dispatch-" + Guid.NewGuid().ToString("N"));
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
        public void WritingTheSelectionPaintsTheViewAgain()
        {
            IDictionary<string, object> envelope = Call(
                "view_set_selected", Arguments("indices", new object[] { 1, 2 }));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal(new[] { 1, 2 }, _target.Selected);
            Assert.Equal(1, _view.Repaints);
            Assert.Equal(0, _view.Redraws);
            Assert.Empty(_form.Updated);
        }

        [Fact]
        public void ASelectionThatIsRefusedLeavesTheViewAsItWas()
        {
            IDictionary<string, object> envelope = Call(
                "view_set_selected", Arguments("indices", "数でない"));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Equal(0, _view.Repaints);
        }

        [Fact]
        public void ACallThatDoesNotShowOnTheScreenLeavesTheViewAsItWas()
        {
            Call("session_save", Arguments("path", "a.pmx", "confirm", true));

            Assert.Equal(0, _view.Repaints);
            Assert.Equal(0, _view.Redraws);
        }

        [Fact]
        public void AShotTakenForTheAnswerIsLetGoOnceItIsPacked()
        {
            IDictionary<string, object> envelope = Call("view_shot", Arguments());

            Assert.True((bool)envelope["ok"]);
            Assert.Throws<ArgumentException>(() => _shot.Width);
        }

        [Fact]
        public void AShotIsLetGoEvenWhenPackingItThrows()
        {
            _packable = false;

            Assert.Throws<InvalidCastException>(() => Call("view_shot", Arguments()));
            Assert.True(_unpackable.Released);
        }

        [Fact]
        public void APagedCallReturnsTheSliceWithTheTotalAndWhereToGoOn()
        {
            _target.Paged = Enumerable.Range(0, 10).ToArray();

            IDictionary<string, object> value = Value(
                Call("session_paged", Arguments("offset", 2, "limit", 3)));

            Assert.Equal(10, value["total"]);
            Assert.Equal(new object[] { 2, 3, 4 }, (object[])value["items"]);
            Assert.Equal(5, value["nextOffset"]);
        }

        [Fact]
        public void APagedCallWithoutOffsetOrLimitReturnsTheWholeListWhenItFits()
        {
            _target.Paged = Enumerable.Range(0, 10).ToArray();

            IDictionary<string, object> value = Value(Call("session_paged", Arguments()));

            Assert.Equal(10, value["total"]);
            Assert.Equal(10, ((object[])value["items"]).Length);
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void APagedCallCutsTheListDownToWhatTheAnswerCanHold()
        {
            _target.Paged = Enumerable.Range(0, 200000).ToArray();

            IDictionary<string, object> envelope = Call("session_paged", Arguments());
            IDictionary<string, object> value = Value(envelope);
            int taken = ((object[])value["items"]).Length;

            Assert.Equal(200000, value["total"]);
            Assert.InRange(taken, 1, 199999);
            Assert.Equal(taken, value["nextOffset"]);
        }

        [Fact]
        public void APagedCallOnHeldReceiversSlicesTheListOfEachOne()
        {
            HandleLedger ledger = Ledger();
            ledger.Issue(TargetType, new Target { Paged = new[] { 0, 1, 2, 3, 4 } }, () => { });
            ledger.Issue(TargetType, new Target { Paged = new[] { 0, 1, 2 } }, () => { });
            IDictionary<string, object> arguments = Arguments("offset", 1, "limit", 2);
            arguments.Add("handles", new object[] { 1L, 2L });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_paged_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));
            object[] each = (object[])envelope["value"];
            IDictionary<string, object> first = (IDictionary<string, object>)each[0];
            IDictionary<string, object> second = (IDictionary<string, object>)each[1];

            Assert.Equal(5, first["total"]);
            Assert.Equal(new object[] { 1, 2 }, (object[])first["items"]);
            Assert.Equal(3, first["nextOffset"]);
            Assert.Equal(3, second["total"]);
            Assert.Equal(new object[] { 1, 2 }, (object[])second["items"]);
            Assert.False(second.ContainsKey("nextOffset"));
        }

        [Fact]
        public void APagedCallSlicesTheItemsReadOutOfEachReturnedThing()
        {
            _infos = new[] { new Info { Name = "一" }, new Info { Name = "二" }, new Info { Name = "三" } };

            IDictionary<string, object> value = Value(
                Call("session_infos_paged", Arguments("offset", 1, "limit", 1)));
            object[] items = (object[])value["items"];

            Assert.Equal(3, value["total"]);
            Assert.Equal("二", ((IDictionary<string, object>)Assert.Single(items))["name"]);
            Assert.Equal(2, value["nextOffset"]);
        }

        [Fact]
        public void EveryPageOfAPagedCallOnHeldReceiversFitsInTheValueFrameAsAWhole()
        {
            const int Budget = 10000;
            HandleLedger ledger = Ledger();
            List<object> handles = new List<object>();
            for (int at = 0; at < 40; at++)
            {
                ledger.Issue(
                    TargetType,
                    new Target { Paged = Enumerable.Range(0, 1000).ToArray() },
                    () => { });
                handles.Add((long)(at + 1));
            }

            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", handles.ToArray());

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_paged_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), Budget, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.InRange(
                new System.Web.Script.Serialization.JavaScriptSerializer()
                    .Serialize(envelope["value"]).Length,
                1,
                ResponseSize.ValueChars(Budget));
        }

        [Fact]
        public void APagedCallOnMoreHeldReceiversThanTheValueFrameHoldsIsRefused()
        {
            HandleLedger ledger = Ledger();
            List<object> handles = new List<object>();
            for (int at = 0; at < 600; at++)
            {
                ledger.Issue(TargetType, new Target(), () => { });
                handles.Add((long)(at + 1));
            }

            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", handles.ToArray());

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_paged_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 10000, ledger, Events()));

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(envelope));
            Assert.Contains("handles", Message(envelope));
        }

        [Fact]
        public void APagedCallRefusesALimitOfZero()
        {
            IDictionary<string, object> envelope = Call("session_paged", Arguments("limit", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void ACallThatIsNotPagedRefusesAnOffset()
        {
            IDictionary<string, object> envelope = Call("session_picked", Arguments("offset", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void ADangerousCallWithoutTheConfirmationIsRefused()
        {
            IDictionary<string, object> envelope = Call("session_save", Arguments("path", "a.pmx"));

            Assert.Equal(ToolEnvelope.ConfirmRequired, Code(envelope));
            Assert.Null(_target.Saved);
        }

        [Fact]
        public void ADangerousCallWithTheConfirmationReachesTheMember()
        {
            IDictionary<string, object> envelope = Call(
                "session_save", Arguments("path", "a.pmx", "confirm", true));

            Assert.True((bool)envelope["ok"]);
            Assert.Equal("a.pmx", _target.Saved);
        }

        [Fact]
        public void TheViewChosenForASettingDecidesWhichViewsSettingIsReached()
        {
            Assert.True((bool)Call("view_save_setting", Arguments("path", "t.xml", "view", "transformView"))["ok"]);
            Assert.True((bool)Call("view_save_setting", Arguments("path", "s.xml", "view", "subView"))["ok"]);
            Assert.True((bool)Call("view_save_setting", Arguments("path", "p.xml", "view", "pmxView"))["ok"]);

            Assert.Equal("t.xml", _transformSetting.Saved);
            Assert.Equal("s.xml", _subSetting.Saved);
            Assert.Equal("p.xml", _pmxSetting.Saved);
        }

        [Fact]
        public void ASettingWithoutAViewIsThePmxViewsSetting()
        {
            Assert.True((bool)Call("view_save_setting", Arguments("path", "p.xml"))["ok"]);

            Assert.Equal("p.xml", _pmxSetting.Saved);
            Assert.Null(_transformSetting.Saved);
        }

        [Fact]
        public void TheViewChosenForASettingIsReadAndWrittenThere()
        {
            _subSetting.Count = 7;

            Assert.Equal(7, Value(Call("view_get_setting", Arguments("view", "subView")))["count"]);
            Assert.True((bool)Call("view_update_setting", Arguments("flag", true, "view", "transformView"))["ok"]);

            Assert.True(_transformSetting.Flag);
            Assert.False(_pmxSetting.Flag);
        }

        [Fact]
        public void AViewThatIsNotKnownIsRefusedAndNotTakenByOtherReceivers()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call("view_save_setting", Arguments("path", "x.xml", "view", "front"))));
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call("session_save", Arguments("path", "a.pmx", "confirm", true, "view", "transformView"))));

            Assert.Null(_pmxSetting.Saved);
            Assert.Null(_target.Saved);
        }

        [Fact]
        public void AToolThatReturnsNothingCarriesNoValue()
        {
            IDictionary<string, object> envelope = Call(
                "session_save", Arguments("path", "a.pmx", "confirm", true));

            Assert.Null(envelope["value"]);
        }

        [Fact]
        public void TheValueOfTheMemberComesBackInTheEnvelope()
        {
            _target.Count = 5;

            IDictionary<string, object> envelope = Call("session_count", Arguments());

            Assert.Equal(5, envelope["value"]);
        }

        [Fact]
        public void AnArgumentTheToolDoesNotTakeIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "session_count", Arguments("depth", 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheConfirmationIsNotTakenByAToolThatDoesNotNeedIt()
        {
            IDictionary<string, object> envelope = Call(
                "session_count", Arguments("confirm", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AnArgumentThatIsNotPassedIsRefused()
        {
            IDictionary<string, object> envelope = Call("session_save", Arguments("confirm", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_target.Saved);
        }

        [Fact]
        public void AnArgumentOfAnotherShapeIsRefusedBeforeTheCall()
        {
            IDictionary<string, object> envelope = Call(
                "session_save", Arguments("path", 1, "confirm", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Null(_target.Saved);
        }

        [Fact]
        public void ARowTheRelayCannotCarryIsRefusedAsNotApplicable()
        {
            IDictionary<string, object> envelope = Call("session_lost", Arguments());

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
        }

        [Fact]
        public void AMemberThatThrowsIsRefusedAsAFailedOperation()
        {
            IDictionary<string, object> envelope = Call("session_throw", Arguments());

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("題材の失敗", Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void ACallThatTheHostWillNotRunIsRefusedAsNotApplicable()
        {
            IDictionary<string, object> envelope = (IDictionary<string, object>)Method("session_count")(
                new McpMethodContext(
                    Arguments(), new RefusingInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
        }

        [Fact]
        public void ACallHeldByAModalIsRefusedWithWhatIsShownAndWithTheResultUnknown()
        {
            IDictionary<string, object> envelope = (IDictionary<string, object>)Method("session_count")(
                new McpMethodContext(
                    Arguments(),
                    new BlockedInvoker(
                        "エディタが人の応答を待つ表示を出していて進められない。"
                            + "表示: 確認: 未保存の編集項目があります"),
                    100000,
                    Ledger(),
                    Events()));

            Assert.Equal(ToolEnvelope.PromptShown, Code(envelope));
            Assert.Equal(
                "エディタが人の応答を待つ表示を出していて進められない。"
                    + "表示: 確認: 未保存の編集項目があります",
                Message(envelope));
        }

        [Fact]
        public void ACallTheHostNeverStartedIsRefusedSoTheCallerCanSendItAgain()
        {
            IDictionary<string, object> envelope = (IDictionary<string, object>)Method("session_count")(
                new McpMethodContext(
                    Arguments(),
                    new UnstartedInvoker("前の呼び出しがまだ終わっていない。"),
                    100000,
                    Ledger(),
                    Events()));

            Assert.Equal(ToolEnvelope.NotStarted, Code(envelope));
            Assert.Equal("前の呼び出しがまだ終わっていない。", Message(envelope));
        }

        [Theory]
        [InlineData(PreconditionKind.PickedObjects, false, false)]
        [InlineData(PreconditionKind.PickedObjects, false, true)]
        [InlineData(PreconditionKind.PickedObjects, true, true)]
        [InlineData(PreconditionKind.SavedEdits, false, false)]
        [InlineData(PreconditionKind.SavedEdits, true, false)]
        [InlineData(PreconditionKind.SavedEdits, true, true)]
        [InlineData(PreconditionKind.ListedParts, false, false)]
        [InlineData(PreconditionKind.ListedParts, true, false)]
        [InlineData(PreconditionKind.ListedParts, true, true)]
        public void AToolWhoseMaterialDoesNotMatchItsKindStopsTheBuild(
            PreconditionKind kind, bool reading, bool counting)
        {
            Assert.Throws<InvalidOperationException>(
                () => Registered(
                    new ToolPrecondition(
                        kind,
                        reading ? new[] { "session_picked" } : new string[0],
                        counting ? new[] { PickedKey } : new string[0])));
        }

        [Theory]
        [InlineData(PreconditionKind.PickedObjects, true, false)]
        [InlineData(PreconditionKind.SavedEdits, false, true)]
        [InlineData(PreconditionKind.ListedParts, false, true)]
        public void AToolWhoseMaterialMatchesItsKindIsBuilt(
            PreconditionKind kind, bool reading, bool counting)
        {
            Assert.NotNull(
                Registered(
                    new ToolPrecondition(
                        kind,
                        reading ? new[] { "session_picked" } : new string[0],
                        counting ? new[] { PickedKey } : new string[0])));
        }

        [Fact]
        public void AToolThatNeedsSomethingPickedRunsWhenSomethingIsPicked()
        {
            _target.Picked = new[] { 3 };
            _target.Count = 7;

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Picking(new StillModifierKeys())(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(7, envelope[ToolEnvelope.ValueName]);
        }

        [Fact]
        public void AToolThatNeedsSomethingPickedIsRefusedWhenNothingIsPicked()
        {
            _target.Picked = new int[0];

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Picking(new StillModifierKeys())(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("選ばれていない", Message(envelope));
        }

        [Fact]
        public void AToolThatNeedsAListRunsWhenTheListHasItemsOnIt()
        {
            _target.Count = 9;

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Listing()(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(9, envelope[ToolEnvelope.ValueName]);
        }

        [Fact]
        public void AToolThatNeedsAListIsRefusedWhenTheListHasNotBeenBuilt()
        {
            _target.Count = 0;

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Listing()(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("絞込の窓を一度表示するまで組まれず", Message(envelope));
        }

        [Fact]
        public void AToolThatNeedsSomethingPickedIsRefusedWhenWhatIsPickedCannotBeRead()
        {
            _target.Picked = null;

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Picking(new StillModifierKeys())(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("数えられなかった", Message(envelope));
        }

        [Fact]
        public void AToolThatNeedsSomethingPickedIsRefusedWhileAModifierIsHeld()
        {
            _target.Picked = new[] { 3 };

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Picking(new HeldModifierKeys())(
                    new McpMethodContext(Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("修飾キー", Message(envelope));
        }

        [Fact]
        public void ReleasingWhatTheIssuedThingWasMadeFromAlsoReleasesTheIssuedThing()
        {
            Target source = new Target { Made = new Target() };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));
            Assert.True(ToolEnvelope.Succeeded(envelope));
            int issued = Convert.ToInt32(
                envelope[ToolEnvelope.ValueName], CultureInfo.InvariantCulture);

            HandleReleaseResult released;
            Assert.True(ledger.TryRelease(1, out released));

            Assert.Empty(released.Failed);
            Assert.False(ledger.IsValid(issued));
        }

        [Fact]
        public void AnIssuedListenerIsSubscribedAndUnsubscribedWithItsHandle()
        {
            Target source = new Target { Made = new Target() };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, source, () => { });
            EventQueue queue = Events();
            EventSink held = null;
            bool detached = false;
            EventBindingTable events = new EventBindingTable(
                new Dictionary<string, EventAttach>(StringComparer.Ordinal)
                {
                    {
                        typeof(Target).FullName,
                        (listener, sink) =>
                        {
                            held = sink;

                            return () => detached = true;
                        }
                    },
                },
                new Dictionary<string, PayloadReader>(StringComparer.Ordinal)
                {
                    {
                        "session_made",
                        args => new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "note", args },
                        }
                    },
                });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held", events)(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, queue));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            int issued = Convert.ToInt32(
                envelope[ToolEnvelope.ValueName], CultureInfo.InvariantCulture);
            Assert.NotNull(held);

            held("session_made", "題材");

            EventDrainResult drained = queue.Drain(10);
            QueuedEvent queued = Assert.Single(drained.Events);
            Assert.Equal("session_made", queued.Type);
            Assert.Equal(issued, queued.SourceHandle);
            Assert.Equal(
                "題材", ((IDictionary<string, object>)queued.Payload)["note"]);

            HandleReleaseResult released;
            Assert.True(ledger.TryRelease(issued, out released));
            Assert.True(detached);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void WhatAHeldCallMakesIsReleasedBeforeBothTheReceiverAndTheArgument(int first)
        {
            Target owner = new Target { Made = new Target() };
            Target source = new Target();
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L });
            arguments.Add(
                "args",
                new Dictionary<string, object>(StringComparer.Ordinal) { { "source", 2L } });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, owner, () => { });
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_on_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            object[] handed = (object[])envelope[ToolEnvelope.ValueName];
            int issued = Convert.ToInt32(handed[0], CultureInfo.InvariantCulture);

            HandleReleaseResult released;
            Assert.True(ledger.TryRelease(first, out released));
            Assert.False(ledger.IsValid(issued));
        }

        [Fact]
        public void WhatAHeldCallMakesForEarlierTargetsIsNotLeftWhenALaterOneMakesNothing()
        {
            Target first = new Target { Made = new Target() };
            Target second = new Target();
            Target source = new Target();
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L, 2L });
            arguments.Add(
                "args",
                new Dictionary<string, object>(StringComparer.Ordinal) { { "source", 3L } });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, first, () => { });
            ledger.Issue(typeof(Target).FullName, second, () => { });
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_on_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Equal(3, ledger.LastIssuedId);
        }

        [Fact]
        public void TheHostPutsInTheCurrentPmxEvenWhenTheReceiverComesFromAHandle()
        {
            Target held = new Target();
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, held, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_takes_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.NotNull(held.Taken);
        }

        [Fact]
        public void ACallOnAHeldReceiverDoesNotTakeWhichPmxToLookAt()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L });
            arguments.Add(PmxSession.HandleName, 2L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, new Target(), () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_takes_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains(PmxSession.HandleName, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void ACallOnAHeldReceiverRunsOnEveryHandle()
        {
            Target first = new Target { Count = 3 };
            Target second = new Target { Count = 5 };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L, 2L });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, first, () => { });
            ledger.Issue(typeof(Target).FullName, second, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_count_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(new object[] { 3, 5 }, (IEnumerable<object>)envelope[ToolEnvelope.ValueName]);
        }

        [Fact]
        public void ACallOnAHeldReceiverTakesAHandleOfATypeThatDerivesFromIts()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Twin).FullName, new Twin { Count = 7 }, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_count_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(new object[] { 7 }, (IEnumerable<object>)envelope[ToolEnvelope.ValueName]);
        }

        [Fact]
        public void EachThingOfAnIssuedRowGoesIntoTheLedgerUnderItsOwnHandle()
        {
            Target first = new Target();
            Target second = new Target();
            _target.Twins = new[] { first, second };
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_many")(
                    new McpMethodContext(
                        Arguments(), new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            object[] handed = (object[])envelope[ToolEnvelope.ValueName];
            Assert.Equal(2, handed.Length);
            object held;
            Assert.True(ledger.TryGet((int)handed[0], typeof(Target).FullName, out held));
            Assert.Same(first, held);
            Assert.True(ledger.TryGet((int)handed[1], typeof(Target).FullName, out held));
            Assert.Same(second, held);
        }

        [Fact]
        public void ACallThatMakesOneThingStillRespondsInARowWhenItsToolCanMakeMany()
        {
            Target made = new Target();
            _target.Made = made;
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_one")(
                    new McpMethodContext(
                        Arguments(), new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            object[] handed = (object[])envelope[ToolEnvelope.ValueName];
            object held;
            Assert.True(ledger.TryGet((int)handed[0], typeof(Target).FullName, out held));
            Assert.Same(made, held);
        }

        [Fact]
        public void AToolThatRespondsInARowMakesAsManyThingsAsAsked()
        {
            Target made = new Target();
            _target.Made = made;
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_one")(
                    new McpMethodContext(
                        Arguments("count", 3L), new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            object[] handed = (object[])envelope[ToolEnvelope.ValueName];
            Assert.Equal(3, handed.Length);
            Assert.Equal(3, handed.Distinct().Count());
            foreach (object one in handed)
            {
                object held;
                Assert.True(ledger.TryGet((int)one, typeof(Target).FullName, out held));
                Assert.Same(made, held);
            }
        }

        [Fact]
        public void EachThingOfAnIssuedRowIsMadeAsManyTimesAsAsked()
        {
            _target.Twins = new[] { new Target(), new Target() };
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_many")(
                    new McpMethodContext(
                        Arguments("count", 2L), new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(4, ((object[])envelope[ToolEnvelope.ValueName]).Length);
        }

        [Fact]
        public void AskingForNoneOfAThingIsRefusedWithNoHandleLeftInTheLedger()
        {
            _target.Made = new Target();
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_one")(
                    new McpMethodContext(
                        Arguments("count", 0L), new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("count は 1 以上", Message(envelope));
            Assert.Equal(0, ledger.LastIssuedId);
        }

        [Fact]
        public void AskingForMoreThingsThanTheAnswerCanCarryIsRefused()
        {
            _target.Made = new Target();
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_one")(
                    new McpMethodContext(
                        Arguments("count", 728L), new InlineInvoker(), 10000, ledger, Events()));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("count は 727 以下", Message(envelope));
            Assert.Equal(0, ledger.LastIssuedId);
        }

        [Fact]
        public void ARowThatMakesManyAtOnceStopsWhenTheAnswerCanCarryNoMore()
        {
            Target[] twins = new Target[728];
            for (int at = 0; at < twins.Length; at++)
            {
                twins[at] = new Target();
            }

            _target.Twins = twins;
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_many")(
                    new McpMethodContext(
                        Arguments("count", 2L), new InlineInvoker(), 10000, ledger, Events()));

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(envelope));
            Assert.Equal(0, ledger.LastIssuedId);
        }

        [Fact]
        public void ARowThatMakesManyAtOnceIsAlsoCheckedOnItsLastTurn()
        {
            Target[] twins = new Target[400];
            for (int at = 0; at < twins.Length; at++)
            {
                twins[at] = new Target();
            }

            _target.Twins = twins;
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_many")(
                    new McpMethodContext(
                        Arguments("count", 2L), new InlineInvoker(), 10000, ledger, Events()));

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(envelope));
            Assert.Equal(0, ledger.LastIssuedId);
        }

        [Fact]
        public void ACallThatRespondsWithOneHandleDoesNotTakeACount()
        {
            Target source = new Target { Made = new Target() };
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(
                        Arguments("source", 1L, "count", 2L),
                        new InlineInvoker(),
                        100000,
                        ledger,
                        Events()));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("count", Message(envelope));
        }

        [Fact]
        public void AGapInAnIssuedRowIsRefusedWithNoHandleLeftInTheLedger()
        {
            _target.Twins = new Target[] { new Target(), null };
            HandleLedger ledger = Ledger();

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_many")(
                    new McpMethodContext(
                        Arguments(), new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Equal(0, ledger.LastIssuedId);
        }

        [Fact]
        public void ACallOnAHeldReceiverIsRefusedWithoutHandles()
        {
            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_count_held")(
                    new McpMethodContext(
                        Arguments(), new InlineInvoker(), 100000, Ledger(), Events()));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("handles", Message(envelope));
        }

        [Fact]
        public void ACallOnAHeldReceiverIsRefusedWhenTheHandleIsNotItsType()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Info).FullName, new Info(), () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_count_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
        }

        [Fact]
        public void AnArgumentTakenAsAHandleReachesTheCallAsTheHeldThing()
        {
            Target made = new Target();
            Target source = new Target { Made = made };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            object issued = envelope[ToolEnvelope.ValueName];
            object held;
            Assert.True(ledger.TryGet(
                Convert.ToInt32(issued, CultureInfo.InvariantCulture),
                typeof(Target).FullName,
                out held));
            Assert.Same(made, held);
        }

        [Fact]
        public void AnArgumentTakenAsAHandleIsRefusedWhenItIsNotTheThingTheCallTakes()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Info).FullName, new Info(), () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
            Assert.Contains("source", Message(envelope), StringComparison.Ordinal);
        }

        /// <summary>
        /// 台帳が覚えている型の名前で照らすと、その型を継ぐ実体を基底の型の引数へ渡せない。
        /// 渡せるかどうかを決めるのは実体の側である。
        /// </summary>
        [Fact]
        public void AnArgumentTakenAsAHandleAcceptsAThingThatExtendsWhatTheCallTakes()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Twin).FullName, new Twin { Made = new Target() }, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
        }

        [Fact]
        public void ReleasingAHandleAlsoLetsTheSdkGoOfTheIssuedThing()
        {
            Target made = new Target();
            Target source = new Target { Made = made };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("source", 1L);
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, source, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_make_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));
            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.False(made.Dropped);

            HandleReleaseResult released;
            Assert.True(ledger.TryRelease(
                Convert.ToInt32(
                    envelope[ToolEnvelope.ValueName], CultureInfo.InvariantCulture),
                out released));

            Assert.Empty(released.Failed);
            Assert.True(made.Dropped);
        }

        [Fact]
        public void TheUpdatingToolOnHeldTargetsTakesAValueForEach()
        {
            Target first = new Target();
            Target second = new Target { Flag = true };
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("handles", new object[] { 1L, 2L });
            arguments.Add(
                "values",
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal) { { "flag", true } },
                    new Dictionary<string, object>(StringComparer.Ordinal) { { "flag", false } },
                });
            HandleLedger ledger = Ledger();
            ledger.Issue(typeof(Target).FullName, first, () => { });
            ledger.Issue(typeof(Target).FullName, second, () => { });

            IDictionary<string, object> envelope = (IDictionary<string, object>)
                Method("session_update_held")(
                    new McpMethodContext(arguments, new InlineInvoker(), 100000, ledger, Events()));

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.True(first.Flag);
            Assert.False(second.Flag);
        }

        [Fact]
        public void AnArgumentTakenAsSetsOfMembersReachesTheCallAsBuiltThings()
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add(
                "notes",
                new object[]
                {
                    Members(1, true),
                    Members(2, false),
                });

            IDictionary<string, object> envelope = Call("session_noted", arguments);

            Assert.True(ToolEnvelope.Succeeded(envelope));
            Assert.Equal(2, _target.Notes.Length);
            Assert.Equal(1, _target.Notes[0].Count);
            Assert.True(_target.Notes[0].Flag);
            Assert.Equal(2, _target.Notes[1].Count);
            Assert.False(_target.Notes[1].Flag);
        }

        [Fact]
        public void ASetWithAnItemTheCallDoesNotTakeIsRefused()
        {
            IDictionary<string, object> arguments = Arguments();
            IDictionary<string, object> members = Members(1, true);
            members.Add("unknown", 0L);
            arguments.Add("notes", new object[] { members });

            IDictionary<string, object> envelope = Call("session_noted", arguments);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("unknown", Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void ASetMissingAnItemTheCallTakesIsRefused()
        {
            IDictionary<string, object> arguments = Arguments();
            IDictionary<string, object> members = Members(1, true);
            members.Remove("flag");
            arguments.Add("notes", new object[] { members });

            IDictionary<string, object> envelope = Call("session_noted", arguments);

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("flag", Message(envelope), StringComparison.Ordinal);
        }

        private static IDictionary<string, object> Members(int count, bool flag)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "count", (long)count },
                { "flag", flag },
            };
        }

        [Fact]
        public void TheGettingToolReturnsEveryReadableItem()
        {
            _target.Count = 3;
            _target.Flag = true;

            IDictionary<string, object> value = Value(Call("session_get_form", Arguments()));

            Assert.Equal(new[] { "count", "flag" }, value.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(3, value["count"]);
            Assert.Equal(true, value["flag"]);
        }

        [Fact]
        public void TheGettingToolReturnsOnlyTheChosenItems()
        {
            IDictionary<string, object> value = Value(
                Call("session_get_form", Arguments("fields", new object[] { "flag" })));

            Assert.Equal(new[] { "flag" }, value.Keys.ToArray());
        }

        [Fact]
        public void AnItemTheGettingToolDoesNotCarryIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "session_get_form", Arguments("fields", new object[] { "depth" }));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void TheUpdatingToolWritesTheItemsItIsGiven()
        {
            IDictionary<string, object> envelope = Call(
                "session_update_form", Arguments("flag", true));

            Assert.True(_target.Flag);
            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
        }

        [Fact]
        public void AnItemThatReadsBackOtherThanWhatWasWrittenIsAFailure()
        {
            _target.Count = 3;

            IDictionary<string, object> envelope = Call("session_update_stuck", Arguments("stuck", 5));

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Contains("3", (string)((IDictionary<string, object>)envelope["error"])["message"]);
        }

        [Fact]
        public void AFractionReadBackTheSameToSevenDigitsIsASuccess()
        {
            IDictionary<string, object> envelope = Call("session_update_stuck", Arguments("size", 33.333333d));

            Assert.Equal(1, Value(envelope)[SetResponse.UpdatedName]);
            Assert.Equal("33.33333", _target.SizeText);
        }

        [Fact]
        public void AnUpdateThatChoosesNoItemIsRefused()
        {
            IDictionary<string, object> envelope = Call("session_update_form", Arguments());

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
        }

        [Fact]
        public void AnUpdateThatChoosesTwoItemsIsRefusedWithoutWritingEither()
        {
            IDictionary<string, object> envelope = Call(
                "session_update_pair", Arguments("flag", true, "broken", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.False(_target.Flag);
        }

        [Fact]
        public void AnUpdateOfAnotherShapeIsRefusedBeforeAnythingIsWritten()
        {
            IDictionary<string, object> envelope = Call(
                "session_update_form", Arguments("flag", "true"));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.False(_target.Flag);
        }

        [Fact]
        public void AnUpdateWhoseItemCannotBeRelayedWritesNothing()
        {
            IDictionary<string, object> envelope = Call(
                "session_update_pair", Arguments("lost", true));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.False(_target.Flag);
        }

        [Fact]
        public void AnItemTheUpdatingToolDoesNotCarryIsRefused()
        {
            IDictionary<string, object> envelope = Call(
                "session_update_form", Arguments("count", 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
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

        private IDictionary<string, object> Call(string tool, IDictionary<string, object> arguments)
        {
            return (IDictionary<string, object>)Method(tool)(
                new McpMethodContext(arguments, new InlineInvoker(), 100000, Ledger(), Events()));
        }

        private McpMethod Method(string tool)
        {
            return Method(tool, EventBindingFixture.Empty());
        }

        private McpMethod Method(string tool, EventBindingTable events)
        {
            McpMethodTable methods = new McpMethodTable();
            SdkRelayTable relay = Relay();
            IDictionary<string, SdkReceiver> receivers = Receivers();
            ResidentConnection connection = Connection();
            PmxSession session = Session(relay, receivers, connection);
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                new Dictionary<string, SdkList>(StringComparer.Ordinal),
                connection,
                session,
                Session(relay, receivers, connection),
                new UndoRecovery(new UndoSuppression(_log), session.UndoLock),
                Calls(),
                Aggregations(),
                new Dictionary<string, ToolElements>(StringComparer.Ordinal),
                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal),
                new StillModifierKeys(),
                events,
                Refresh(),
                Screen());

            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていないツール: " + tool);

            return method;
        }

        /// <summary>題材の複製編集の流れ。PMXを相手にしない題材なので、同じ行を両端に置く。</summary>
        private PmxSession Session(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection)
        {
            return new PmxSession(
                relay,
                receivers,
                connection,
                new PmxFlow(CountKey, CountKey, TargetType, new FlowSlot[0], new[] { FlowSlot.Pmx }),
                typeof(object),
                new UndoSuppression(_log));
        }

        /// <summary>画面へ映す段。題材の口を通して、映し直しの回数を数える。</summary>
        private ScreenRefresh Refresh()
        {
            return new ScreenRefresh(() => _view, () => _form);
        }

        private ScreenTargets Screen()
        {
            return new ScreenTargets(() => _view, () => _form);
        }

        /// <summary>その前提条件を持つツールとして登録し、引いた呼び出しを返す。</summary>
        private McpMethod Registered(ToolPrecondition precondition)
        {
            McpMethodTable methods = new McpMethodTable();
            SdkRelayTable relay = Relay();
            IDictionary<string, SdkReceiver> receivers = Receivers();
            ResidentConnection connection = Connection();
            PmxSession session = Session(relay, receivers, connection);
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                new Dictionary<string, SdkList>(StringComparer.Ordinal),
                connection,
                session,
                Session(relay, receivers, connection),
                new UndoRecovery(new UndoSuppression(_log), session.UndoLock),
                Calls(),
                Aggregations(),
                new Dictionary<string, ToolElements>(StringComparer.Ordinal),
                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal)
                {
                    { "session_count", precondition },
                },
                new StillModifierKeys(),
                EventBindingFixture.Empty(),
                Refresh(),
                Screen());

            McpMethod method;
            Assert.True(methods.TryGet("session_count", out method));

            return method;
        }

        /// <summary>絞込の一覧に項目が並んでいることが要るツールとして、題材の呼び出しを引く。</summary>
        private McpMethod Listing()
        {
            return Registered(
                new ToolPrecondition(
                    PreconditionKind.ListedParts, new string[0], new[] { CountKey }));
        }

        /// <summary>選ばれているものが要るツールとして、題材の呼び出しを引く。</summary>
        private McpMethod Picking(IModifierKeys modifiers)
        {
            McpMethodTable methods = new McpMethodTable();
            SdkRelayTable relay = Relay();
            IDictionary<string, SdkReceiver> receivers = Receivers();
            ResidentConnection connection = Connection();
            PmxSession session = Session(relay, receivers, connection);
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                new Dictionary<string, SdkList>(StringComparer.Ordinal),
                connection,
                session,
                Session(relay, receivers, connection),
                new UndoRecovery(new UndoSuppression(_log), session.UndoLock),
                Calls(),
                Aggregations(),
                new Dictionary<string, ToolElements>(StringComparer.Ordinal),
                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal)
                {
                    {
                        "session_count",
                        new ToolPrecondition(
                            PreconditionKind.PickedObjects,
                            new[] { "session_picked" },
                            new string[0])
                    },
                },
                modifiers,
                EventBindingFixture.Empty(),
                Refresh(),
                Screen());

            McpMethod method;
            Assert.True(methods.TryGet("session_count", out method));

            return method;
        }

        private HandleLedger Ledger()
        {
            return new HandleLedger(_log, new HandleIdIssuer());
        }

        private static EventQueue Events()
        {
            return new EventQueue(new EventSequenceIssuer());
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

        private IDictionary<string, SdkReceiver> Receivers()
        {
            return new Dictionary<string, SdkReceiver>(StringComparer.Ordinal)
            {
                { TargetType, connection => _target },
                { SettingType, connection => _pmxSetting },
                { ToolDispatch.ReceiverKey(SettingType, "transformView"), connection => _transformSetting },
                { ToolDispatch.ReceiverKey(SettingType, "subView"), connection => _subSetting },
            };
        }

        /// <summary>画像として詰められない持ち物。</summary>
        private sealed class Throwaway : IDisposable
        {
            /// <summary>手放されたか。</summary>
            public bool Released { get; private set; }

            public void Dispose()
            {
                Released = true;
            }
        }

        private SdkRelayTable Relay()
        {
            Dictionary<string, SdkCall> calls =
                new Dictionary<string, SdkCall>(StringComparer.Ordinal)
                {
                    {
                        SaveKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Saved = (string)arguments[0];
                            return null;
                        }
                    },
                    { CountKey, (target, arguments) => ((Target)target).Count },
                    {
                        ShotKey,
                        (target, arguments) =>
                        {
                            if (!_packable)
                            {
                                _unpackable = new Throwaway();

                                return _unpackable;
                            }

                            _shot = new System.Drawing.Bitmap(2, 2);

                            return _shot;
                        }
                    },
                    {
                        SelectionKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Selected = (int[])arguments[0];
                            return null;
                        }
                    },
                    {
                        ShareTextKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Shared = "text";
                            return null;
                        }
                    },
                    {
                        ShareBytesKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Shared = "base64";
                            return null;
                        }
                    },
                    { PickedKey, (target, arguments) => ((Target)target).Picked },
                    { PagedKey, (target, arguments) => ((Target)target).Paged },
                    {
                        FlagKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Target)target).Flag
                            : Write((Target)target, (bool)arguments[0])
                    },
                    { WriteDroppingKey, (target, arguments) => arguments.Length == 0 ? (object)((Target)target).Count : null },
                    {
                        TextHeldSizeKey,
                        (target, arguments) =>
                        {
                            if (arguments.Length == 0)
                            {
                                return float.Parse(((Target)target).SizeText, CultureInfo.InvariantCulture);
                            }

                            ((Target)target).SizeText = ((float)arguments[0]).ToString(CultureInfo.InvariantCulture);

                            return null;
                        }
                    },
                    { ThrowKey, (target, arguments) => { throw new InvalidOperationException("題材の失敗。"); } },
                    { InfoKey, (target, arguments) => _info },
                    { InfosKey, (target, arguments) => _infos },
                    { InfoNameKey, (target, arguments) => ((Info)target).Name },
                    { InfoOptionKey, (target, arguments) => ((Info)target).Option },
                    { OptionBootupKey, (target, arguments) => ((Option)target).Bootup },
                    { InfoRawKey, (target, arguments) => new Target() },
                    {
                        NotedKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Notes = (Note[])arguments[0];
                            return null;
                        }
                    },
                    {
                        MakeKey,
                        (target, arguments) => ((Target)(target ?? arguments[0])).Made
                            ?? ((Target)arguments[0]).Made
                    },
                    { MakeManyKey, (target, arguments) => ((Target)target).Twins },
                    { MakeOneKey, (target, arguments) => ((Target)target).Made },
                    {
                        TakesKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Taken = arguments[0];
                            return null;
                        }
                    },
                    {
                        DropKey,
                        (target, arguments) =>
                        {
                            ((Target)target).Dropped = true;
                            return null;
                        }
                    },
                };

            return new SdkRelayTable(SdkVersion, Digest, calls, new[] { LostKey });
        }

        private static object Write(Target target, bool flag)
        {
            target.Flag = flag;

            return null;
        }

        /// <summary>接続の道から受け手を得る、直に触る呼び出し。</summary>
        private static ToolReceiver Direct()
        {
            return new ToolReceiver(ToolReceiverKind.Connection, TargetType, EditKind.DirectChange);
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

        [Fact]
        public void AValueThatIsNotAShapeOfItsOwnComesBackAsItsItems()
        {
            _info = new Info { Name = "題材", Option = new Option { Bootup = true } };

            IDictionary<string, object> envelope = Call("session_info", Arguments());

            IDictionary<string, object> value =
                (IDictionary<string, object>)envelope["value"];
            Assert.Equal("題材", value["name"]);
            Assert.Equal(
                true, ((IDictionary<string, object>)value["option"])["bootup"]);
        }

        [Fact]
        public void ValuesThatAreNotAShapeOfTheirOwnComeBackAsItemsOneByOne()
        {
            _infos = new[] { new Info { Name = "一" }, new Info { Name = "二" } };

            IDictionary<string, object> envelope = Call("session_infos", Arguments());

            object[] value = (object[])envelope["value"];
            Assert.Equal(
                new[] { "一", "二" },
                value.Select(one => ((IDictionary<string, object>)one)["name"]).ToArray());
        }

        [Fact]
        public void AValueThatIsNotAShapeOfItsOwnComesBackEmptyWhenThereIsNone()
        {
            _info = null;

            IDictionary<string, object> envelope = Call("session_info", Arguments());

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Null(envelope["value"]);
        }

        [Fact]
        public void AnItemWithoutARelayIsRefusedTheSameWayAsAnyOtherCall()
        {
            _info = new Info { Name = "題材" };

            IDictionary<string, object> envelope = Call("session_info_lost", Arguments());

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains(InfoLostKey, Message(envelope), StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemThatCannotBeWrittenAsAValueIsRefusedWithItsOwnReason()
        {
            _info = new Info { Name = "題材" };

            IDictionary<string, object> envelope = Call("session_info_raw", Arguments());

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Contains("写せない", Message(envelope), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("text", "text")]
        [InlineData("base64", "base64")]
        public void TheSelectorChoosesWhichOverloadRuns(string given, string ran)
        {
            Call("session_share", Arguments("dataShape", given, "key", "k", "data", "v"));

            Assert.Equal(ran, _target.Shared);
        }

        [Theory]
        [InlineData("json")]
        [InlineData(null)]
        public void AValueThatSelectsNoOverloadIsRefused(string given)
        {
            IDictionary<string, object> envelope = Call(
                "session_share",
                given == null
                    ? Arguments("key", "k", "data", "v")
                    : Arguments("dataShape", given, "key", "k", "data", "v"));

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ((IDictionary<string, object>)envelope["error"])["code"]);
            Assert.Null(_target.Shared);
        }

        /// <summary>分岐を選ぶ項目で分かれる、引数の名前が同じ2つの呼び分け。</summary>
        private static IList<ToolCall> Shared()
        {
            return new[]
            {
                Sharing(ShareTextKey, "text"),
                Sharing(ShareBytesKey, "base64"),
            };
        }

        private static ToolCall Sharing(string rowKey, string shape)
        {
            return new ToolCall(
                rowKey,
                Direct(),
                ToolAccess.Whole(),
                DangerKind.None,
                new[]
                {
                    new ToolArgument("key", typeof(string)),
                    new ToolArgument("data", typeof(string)),
                },
                new ToolArgument[0],
                null,
                selectorName: "dataShape",
                selectorValue: shape);
        }

        private static IDictionary<string, IList<ToolCall>> Calls()
        {
            IDictionary<string, IList<ToolCall>> built =
                Singles(new Dictionary<string, ToolCall>(StringComparer.Ordinal)
            {
                {
                    "view_set_selected",
                    new ToolCall(
                        SelectionKey,
                        new ToolReceiver(
                            ToolReceiverKind.Connection, TargetType, EditKind.ViewSession),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[] { new ToolArgument("indices", typeof(int[])) },
                        new ToolArgument[0],
                        null)
                },
                {
                    "view_shot",
                    new ToolCall(
                        ShotKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(System.Drawing.Bitmap))
                },
                {
                    "session_save",
                    new ToolCall(
                        SaveKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.Overwrite,
                        new[] { new ToolArgument("path", typeof(string)) },
                        new ToolArgument[0],
                        null)
                },
                {
                    "view_save_setting",
                    new ToolCall(
                        SaveKey,
                        new ToolReceiver(ToolReceiverKind.Connection, SettingType, EditKind.DirectChange),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[] { new ToolArgument("path", typeof(string)) },
                        new ToolArgument[0],
                        null)
                },
                {
                    "session_count_held",
                    new ToolCall(
                        CountKey,
                        new ToolReceiver(
                            ToolReceiverKind.Handle,
                            TargetType,
                            EditKind.Read,
                            false,
                            item => item is Target),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(int))
                },
                {
                    "session_make_many",
                    new ToolCall(
                        MakeManyKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Target[]),
                        typeof(Target),
                        null,
                        null,
                        false,
                        true,
                        true)
                },
                {
                    "session_make_one",
                    new ToolCall(
                        MakeOneKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Target),
                        typeof(Target),
                        null,
                        null,
                        false,
                        false,
                        true)
                },
                {
                    "session_make_held",
                    new ToolCall(
                        MakeKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[]
                        {
                            new ToolArgument(
                                "source", typeof(Target), false, null, false, typeof(Target),
                                null, null, item => item is Target),
                        },
                        new ToolArgument[0],
                        typeof(Target),
                        typeof(Target),
                        null,
                        DropKey)
                },
                {
                    "session_make_on_held",
                    new ToolCall(
                        MakeKey,
                        new ToolReceiver(
                            ToolReceiverKind.Handle,
                            TargetType,
                            EditKind.ViewSession,
                            false,
                            item => item is Target),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[]
                        {
                            new ToolArgument(
                                "source", typeof(Target), false, null, false, typeof(Target),
                                null, null, item => item is Target),
                        },
                        new ToolArgument[0],
                        typeof(Target),
                        typeof(Target))
                },
                {
                    "session_takes_held",
                    new ToolCall(
                        TakesKey,
                        new ToolReceiver(
                            ToolReceiverKind.Handle,
                            TargetType,
                            EditKind.DirectChange,
                            false,
                            item => item is Target),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[] { new ToolArgument("pmx", typeof(object), true) },
                        new ToolArgument[0],
                        null)
                },
                {
                    "session_noted",
                    new ToolCall(
                        NotedKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new[]
                        {
                            new ToolArgument(
                                "notes",
                                typeof(Note[]),
                                false,
                                null,
                                false,
                                null,
                                null,
                                new ToolValueShape(
                                    () => new Note(),
                                    new[]
                                    {
                                        new ToolValueMember(
                                            "count",
                                            typeof(int),
                                            (made, value) => ((Note)made).Count = (int)value),
                                        new ToolValueMember(
                                            "flag",
                                            typeof(bool),
                                            (made, value) => ((Note)made).Flag = (bool)value),
                                    })),
                        },
                        new ToolArgument[0],
                        null)
                },
                {
                    "session_paged",
                    new ToolCall(
                        PagedKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(int[]),
                        paged: true)
                },
                {
                    "session_picked",
                    new ToolCall(
                        PickedKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(int[]))
                },
                {
                    "session_count",
                    new ToolCall(
                        CountKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(int))
                },
                {
                    "session_info",
                    new ToolCall(
                        InfoKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Info),
                        null,
                        new[]
                        {
                            new ToolField("name", InfoNameKey, typeof(string)),
                            new ToolField(
                                "option",
                                InfoOptionKey,
                                typeof(Option),
                                new[] { new ToolField("bootup", OptionBootupKey, typeof(bool)) }),
                        })
                },
                {
                    "session_infos_paged",
                    new ToolCall(
                        InfosKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Info[]),
                        null,
                        new[] { new ToolField("name", InfoNameKey, typeof(string)) },
                        null,
                        false,
                        true,
                        paged: true)
                },
                {
                    "session_paged_held",
                    new ToolCall(
                        PagedKey,
                        new ToolReceiver(
                            ToolReceiverKind.Handle,
                            TargetType,
                            EditKind.Read,
                            false,
                            item => item is Target),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(int[]),
                        paged: true)
                },
                {
                    "session_infos",
                    new ToolCall(
                        InfosKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Info[]),
                        null,
                        new[] { new ToolField("name", InfoNameKey, typeof(string)) },
                        null,
                        false,
                        true)
                },
                {
                    "session_info_lost",
                    new ToolCall(
                        InfoKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Info),
                        null,
                        new[] { new ToolField("lost", InfoLostKey, typeof(string)) })
                },
                {
                    "session_info_raw",
                    new ToolCall(
                        InfoKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        typeof(Info),
                        null,
                        new[] { new ToolField("raw", InfoRawKey, typeof(Target)) })
                },
                {
                    "session_lost",
                    new ToolCall(
                        LostKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        null)
                },
                {
                    "session_throw",
                    new ToolCall(
                        ThrowKey,
                        Direct(),
                        ToolAccess.Whole(),
                        DangerKind.None,
                        new ToolArgument[0],
                        new ToolArgument[0],
                        null)
                },
            });
            built.Add("session_share", Shared());

            return built;
        }

        /// <summary>型で分かれないツールの、1つだけの項目の組。</summary>
        private static IList<ToolFieldSet> Set(params ToolField[] fields)
        {
            return new[] { new ToolFieldSet(null, fields) };
        }

        private static IDictionary<string, ToolFields> Aggregations()
        {
            return new Dictionary<string, ToolFields>(StringComparer.Ordinal)
            {
                {
                    "session_get_form",
                    new ToolFields(
                        false,
                        false,
                        Direct(),
                        ToolAccess.Whole(),
                        Set(
                            new ToolField("count", CountKey, typeof(int)),
                            new ToolField("flag", FlagKey, typeof(bool))))
                },
                {
                    "session_update_form",
                    new ToolFields(
                        true,
                        false,
                        Direct(),
                        ToolAccess.Whole(),
                        Set(new ToolField("flag", FlagKey, typeof(bool))))
                },
                {
                    "session_update_stuck",
                    new ToolFields(
                        true,
                        false,
                        Direct(),
                        ToolAccess.Whole(),
                        Set(
                            new ToolField("stuck", WriteDroppingKey, typeof(int)),
                            new ToolField("size", TextHeldSizeKey, typeof(float))))
                },
                {
                    "view_get_setting",
                    new ToolFields(
                        false,
                        false,
                        new ToolReceiver(ToolReceiverKind.Connection, SettingType, EditKind.Read),
                        ToolAccess.Whole(),
                        Set(new ToolField("count", CountKey, typeof(int))))
                },
                {
                    "view_update_setting",
                    new ToolFields(
                        true,
                        false,
                        new ToolReceiver(ToolReceiverKind.Connection, SettingType, EditKind.DirectChange),
                        ToolAccess.Whole(),
                        Set(new ToolField("flag", FlagKey, typeof(bool))))
                },
                {
                    "session_update_held",
                    new ToolFields(
                        true,
                        true,
                        new ToolReceiver(
                            ToolReceiverKind.Handle,
                            TargetType,
                            EditKind.ViewSession,
                            false,
                            item => item is Target),
                        ToolAccess.Whole(),
                        Set(new ToolField("flag", FlagKey, typeof(bool))))
                },
                {
                    "session_update_pair",
                    new ToolFields(
                        true,
                        false,
                        Direct(),
                        ToolAccess.Whole(),
                        Set(
                            new ToolField("flag", FlagKey, typeof(bool)),
                            new ToolField("lost", LostKey, typeof(bool)),
                            new ToolField("broken", ThrowKey, typeof(bool))))
                },
            };
        }

        /// <summary>中継が読み書きする題材。</summary>
        private class Target
        {
            public int[] Picked { get; set; } = new int[0];

            public int[] Paged { get; set; } = new int[0];

            public string Saved { get; set; }

            public bool Flag { get; set; }

            public int Count { get; set; }

            public string SizeText { get; set; } = "0";

            public Target Made { get; set; }

            public bool Dropped { get; set; }

            public Note[] Notes { get; set; }

            public object Taken { get; set; }

            public Target[] Twins { get; set; }

            public string Shared { get; set; }

            public int[] Selected { get; set; } = new int[0];
        }

        /// <summary>題材を継いだ型。台帳はこちらの名前で覚える。</summary>
        private sealed class Twin : Target
        {
        }

        /// <summary>組で受け取ってSDKへ渡す題材。</summary>
        private sealed class Note
        {
            public int Count { get; set; }

            public bool Flag { get; set; }
        }

        /// <summary>独立したツールを持たず、返す値の中だけに現れる題材。</summary>
        private sealed class Info
        {
            public string Name { get; set; }

            public Option Option { get; set; }
        }

        private sealed class Option
        {
            public bool Bootup { get; set; }
        }

        /// <summary>人の応答を待つ表示でUIスレッドが塞がっている稼働世代のように答える。</summary>
        private sealed class BlockedInvoker : IUiInvoker
        {
            private readonly string _shown;

            public BlockedInvoker(string shown)
            {
                _shown = shown;
            }

            public UiInvocation TryInvokeOnUi(Action action)
            {
                return UiInvocation.Blocked(_shown);
            }
        }

        /// <summary>委譲を渡さないまま戻る稼働世代のように答える。</summary>
        private sealed class UnstartedInvoker : IUiInvoker
        {
            private readonly string _standing;

            public UnstartedInvoker(string standing)
            {
                _standing = standing;
            }

            public UiInvocation TryInvokeOnUi(Action action)
            {
                return UiInvocation.NotStarted(_standing);
            }
        }

        /// <summary>受付を止めた稼働世代のように、委譲された処理を実行しない。</summary>
        private sealed class RefusingInvoker : IUiInvoker
        {
            public UiInvocation TryInvokeOnUi(Action action)
            {
                return UiInvocation.Declined;
            }
        }
    }
}
