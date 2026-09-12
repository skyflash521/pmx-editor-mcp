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

        private const string StateReadKey = "Sdk.PmxConnector.GetCurrentState()";

        private const string CommitKey = "Sdk.PmxConnector.Update(Sdk.Pmx)";

        private const string FilePathKey = "Sdk.Pmx.FilePath()";

        private const string ListKey = "Sdk.Pmx.Vertex()";

        private const string ClearKey = "Sdk.Pmx.Clear()";

        private const string CompactKey = "Sdk.Pmx.Compact()";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly Model _model = new Model();

        private int _commits;

        private int _clones;

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
            Item item = new Item();
            int handle = handles.Issue(typeof(Item).FullName, item, () => { });

            IDictionary<string, object> envelope = Call(
                "model_add_vertices",
                Arguments(TargetNames.Element.Handles, new object[] { handle }),
                handles);

            Assert.Equal(1, Value(envelope)[SetResponse.AddedName]);
            Assert.Equal(new[] { 0 }, (int[])Value(envelope)[SetResponse.IndicesName]);
            Assert.Same(item, Assert.Single(_model.Items));
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
            _model.Items.Add(new Item());
            _model.Items.Add(new Item());
            Item kept = new Item();
            _model.Items.Add(kept);

            IDictionary<string, object> envelope = Call(
                "model_remove_vertices",
                Arguments(TargetNames.Element.Indices, new object[] { 0, 1 }));

            Assert.Equal(2, Value(envelope)[SetResponse.RemovedName]);
            Assert.Same(kept, Assert.Single(_model.Items));
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
                Elements());

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
                        StateReadKey,
                        (target, arguments) =>
                        {
                            _clones++;
                            return _model;
                        }
                    },
                    {
                        CommitKey,
                        (target, arguments) =>
                        {
                            if (_reflectionBreaks)
                            {
                                throw new InvalidOperationException("反映が落ちた。");
                            }

                            _commits++;
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
                    ListKey,
                    new SdkList(
                        owner => ((Model)owner).Items.Count,
                        (owner, index) => ((Model)owner).Items[index],
                        (owner, item) => ((Model)owner).Items.Add((Item)item),
                        (owner, index) => ((Model)owner).Items.RemoveAt(index))
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
                    "model_update_pmxes",
                    new ToolFields(
                        true, true, Rooted(EditKind.DuplicateEdit), ToolAccess.Whole(), Set(fields))
                },
            };
        }

        private static IDictionary<string, ToolElements> Elements()
        {
            return new Dictionary<string, ToolElements>(StringComparer.Ordinal)
            {
                {
                    "model_add_vertices",
                    new ToolElements(false, Rooted(EditKind.DuplicateEdit), Listed())
                },
                {
                    "model_remove_vertices",
                    new ToolElements(true, Rooted(EditKind.DuplicateEdit), Listed())
                },
            };
        }

        /// <summary>複製して編集する相手の題材。</summary>
        private sealed class Model
        {
            public string FilePath { get; set; }

            public bool Cleared { get; set; }

            public List<Item> Items { get; } = new List<Item>();
        }

        /// <summary>リストが並べる要素の題材。</summary>
        private sealed class Item
        {
        }
    }
}
