using System;
using System.Collections.Generic;
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

        private const string SaveKey = "Sdk.Form.Save(System.String)";

        private const string CountKey = "Sdk.Form.Count()";

        private const string FlagKey = "Sdk.Form.Flag()";

        private const string LostKey = "Sdk.Form.Lost()";

        private const string ThrowKey = "Sdk.Form.Throw()";

        private const string InfoKey = "Sdk.Form.Info()";

        private const string InfoNameKey = "Sdk.Info.Name()";

        private const string InfoOptionKey = "Sdk.Info.Option()";

        private const string OptionBootupKey = "Sdk.Option.Bootup()";

        private const string InfoLostKey = "Sdk.Info.Lost()";

        private const string InfoRawKey = "Sdk.Info.Raw()";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly Target _target = new Target();

        private Info _info;

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
                new Dictionary<string, ToolElements>(StringComparer.Ordinal));

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
            };
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
                        FlagKey,
                        (target, arguments) => arguments.Length == 0
                            ? (object)((Target)target).Flag
                            : Write((Target)target, (bool)arguments[0])
                    },
                    { ThrowKey, (target, arguments) => { throw new InvalidOperationException("題材の失敗。"); } },
                    { InfoKey, (target, arguments) => _info },
                    { InfoNameKey, (target, arguments) => ((Info)target).Name },
                    { InfoOptionKey, (target, arguments) => ((Info)target).Option },
                    { OptionBootupKey, (target, arguments) => ((Option)target).Bootup },
                    { InfoRawKey, (target, arguments) => new Target() },
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

        private static IDictionary<string, IList<ToolCall>> Calls()
        {
            return Singles(new Dictionary<string, ToolCall>(StringComparer.Ordinal)
            {
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
        private sealed class Target
        {
            public string Saved { get; set; }

            public bool Flag { get; set; }

            public int Count { get; set; }
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

        /// <summary>受付を止めた稼働世代のように、委譲された処理を実行しない。</summary>
        private sealed class RefusingInvoker : IUiInvoker
        {
            public bool TryInvokeOnUi(Action action)
            {
                return false;
            }
        }
    }
}
