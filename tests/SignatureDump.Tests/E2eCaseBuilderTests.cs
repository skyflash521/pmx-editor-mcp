using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 実機のエディタへ投げる検査の組み立て。母集団はスキーマ正本のツールで、行から名前を導く
    /// ツールだけが行キーと編集の流れと接続の経路を持つ。
    /// </summary>
    public sealed class E2eCaseBuilderTests
    {
        private const string RowKey = "Sdk.Type.Wipe()";

        [Fact]
        public void AToolThatTakesHandlesIsCheckedWithAHandleTheLedgerDoesNotCarry()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("session_release_handle", Handles()))));

            Assert.Equal("session_release_handle", one.Tool);
            Assert.Equal(E2eExpectation.Refusal, one.Expectation);
            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles" }, one.Arguments.Keys.ToArray());
        }

        [Fact]
        public void TheHandleCheckAlsoCarriesTheOtherGroupsTheToolMustHave()
        {
            E2eCase one = Assert.Single(Refused(Build(Valued("model_update_items"))));

            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles", "value" }, one.Arguments.Keys.OrderBy(k => k).ToArray());
            Assert.Empty((IDictionary<string, object>)one.Arguments["value"]);
        }

        [Fact]
        public void AGroupThatCannotBeFilledLeavesNoHandleCheck()
        {
            Assert.Empty(Refused(Build(Shaped("model_paint_items"))));
        }

        [Fact]
        public void AGroupWithASampleValueLeavesTheHandleCheck()
        {
            ToolSchema schema = Shaped("model_paint_items");
            SchemaItem color = schema.Branches[0].Inputs.Single(i => i.Name == "color");
            E2eCase one = Assert.Single(Refused(E2eCaseBuilder.Build(
                Map(RowKey),
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string> { { color, "Sdk.Paint" } },
                new SampleValueTable(new[] { new SampleValueRow("Sdk.Paint", "赤", "青") }))));

            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal("赤", one.Arguments["color"]);
        }

        [Fact]
        public void AToolThatTakesACountIsCheckedAtTheEdgeOfThePage()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("model_list_bone", Limit()))));

            Assert.Equal("TOOL_INVALID_ARGUMENT", one.Code);
            Assert.Equal(0d, Convert.ToDouble(one.Arguments["limit"]));
        }

        [Fact]
        public void ADangerousRowIsCheckedWithoutTheConfirmation()
        {
            IList<E2eCase> cases = Build(
                Tool("model_wipe", new SchemaItem[0]),
                rowKey: RowKey,
                dangerous: true);

            E2eCase one = Assert.Single(Refused(cases));
            Assert.Equal("TOOL_CONFIRM_REQUIRED", one.Code);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void TheOtherChecksOfADangerousRowCarryTheConfirmation()
        {
            IList<E2eCase> cases = Build(
                Tool("model_wipe", Handles()), rowKey: RowKey, dangerous: true);

            E2eCase handles = Assert.Single(cases, c => c.Code == "TOOL_INVALID_HANDLE");
            Assert.Equal(true, handles.Arguments["confirm"]);
        }

        [Fact]
        public void AToolNamedByARowCarriesTheRowAndTheFlowAndThePath()
        {
            E2eCase one = Assert.Single(
                Refused(Build(Tool("model_release", Handles()), rowKey: RowKey)));

            Assert.Equal(RowKey, one.RowKey);
            Assert.Equal("read", one.EditKind);
            Assert.Equal("Host.Connector.Pmx", one.ConnectionPath);
        }

        [Fact]
        public void AToolThatNoRowNamesIsStillChecked()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("session_release_handle", Handles()))));

            Assert.Equal(string.Empty, one.RowKey);
            Assert.Equal(string.Empty, one.EditKind);
            Assert.Equal(string.Empty, one.ConnectionPath);
        }

        [Fact]
        public void AToolWithoutAnythingToAbuseGivesNoRefusalCase()
        {
            Assert.Empty(Refused(Build(Tool("model_get_name", new SchemaItem[0]))));
        }

        [Fact]
        public void AReadRowIsAlsoCalledForReal()
        {
            E2eCase one = Assert.Single(
                Build(Tool("model_get_name", new SchemaItem[0]), rowKey: RowKey),
                c => c.Expectation == E2eExpectation.Called);

            Assert.Equal("model_get_name", one.Tool);
            Assert.Equal(RowKey, one.RowKey);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void AToolWithNoRowOfItsOwnIsNotCalledWhenItAlreadyReadsWithoutChoosing()
        {
            IList<E2eCase> cases = Build(
                Tool("model_get_name", new SchemaItem[0]),
                readers: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "model_set_name", "model_get_name" },
                });

            Assert.Contains(
                cases,
                c => c.Tool == "model_get_name" && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Called);
        }

        /// <summary>
        /// 呼び先まで届かせられないツールは、断られなかったことだけを見る検査も持たない——その
        /// 検査は行の振る舞いへ一度も入らないので、通っても分かることが無い。
        /// </summary>
        [Fact]
        public void AToolWithNothingToCallBehindItGetsNoCaseThatOnlyLooksForDispatch()
        {
            Assert.DoesNotContain(
                Build(Tool("model_wipe_bones", Handles())),
                c => c.Tool == "model_wipe_bones" && c.Code == null);
        }

        [Fact]
        public void AToolWithNoRowOfItsOwnIsCalledWhenItNeedsNoArguments()
        {
            E2eCase one = Assert.Single(
                Build(Tool("model_get_name", new SchemaItem[0])),
                c => c.Expectation == E2eExpectation.Called);

            Assert.Equal("model_get_name", one.Tool);
            Assert.Equal(string.Empty, one.RowKey);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void AToolThatReturnsAViewImageIsCheckedAgainstTheCapturedView()
        {
            const string Named = "view_get_client_image";
            E2eCase one = Assert.Single(
                E2eCaseBuilder.Build(
                    Map(RowKey),
                    new ToolSchemaTable(new[] { Tool(Named, new SchemaItem[0]) }),
                    new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Named } },
                    Paths(),
                    new HashSet<string>(StringComparer.Ordinal),
                    Shapes(),
                    null,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { { Named, "pmx" } }),
                c => c.Expectation == E2eExpectation.ViewImage);

            Assert.Equal(Named, one.Tool);
            Assert.Equal("pmx", one.View);
            Assert.Equal(RowKey, one.RowKey);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void AToolThatReturnsNoViewImageIsNotCheckedAgainstAnyView()
        {
            Assert.DoesNotContain(
                Build(Tool("model_get_name", new SchemaItem[0])),
                c => c.Expectation == E2eExpectation.ViewImage);
        }

        [Fact]
        public void APositionedMemberTakesNoRelationAndThenTheHeadOfTheList()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");
            IList<E2eCase> written = Positions(writing, reading, reading.Tool)
                .Where(c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success)
                .ToList();

            Assert.Equal(2, written.Count);
            Assert.Equal(true, written[0].Arguments["all"]);
            Assert.Null(((IDictionary<string, object>)written[0].Arguments["value"])["parent"]);
            Assert.Equal(0, ((IDictionary<string, object>)written[1].Arguments["value"])["parent"]);
        }

        [Fact]
        public void TheWrittenPositionIsReadBackFromTheToolThatListsTheSameType()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");

            E2eCase read = Assert.Single(
                Positions(writing, reading, reading.Tool),
                c => c.Expectation == E2eExpectation.Reads);
            Assert.Equal(reading.Tool, read.Tool);
            Assert.Equal(true, read.Arguments["all"]);
            Assert.Equal("parent", read.Expected.Member);
            Assert.Equal(0, read.Expected.Value);
        }

        [Fact]
        public void APositionedMemberIsAlsoCheckedWithAPositionNoListCarries()
        {
            ToolSchema writing = Positioning("model_update_bones");
            E2eCase one = Assert.Single(
                Positions(writing, Listed("model_list_bones"), null),
                c => c.Expectation == E2eExpectation.Refusal);

            Assert.Equal("TOOL_INDEX_OUT_OF_RANGE", one.Code);
            Assert.Equal(
                int.MaxValue,
                ((IDictionary<string, object>)one.Arguments["value"])["parent"]);
        }

        [Fact]
        public void AMemberTheModelDoesNotKeepIsWrittenWithoutReadingItBack()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");
            IList<E2eCase> cases = Positions(
                writing,
                reading,
                reading.Tool,
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    { writing.Tool, new HashSet<string>(new[] { "parent" }, StringComparer.Ordinal) },
                });

            Assert.Contains(
                cases, c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Reads);
        }

        [Fact]
        public void AWriteWithNoReaderIsStillCheckedWithoutReadingItBack()
        {
            ToolSchema writing = Positioning("model_update_bones");
            IList<E2eCase> cases = Positions(writing, Listed("model_list_bones"), null);

            Assert.Contains(
                cases, c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Reads);
        }

        [Fact]
        public void AHandlePostconditionDrawsTheCreatedHandleWithItsObserver()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true));

            E2eCase call = Assert.Single(
                cases, c => c.Tool == "model_make_thing" && c.Expectation == E2eExpectation.Called);
            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);

            Assert.NotNull(call.Produces);
            Assert.Equal(new[] { "handles/0" }, drawn.Borrowed.Keys.ToArray());
            Assert.Equal(call.Produces, drawn.Borrowed["handles/0"]);
            Assert.Equal(new object[] { null }, (object[])drawn.Arguments["handles"]);
            Assert.True(cases.IndexOf(call) < cases.IndexOf(drawn));
        }

        [Fact]
        public void AToolThatAnswersInAListLendsTheFirstOfTheAnswer()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Listing("model_make_things"),
                Observing("model_list_things", "handles", listed: true));

            E2eCase call = Assert.Single(
                cases,
                c => c.Tool == "model_make_things" && c.Expectation == E2eExpectation.Called);
            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            E2eCase released = Assert.Single(
                cases,
                c => c.Tool == "session_release_handle"
                    && c.Expectation == E2eExpectation.Success);

            Assert.Equal(RowKey, call.Produces);
            Assert.Equal(RowKey + "/0", drawn.Borrowed["handles/0"]);
            Assert.Equal(RowKey + "/0", released.Borrowed["handles/0"]);
        }

        [Fact]
        public void AReadbackPostconditionReadsTheSameListBeforeAndAfterTheCall()
        {
            IList<E2eCase> cases = Comparing(
                Readback("model_list_things"),
                Tool("model_wipe_things", new SchemaItem[0]),
                Whole("model_list_things"));

            E2eCase call = Assert.Single(
                cases, c => c.Tool == "model_wipe_things" && c.Expectation == E2eExpectation.Called);
            E2eCase before = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            E2eCase after = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Changed);

            Assert.NotNull(before.Produces);
            Assert.Equal(before.Produces, after.Differs);
            Assert.Equal(before.Arguments, after.Arguments);
            Assert.Equal(true, before.Arguments[E2eCaseBuilder.AllName]);
            Assert.True(cases.IndexOf(before) < cases.IndexOf(call));
            Assert.True(cases.IndexOf(call) < cases.IndexOf(after));
        }

        [Fact]
        public void TheReadbackStartsFromTheStateThatThePostconditionAsksFor()
        {
            IList<E2eCase> cases = Comparing(
                Readback(
                    "model_list_things",
                    SetupOperation.InitPmx(),
                    SetupOperation.AddElement("thing", null),
                    SetupOperation.CallTool(
                        "session_undo",
                        new Dictionary<string, object>(StringComparer.Ordinal),
                        null)),
                Tool("model_wipe_things", new SchemaItem[0]),
                Whole("model_list_things"));

            E2eCase before = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            string[] leading = cases
                .Take(cases.IndexOf(before))
                .Where(c => c.RowKey == RowKey)
                .Select(c => c.Tool)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "session_initialize_pmx", "model_thing", "model_add_things", "session_undo",
                },
                leading);
            E2eCase made = Assert.Single(cases, c => c.Tool == "model_thing");
            E2eCase added = Assert.Single(cases, c => c.Tool == "model_add_things");
            Assert.Equal(made.Produces, added.Borrowed["handles/0"]);
        }

        [Fact]
        public void ARowThatIsRefusedGetsNoReadback()
        {
            IList<E2eCase> cases = Comparing(
                Readback("model_list_things"),
                Tool("model_wipe_things", new SchemaItem[0]),
                Whole("model_list_things"),
                refused: E2eCaseBuilder.InvalidArgument);

            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Changed);
            Assert.DoesNotContain(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
        }

        [Fact]
        public void AnObserverThatCannotReadTheWholeListIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Comparing(
                Readback("model_list_things"),
                Tool("model_wipe_things", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true)));
        }

        /// <summary>
        /// ほかの行の段取りとしてしか呼ばれないツールは、自分の行を名指しした検査を持たないが、
        /// 共通の入口の断りは代表を取ったツールだけが残す。
        /// </summary>
        [Fact]
        public void ARowCalledOnlyAsAnotherRowsSetupKeepsNoRefusalOfItsOwn()
        {
            ToolSchema wipe = Tool("model_bend_bones", new SchemaItem[0]);
            ToolSchema aim = Tool("model_aim_bones", Handles());
            ToolSchema touch = Tool("model_touch_bones", Handles());
            ToolSchema listing = Whole("model_list_things");
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "曲げる。",
                        new[]
                        {
                            Readback(
                                listing.Tool,
                                SetupOperation.CallTool(
                                    touch.Tool,
                                    new Dictionary<string, object>(StringComparer.Ordinal),
                                    null)),
                        },
                        null,
                        null),
                    new ToolMapRow(
                        "Sdk.Type.Aim()", ToolMapEditKind.DirectChange, null, "狙う。", null, null,
                        null),
                    new ToolMapRow(
                        "Sdk.Type.Touch()", ToolMapEditKind.DirectChange, null, "触る。", null,
                        null, null),
                }),
                new ToolSchemaTable(new[] { wipe, aim, touch, listing }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, wipe.Tool },
                    { "Sdk.Type.Aim()", aim.Tool },
                    { "Sdk.Type.Touch()", touch.Tool },
                },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes());

            Assert.Contains(
                cases,
                c => c.Tool == touch.Tool && c.RowKey == RowKey
                    && c.Expectation == E2eExpectation.Success);
            Assert.Contains(cases, c => c.Tool == aim.Tool && c.Code == "TOOL_INVALID_HANDLE");
            Assert.DoesNotContain(
                cases, c => c.Tool == touch.Tool && c.Code == "TOOL_INVALID_HANDLE");
        }

        [Fact]
        public void AToolWithNoRowOfItsOwnKeepsNoShrunkRefusalOnceItIsReached()
        {
            ToolSchema wipe = Tool("model_bend_bones", new SchemaItem[0]);
            ToolSchema aim = Tool("model_aim_bones", Handles());
            ToolSchema loose = Tool("model_touch_bones", Handles());
            ToolSchema listing = Whole("model_list_things");
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "曲げる。",
                        new[]
                        {
                            Readback(
                                listing.Tool,
                                SetupOperation.CallTool(
                                    loose.Tool,
                                    new Dictionary<string, object>(StringComparer.Ordinal),
                                    null)),
                        },
                        null,
                        null),
                    new ToolMapRow(
                        "Sdk.Type.Aim()", ToolMapEditKind.Read, null, "狙う。", null, null, null),
                }),
                new ToolSchemaTable(new[] { wipe, aim, loose, listing }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, wipe.Tool },
                    { "Sdk.Type.Aim()", aim.Tool },
                },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes());

            Assert.Contains(cases, c => c.Tool == aim.Tool && c.Code == "TOOL_INVALID_HANDLE");
            Assert.DoesNotContain(
                cases, c => c.Tool == loose.Tool && c.Code == "TOOL_INVALID_HANDLE");
        }

        [Fact]
        public void TheObservationCarriesTheRowThatAskedForIt()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true));

            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            E2eCase released = Assert.Single(
                cases,
                c => c.Tool == "session_release_handle"
                    && c.Expectation == E2eExpectation.Success);

            foreach (E2eCase one in new[] { drawn, released })
            {
                Assert.Equal(RowKey, one.RowKey);
                Assert.Equal("directChange", one.EditKind);
                Assert.Equal("Host.Connector.Pmx", one.ConnectionPath);
            }
        }

        [Fact]
        public void TheDrawnHandleIsReleasedAfterTheObservation()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true));

            E2eCase call = Assert.Single(
                cases, c => c.Tool == "model_make_thing" && c.Expectation == E2eExpectation.Called);
            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            E2eCase released = Assert.Single(
                cases,
                c => c.Tool == "session_release_handle"
                    && c.Expectation == E2eExpectation.Success);

            Assert.Equal(new[] { "handles/0" }, released.Borrowed.Keys.ToArray());
            Assert.Equal(call.Produces, released.Borrowed["handles/0"]);
            Assert.Equal(new object[] { null }, (object[])released.Arguments["handles"]);
            Assert.True(cases.IndexOf(drawn) < cases.IndexOf(released));
        }

        [Fact]
        public void AnObserverThatTakesTheHandleAloneGetsItWithoutAList()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_headers", "pmxHandle"),
                Tool("model_make_thing", new SchemaItem[0]),
                Taking("model_list_headers", "pmxHandle"));

            E2eCase call = Assert.Single(
                cases, c => c.Tool == "model_make_thing" && c.Expectation == E2eExpectation.Called);
            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_headers" && c.Expectation == E2eExpectation.Success);

            Assert.Equal(new[] { "pmxHandle" }, drawn.Borrowed.Keys.ToArray());
            Assert.Equal(call.Produces, drawn.Borrowed["pmxHandle"]);
            Assert.Null(drawn.Arguments["pmxHandle"]);
        }

        [Fact]
        public void ARowWhoseCallNeedsAConfirmationGetsNoObservation()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true),
                dangerous: true);

            Assert.DoesNotContain(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Produces != null);
        }

        [Fact]
        public void ARowWhoseArgumentsAreUndecidedGetsNoObservation()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", Handles()),
                Observing("model_list_things", "handles", listed: true));

            Assert.DoesNotContain(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Produces != null);
        }

        [Fact]
        public void ARowWhoseCallIsExpectedToBeRefusedGetsNoObservation()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true),
                refused: "TOOL_OPERATION_FAILED");

            Assert.Contains(
                cases,
                c => c.Tool == "model_make_thing" && c.Expectation == E2eExpectation.Denied);
            Assert.DoesNotContain(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Produces != null);
        }

        [Fact]
        public void TheObserverAsksOnlyForTheNameWhenTheListCarriesOne()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Naming("model_list_things", "handles", named: true));

            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);

            Assert.Equal(new object[] { "name" }, (object[])drawn.Arguments["fields"]);
        }

        [Fact]
        public void TheObserverAsksForNoFieldWhenTheListCarriesNoName()
        {
            IList<E2eCase> cases = Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Naming("model_list_things", "handles", named: false));

            E2eCase drawn = Assert.Single(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);

            Assert.DoesNotContain("fields", drawn.Arguments.Keys);
        }

        [Fact]
        public void AHandlePostconditionWithoutAnObserverStopsTheBuild()
        {
            Assert.Throws<InvalidOperationException>(() => Observed(
                new Postcondition(
                    EffectType.HandleCreated,
                    string.Empty,
                    EffectCheckKind.Handle,
                    null,
                    null,
                    null,
                    EffectComparison.AnyChanged,
                    null,
                    false,
                    null),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true)));
        }

        [Fact]
        public void APostconditionWithoutAnObserverAddsNoObservation()
        {
            IList<E2eCase> cases = Observed(
                new Postcondition(
                    EffectType.HandleCreated,
                    string.Empty,
                    EffectCheckKind.CallLogOnly,
                    null,
                    null,
                    null,
                    EffectComparison.Exists,
                    null,
                    false,
                    null),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "handles", listed: true));

            Assert.DoesNotContain(
                cases,
                c => c.Tool == "model_list_things" && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Produces != null);
        }

        [Fact]
        public void AnObserverThatCannotTakeTheHandleStopsTheBuild()
        {
            Assert.Throws<InvalidOperationException>(() => Observed(
                Handle("model_list_things", "handles"),
                Tool("model_make_thing", new SchemaItem[0]),
                Observing("model_list_things", "limit", listed: false)));
        }

        [Fact]
        public void TheSetupThatLendsAnElementFillsTheSlotItLends()
        {
            IList<E2eCase> cases = Lending(Tool("model_add_things", Handles()));

            E2eCase made = Assert.Single(cases, c => c.Produces == "model_add_things");
            E2eCase added = Assert.Single(
                cases, c => c.Tool == "model_add_things" && c.Borrowed != null);

            Assert.Equal("model_thing", made.Tool);
            Assert.Equal(new[] { "handles/0" }, added.Borrowed.Keys.ToArray());
            Assert.Equal("model_add_things", added.Borrowed["handles/0"]);
            Assert.Equal(new object[] { null }, (object[])added.Arguments["handles"]);
        }

        [Fact]
        public void TheSetupThatLendsIntoAParentFillsTheSlotItLends()
        {
            IList<E2eCase> cases = Lending(Assigning("model_add_things"));

            E2eCase added = Assert.Single(
                cases, c => c.Tool == "model_add_things" && c.Borrowed != null);

            Assert.Equal(
                new[] { "assignments/0/handles/0" }, added.Borrowed.Keys.ToArray());
            Assert.Equal("model_add_things", added.Borrowed["assignments/0/handles/0"]);

            IDictionary<string, object> pair = (IDictionary<string, object>)
                ((object[])added.Arguments["assignments"])[0];
            Assert.Equal(0, pair["parentIndex"]);
            Assert.Equal(new object[] { null }, (object[])pair["handles"]);
        }

        [Fact]
        public void AConfirmedRowWithGivenValuesIsCalledWithTheConfirmation()
        {
            IList<E2eCase> cases = Writing("path", given: true, confirmed: true);

            E2eCase call = Assert.Single(
                cases,
                c => c.Tool == "session_save_thing" && c.Expectation == E2eExpectation.Called);

            Assert.Equal(true, call.Arguments["confirm"]);
            Assert.Equal("書き出す位置", call.Arguments["path"]);
        }

        [Fact]
        public void AFilePostconditionMakesTheCallPointAtThePathItWrote()
        {
            IList<E2eCase> cases = Writing("path", given: true, confirmed: true);

            E2eCase call = Assert.Single(
                cases,
                c => c.Tool == "session_save_thing" && c.Expectation == E2eExpectation.Called);

            Assert.Equal("path", call.Writes);
        }

        [Fact]
        public void ACallWhosePostconditionIsCheckedComesBeforeTheOnesThatMoveTheState()
        {
            ToolSchema moving = Tool("model_move_thing", new SchemaItem[0]);
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "ファイルへ書く。",
                        new[]
                        {
                            new Postcondition(
                                EffectType.FileWritten,
                                "path",
                                EffectCheckKind.File,
                                null,
                                null,
                                null,
                                EffectComparison.Exists,
                                null,
                                false,
                                null),
                        },
                        null,
                        null),
                    new ToolMapRow(
                        "Sdk.Type.Move()",
                        ToolMapEditKind.DirectChange,
                        null,
                        "状態を動かす。",
                        null,
                        null,
                        null),
                }),
                new ToolSchemaTable(new[] { Saving(), moving }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, "session_save_thing" },
                    { "Sdk.Type.Move()", moving.Tool },
                },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                new SampleValueTable(
                    new SampleValueRow[0],
                    new[]
                    {
                        new SampleCallRow(
                            RowKey,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "path", "書き出す位置" },
                            },
                            "書ける位置を渡す。"),
                    }));

            E2eCase wrote = Assert.Single(cases, c => c.Writes != null);
            E2eCase moved = Assert.Single(
                cases, c => c.Tool == moving.Tool && c.Expectation == E2eExpectation.Called);

            Assert.True(cases.IndexOf(wrote) < cases.IndexOf(moved));
        }

        [Fact]
        public void ARowWhoseCallIsExpectedToBeRefusedPointsAtNoPath()
        {
            IList<E2eCase> cases = Writing(
                "path", given: true, confirmed: true, refused: "TOOL_OPERATION_FAILED");

            Assert.Contains(
                cases,
                c => c.Tool == "session_save_thing" && c.Expectation == E2eExpectation.Denied);
            Assert.DoesNotContain(cases, c => c.Writes != null);
        }

        [Fact]
        public void ARowThatDeclaresNoFileWrittenPointsAtNoPath()
        {
            IList<E2eCase> cases = Writing(
                "path", given: true, confirmed: true, writesFile: false);

            Assert.Contains(
                cases,
                c => c.Tool == "session_save_thing" && c.Expectation == E2eExpectation.Called);
            Assert.DoesNotContain(cases, c => c.Writes != null);
        }

        [Fact]
        public void AConfirmedRowWithoutGivenValuesIsNotCalled()
        {
            IList<E2eCase> cases = Writing("path", given: false, confirmed: true);

            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Called);
            Assert.DoesNotContain(cases, c => c.Writes != null);
        }

        [Fact]
        public void ARowWhoseCallIsNotBuiltPointsAtNoPath()
        {
            IList<E2eCase> cases = Writing("path", given: false, confirmed: false);

            Assert.DoesNotContain(cases, c => c.Writes != null);
        }

        [Fact]
        public void AFilePostconditionWhoseKeyIsNoArgumentStopsTheBuild()
        {
            Assert.Throws<InvalidOperationException>(
                () => Writing("elsewhere", given: false, confirmed: false));
        }

        [Fact]
        public void TheLedgerRefusalIsCheckedOnceAcrossHandledTypes()
        {
            IList<E2eCase> cases = Reached(
                "model_release_bones", "model_release_bodies", typed: true);

            E2eCase one = Assert.Single(cases, c => c.Code == "TOOL_INVALID_HANDLE");
            Assert.Equal("model_release_bodies", one.Tool);
        }

        /// <summary>
        /// どちらも呼び先まで届く検査を持たない2つのツールでも、共通の入口の断りは種別ごとの
        /// 代表1件だけを残す。
        /// </summary>
        [Fact]
        public void ARowWithNoOtherCheckIsLeftWithTheRepresentativeRefusalOnly()
        {
            ToolSchema first = Tool("model_release_bones", Handles());
            ToolSchema second = Tool("model_release_bodies", Handles());
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(RowKey, ToolMapEditKind.Read, null, "手放す。", null, null, null),
                    new ToolMapRow(
                        "Sdk.Type.Free()", ToolMapEditKind.Read, null, "手放す。", null, null, null),
                }),
                new ToolSchemaTable(new[] { first, second }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, first.Tool },
                    { "Sdk.Type.Free()", second.Tool },
                },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes());

            E2eCase one = Assert.Single(cases, c => c.Code == "TOOL_INVALID_HANDLE");
            Assert.Equal("model_release_bodies", one.Tool);
        }

        [Fact]
        public void TheCountRefusalIsCheckedOnce()
        {
            IList<E2eCase> cases = Reached("model_list_bones", "model_list_bodies");

            E2eCase one = Assert.Single(cases, c => c.Code == "TOOL_INVALID_ARGUMENT");
            Assert.Equal("model_list_bodies", one.Tool);
        }

        [Fact]
        public void TheConfirmationRefusalIsCheckedOnce()
        {
            ToolSchema wipe = Tool("model_wipe", Handles());
            ToolSchema erase = Tool("model_erase", Handles());
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(RowKey, ToolMapEditKind.Read, null, "消す。", null, null, null),
                    new ToolMapRow(
                        "Sdk.Type.Erase()", ToolMapEditKind.Read, null, "消す。", null, null, null),
                }),
                new ToolSchemaTable(new[] { wipe, erase }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, wipe.Tool },
                    { "Sdk.Type.Erase()", erase.Tool },
                },
                Paths(),
                new HashSet<string>(
                    new[] { RowKey, "Sdk.Type.Erase()" }, StringComparer.Ordinal),
                Shapes());

            E2eCase one = Assert.Single(cases, c => c.Code == "TOOL_CONFIRM_REQUIRED");
            Assert.Equal("model_erase", one.Tool);

            // 2つの種類の代表を兼ねる行は、その2件を残す。
            Assert.Equal(2, cases.Count(c => c.Tool == "model_erase" && c.Code != null));

            // 代表を取られた行は、どちらの種別の断りも残さない。
            Assert.DoesNotContain(cases, c => c.Tool == "model_wipe" && c.Code != null);
        }

        [Fact]
        public void ARowThatIsReachedOtherwiseKeepsNoShrunkRefusal()
        {
            ToolSchema plain = Tool("model_bend_bones", Handles());
            ToolSchema reached = Tool("model_touch_bones", Handles());
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey, ToolMapEditKind.DirectChange, null, "曲げる。", null, null, null),
                    new ToolMapRow(
                        "Sdk.Type.Touch()",
                        ToolMapEditKind.DirectChange,
                        null,
                        "触る。",
                        null,
                        null,
                        null),
                }),
                new ToolSchemaTable(new[] { plain, reached }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, plain.Tool },
                    { "Sdk.Type.Touch()", reached.Tool },
                },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                new SampleValueTable(
                    new SampleValueRow[0],
                    new[]
                    {
                        new SampleCallRow(
                            "Sdk.Type.Touch()",
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "handles", new object[] { 0 } },
                            },
                            "使えるハンドルを渡す。"),
                    }));

            Assert.Contains(
                cases,
                c => c.Tool == reached.Tool && c.Expectation == E2eExpectation.Called);
            Assert.DoesNotContain(
                cases,
                c => c.Tool == reached.Tool && c.Code == "TOOL_INVALID_HANDLE");
            Assert.Contains(cases, c => c.Tool == plain.Tool && c.Code == "TOOL_INVALID_HANDLE");
        }

        [Fact]
        public void TheLoneHandleRefusalIsCheckedApartFromTheListedOne()
        {
            ToolSchema bind = Taking("model_bind_bone", "bone", required: true);
            ToolSchema aim = Taking("model_aim_bone", "bone", required: true);
            ToolSchema release = Tool("model_release_bones", Handles());
            IList<E2eCase> cases = E2eCaseBuilder.Build(
                Reaching(new[] { bind, aim, release }),
                new ToolSchemaTable(new[] { bind, aim, release }),
                Named(new[] { bind, aim, release }),
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string>
                {
                    { Only(bind), "Sdk.Bone" },
                    { Only(aim), "Sdk.Bone" },
                },
                Values(new[] { bind, aim, release }),
                null,
                null,
                null,
                null,
                null,
                new HashSet<string>(new[] { "Sdk.Bone" }, StringComparer.Ordinal));

            Assert.Equal(
                new[] { "model_aim_bone", "model_release_bones" },
                cases.Where(c => c.Code == "TOOL_INVALID_HANDLE")
                    .Select(c => c.Tool)
                    .OrderBy(t => t, StringComparer.Ordinal)
                    .ToArray());
        }

        [Fact]
        public void ARowThatNeedsAHeldTargetIsGivenOneThatIsMadeFirst()
        {
            ToolSchema maker = Tool("model_make_bone", new SchemaItem[0]);
            ToolSchema taker = Tool("model_bend_bones", Handles());
            IList<E2eCase> cases = Making(maker, taker, "Sdk.Bone");

            E2eCase made = Assert.Single(
                cases, c => c.Tool == maker.Tool && c.Expectation == E2eExpectation.Success);
            E2eCase called = Assert.Single(
                cases, c => c.Tool == taker.Tool && c.Expectation == E2eExpectation.Called);

            Assert.NotNull(made.Produces);
            Assert.Equal(made.Produces, called.Borrowed["handles/0"]);
            Assert.Equal(new object[] { null }, (object[])called.Arguments["handles"]);
            Assert.True(cases.IndexOf(made) < cases.IndexOf(called));
        }

        /// <summary>受け手を作るツールが無い行は呼べないままで、ほかの検査も持たない。</summary>
        [Fact]
        public void ARowWhoseHeldTypeHasNoMakerIsNotCalledAtAll()
        {
            ToolSchema taker = Tool("model_bend_bones", Handles());
            IList<E2eCase> cases = Making(null, taker, "Sdk.Bone");

            Assert.DoesNotContain(
                cases, c => c.Tool == taker.Tool && c.Expectation == E2eExpectation.Called);
            Assert.DoesNotContain(cases, c => c.Tool == taker.Tool && c.Code == null);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            ToolSchemaTable schemas = new ToolSchemaTable(new ToolSchema[0]);
            Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> dangerous = new HashSet<string>(StringComparer.Ordinal);

            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(null, schemas, named, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), null, named, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, null, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, null, dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, Paths(), null, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, Paths(), dangerous, null));
        }

        /// <summary>断りを見る検査だけ。呼び先が在ることの検査はどのツールにも付くので外す。</summary>
        private static IList<E2eCase> Refused(IEnumerable<E2eCase> cases)
        {
            return cases.Where(c => c.Code != null).ToList();
        }

        private static IList<E2eCase> Build(
            ToolSchema schema,
            string rowKey = null,
            bool dangerous = false,
            IDictionary<string, string> readers = null)
        {
            Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
            if (rowKey != null)
            {
                named[rowKey] = schema.Tool;
            }

            return E2eCaseBuilder.Build(
                Map(rowKey ?? RowKey),
                new ToolSchemaTable(new[] { schema }),
                named,
                Paths(),
                dangerous
                    ? new HashSet<string>(new[] { rowKey }, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                null,
                null,
                null,
                null,
                readers);
        }

        /// <summary>SDKに由来する項目の綴り。題材では引く先を持たない。</summary>
        private static IDictionary<SchemaItem, string> Shapes()
        {
            return new Dictionary<SchemaItem, string>();
        }

        private static ToolMap Map(string rowKey)
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(rowKey, ToolMapEditKind.Read, null, "読むだけ。", null, null, null),
            });
        }

        private static IDictionary<string, string> Paths()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Sdk.Type", "Host.Connector.Pmx" },
            };
        }

        private static ToolSchema Tool(string name, IList<SchemaItem> inputs)
        {
            return new ToolSchema(
                name,
                new[] { new SchemaBranch("only", null, null, inputs, new SchemaChoice[0]) },
                Output(),
                null);
        }

        /// <summary>応答を並びで返すツール。出たハンドルはその並びの先頭に入る。</summary>
        private static ToolSchema Listing(string name)
        {
            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch("only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, null, Output(), null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null);
        }

        /// <summary>対象をハンドルで指し、値の組も要るツール。</summary>
        private static ToolSchema Valued(string name)
        {
            SchemaItem value = new SchemaItem(
                null, new SchemaItem[0], null, "value", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        Handles().Concat(new[] { value }).ToList(),
                        new[] { new SchemaChoice(new[] { "value", "values" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>対象をハンドルで指し、綴りから値を決められない項目が要るツール。</summary>
        private static ToolSchema Shaped(string name)
        {
            SchemaItem color = new SchemaItem(
                "color", null, null, "color", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        Handles().Concat(new[] { color }).ToList(),
                        new[] { new SchemaChoice(new[] { "color", "brush" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>
        /// 何を相手にするかを選ばせ、何件返すかも決めて読めるツール。実機の一覧はどれもこの形で、
        /// 全体・位置・範囲のどれかを必ず取る。
        /// </summary>
        private static ToolSchema Whole(string name)
        {
            SchemaItem all = new SchemaItem(
                "boolean", null, null, E2eCaseBuilder.AllName, ItemOrigin.HostInput, null, null,
                false, null, null, null, false, null);
            SchemaItem indices = new SchemaItem(
                null,
                null,
                new SchemaItem(
                    "number", null, null, null, ItemOrigin.HostInput, null, null, false,
                    null, null, null, false, null),
                "indices",
                ItemOrigin.HostInput,
                null, null, false, null, null, null, false, null);
            SchemaItem limit = new SchemaItem(
                "number", null, null, E2eCaseBuilder.LimitName, ItemOrigin.HostInput, null, null,
                false, null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { all, indices, limit },
                        new[] { new SchemaChoice(new[] { E2eCaseBuilder.AllName, "indices" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>呼ぶ前と後で同じ一覧を読み比べる事後条件。</summary>
        private static Postcondition Readback(string observer, params SetupOperation[] setup)
        {
            return new Postcondition(
                EffectType.ObservableChange,
                string.Empty,
                EffectCheckKind.Readback,
                observer,
                null,
                null,
                EffectComparison.AnyChanged,
                null,
                false,
                setup);
        }

        /// <summary>読み比べる行と、その一覧を読むツールの2つで検査を組み立てる。</summary>
        private static IList<E2eCase> Comparing(
            Postcondition postcondition,
            ToolSchema target,
            ToolSchema observer,
            string refused = null)
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "書き換える。",
                        new[] { postcondition },
                        null,
                        null),
                }),
                new ToolSchemaTable(new[] { target, observer }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, target.Tool } },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                refused == null
                    ? null
                    : new SampleValueTable(
                        new SampleValueRow[0],
                        new[]
                        {
                            new SampleCallRow(
                                RowKey,
                                new Dictionary<string, object>(StringComparer.Ordinal),
                                "断られることを見る。",
                                refused,
                                "断る文面"),
                        }),
                adders: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "thing", "model_add_things" },
                },
                factories: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "model_add_things", "model_thing" },
                });
        }

        /// <summary>ハンドルが出たことを、その名前のツールの引数で観測する事後条件。</summary>
        private static Postcondition Handle(string observer, string argument)
        {
            return new Postcondition(
                EffectType.HandleCreated,
                string.Empty,
                EffectCheckKind.Handle,
                observer,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { argument, ReferenceSpace.Result },
                },
                null,
                EffectComparison.Exists,
                null,
                false,
                null);
        }

        /// <summary>事後条件を持つ行と、その観測に使うツールの2つで検査を組み立てる。</summary>
        private static IList<E2eCase> Observed(
            Postcondition postcondition,
            ToolSchema target,
            ToolSchema observer,
            bool dangerous = false,
            string refused = null)
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "書き換える。",
                        new[] { postcondition },
                        null,
                        null),
                }),
                new ToolSchemaTable(new[] { target, observer }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, target.Tool } },
                Paths(),
                dangerous
                    ? new HashSet<string>(new[] { RowKey }, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                refused == null
                    ? null
                    : new SampleValueTable(
                        new SampleValueRow[0],
                        new[]
                        {
                            new SampleCallRow(
                                RowKey,
                                new Dictionary<string, object>(StringComparer.Ordinal),
                                "断られることを見る。",
                                refused,
                                "断る文面"),
                        }));
        }

        /// <summary>
        /// 返す項目を選べて、並べたものが項目の組を載せる観測ツール。載せる項目は、名前か、
        /// 位置で指す項目かを選べる。
        /// </summary>
        private static ToolSchema Naming(string name, string argument, bool named)
        {
            SchemaItem carried = new SchemaItem(
                named ? "text" : "number",
                null,
                null,
                named ? "name" : "parentIndex",
                ItemOrigin.HostOutput,
                null, null, false, null, null, null, false, null);
            SchemaItem items = new SchemaItem(
                null,
                null,
                new SchemaItem(
                    null, new[] { carried }, null, null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                "items",
                ItemOrigin.HostOutput,
                null, null, false, null, null, null, false, null);
            SchemaItem fields = new SchemaItem(
                null, null, Element(), "fields", ItemOrigin.HostInput, false, null, false,
                null, null, null, false, null);
            SchemaItem taken = new SchemaItem(
                null, null, Element(), argument, ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "held", null, null, new[] { taken, fields }, new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, new[] { items }, null, null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null);
        }

        /// <summary>要素を1つ作って、受け取る側のツールへ渡す段取りだけで検査を組み立てる。</summary>
        private static IList<E2eCase> Lending(ToolSchema taker)
        {
            return E2eCaseBuilder.Build(
                Map(RowKey),
                new ToolSchemaTable(new[] { Tool("model_thing", new SchemaItem[0]), taker }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { taker.Tool, "model_thing" },
                });
        }

        /// <summary>
        /// 対象を位置で指す呼び分けを先に置き、その名前の項目はあとの呼び分けだけが受け取るツール。
        /// </summary>
        private static ToolSchema Observing(string name, string argument, bool listed)
        {
            SchemaItem taken = listed
                ? new SchemaItem(
                    null, null, Element(), argument, ItemOrigin.HostInput, true, null, false,
                    null, null, null, false, null)
                : new SchemaItem(
                    "number", null, null, argument, ItemOrigin.HostInput, false, null, false,
                    null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "position", null, null, new[] { Whole() }, new SchemaChoice[0]),
                    new SchemaBranch("held", null, null, new[] { taken }, new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>その名前の項目を1つだけ、呼び分けを分けずに受け取るツール。</summary>
        private static ToolSchema Taking(string name, string argument, bool required = false)
        {
            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[]
                        {
                            new SchemaItem(
                                "number", null, null, argument, ItemOrigin.HostInput, required,
                                null, false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>並びで受け取るハンドルの要素。</summary>
        private static SchemaItem Held(ToolSchema schema)
        {
            return schema.Branches[0].Inputs.Single(i => i.Name == "handles").Element;
        }

        /// <summary>1つだけ受け取る項目。</summary>
        private static SchemaItem Only(ToolSchema schema)
        {
            return schema.Branches[0].Inputs.Single();
        }

        /// <summary>
        /// 受け手をハンドルで要るツールと、その型を作るツールで検査を組み立てる。
        /// <paramref name="maker"/> が null なら、その型を作るツールを持たない場を作る。
        /// </summary>
        private static IList<E2eCase> Making(
            ToolSchema maker, ToolSchema taker, string held, string basis = "触る。")
        {
            IList<ToolSchema> tools = maker == null
                ? new[] { taker }
                : new[] { maker, taker };
            IDictionary<string, string> named =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { RowKey, taker.Tool },
                };
            IList<ToolMapRow> rows = new List<ToolMapRow>
            {
                new ToolMapRow(RowKey, ToolMapEditKind.Read, null, basis, null, null, null),
            };
            if (maker != null)
            {
                named["Sdk.Type.Make()"] = maker.Tool;
                rows.Add(new ToolMapRow(
                    "Sdk.Type.Make()", ToolMapEditKind.Read, null, "作る。", null, null, null));
            }

            return E2eCaseBuilder.Build(
                new ToolMap(rows),
                new ToolSchemaTable(tools),
                named,
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string> { { Held(taker), held } },
                null,
                null,
                null,
                null,
                null,
                null,
                new HashSet<string>(new[] { held }, StringComparer.Ordinal),
                null,
                maker == null
                    ? null
                    : new Dictionary<string, IList<string>>(StringComparer.Ordinal)
                    {
                        { taker.Tool, new[] { maker.Tool } },
                    });
        }

        /// <summary>その並びのツールを、どれも呼び先まで届く行として持つ能力対応表。</summary>
        private static ToolMap Reaching(IList<ToolSchema> tools)
        {
            return new ToolMap(tools
                .Select(t => new ToolMapRow(
                    "Sdk.Type." + t.Tool + "()",
                    ToolMapEditKind.DirectChange,
                    null,
                    "呼ぶ。",
                    null,
                    null,
                    null))
                .ToList());
        }

        /// <summary>その並びのツールを、行キーから名前へ結んだ表。</summary>
        private static IDictionary<string, string> Named(IList<ToolSchema> tools)
        {
            return tools.ToDictionary(
                t => "Sdk.Type." + t.Tool + "()", t => t.Tool, StringComparer.Ordinal);
        }

        /// <summary>その並びのツールを、どれも呼べるようにする渡す値。</summary>
        private static SampleValueTable Values(IList<ToolSchema> tools)
        {
            return new SampleValueTable(
                new SampleValueRow[0],
                tools
                    .Select(t => new SampleCallRow(
                        "Sdk.Type." + t.Tool + "()",
                        t.Branches[0].Inputs.ToDictionary(
                            i => i.Name,
                            i => i.Element == null ? (object)0 : new object[] { 0 },
                            StringComparer.Ordinal),
                        "使える値を渡す。"))
                    .ToList());
        }

        /// <summary>
        /// そのどれもが呼び先まで届く2つのツールで、断りを見る検査を組み立てる。
        /// <paramref name="typed"/> が真なら、2つのハンドルへ別々の型を割り当てる。
        /// </summary>
        private static IList<E2eCase> Reached(string first, string second, bool typed = false)
        {
            ToolSchema one = Tool(first, Handles().Concat(Limit()).ToList());
            ToolSchema other = Tool(second, Handles().Concat(Limit()).ToList());
            IList<ToolSchema> tools = new[] { one, other };

            return E2eCaseBuilder.Build(
                Reaching(tools),
                new ToolSchemaTable(tools),
                Named(tools),
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                typed
                    ? new Dictionary<SchemaItem, string>
                    {
                        { Held(one), "Sdk.Bone" },
                        { Held(other), "Sdk.Body" },
                    }
                    : null,
                Values(tools));
        }

        /// <summary>書き出す先を取るツール。その引数は、実機と同じく並びの先頭ではない。</summary>
        private static ToolSchema Saving()
        {
            SchemaItem holder = new SchemaItem(
                "number", null, null, "pmxHandle", ItemOrigin.HostInput, false, null, false,
                null, null, null, false, null);
            SchemaItem path = new SchemaItem(
                "text", null, null, "path", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                "session_save_thing",
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new[] { holder, path }, new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>
        /// 書き出す先を取るツールを持つ行で検査を組み立てる。その引数は、実機と同じく並びの先頭では
        /// ない。<paramref name="writesFile"/> が偽なら、同じツールでファイルを書くとは宣言しない。
        /// </summary>
        private static IList<E2eCase> Writing(
            string effectKey,
            bool given,
            bool confirmed,
            bool writesFile = true,
            string refused = null)
        {
            const string Tool = "session_save_thing";

            return E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey,
                        ToolMapEditKind.DirectChange,
                        null,
                        "ファイルへ書く。",
                        new[]
                        {
                            writesFile
                                ? new Postcondition(
                                    EffectType.FileWritten,
                                    effectKey,
                                    EffectCheckKind.File,
                                    null,
                                    null,
                                    null,
                                    EffectComparison.Exists,
                                    null,
                                    false,
                                    null)
                                : new Postcondition(
                                    EffectType.StateWritten,
                                    string.Empty,
                                    EffectCheckKind.CallLogOnly,
                                    null,
                                    null,
                                    null,
                                    EffectComparison.Exists,
                                    null,
                                    false,
                                    null),
                        },
                        null,
                        null),
                }),
                new ToolSchemaTable(new[] { Saving() }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Tool } },
                Paths(),
                confirmed
                    ? new HashSet<string>(new[] { RowKey }, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                null,
                given
                    ? new SampleValueTable(
                        new SampleValueRow[0],
                        new[]
                        {
                            new SampleCallRow(
                                RowKey,
                                new Dictionary<string, object>(StringComparer.Ordinal)
                                {
                                    { "path", "書き出す位置" },
                                },
                                "書ける位置を渡す。",
                                refused,
                                refused == null ? null : "断る文面"),
                        })
                    : null);
        }

        /// <summary>親を位置で指し、その親へ入れるハンドルの並びを組で受け取るツール。</summary>
        private static ToolSchema Assigning(string name)
        {
            SchemaItem parentIndex = new SchemaItem(
                "number", null, null, "parentIndex", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
            SchemaItem handles = new SchemaItem(
                null, null, Element(), "handles", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
            SchemaItem pair = new SchemaItem(
                null, new[] { parentIndex, handles }, null, null, ItemOrigin.HostInput, null, null,
                false, null, null, null, false, null);
            SchemaItem assignments = new SchemaItem(
                null, null, pair, "assignments", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new[] { assignments }, new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        private static IList<SchemaItem> Handles()
        {
            return new[]
            {
                new SchemaItem(
                    null, null, Element(), "handles", ItemOrigin.HostInput, true, null, false,
                    null, null, null, false, null),
            };
        }

        private static IList<SchemaItem> Limit()
        {
            return new[]
            {
                new SchemaItem(
                    "number", null, null, "limit", ItemOrigin.HostInput, false, null, false,
                    null, null, null, false, null),
            };
        }

        /// <summary>位置で指す項目を持つツールと、それを読み返すツールの組で検査を組み立てる。</summary>
        private static IList<E2eCase> Positions(
            ToolSchema writing,
            ToolSchema reading,
            string reader,
            IDictionary<string, ISet<string>> unkept = null)
        {
            SchemaItem member = writing.Branches[0].Inputs
                .Single(i => i.Name == "value").Members.Single();

            return E2eCaseBuilder.Build(
                Map(RowKey),
                new ToolSchemaTable(new[] { writing, reading }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, writing.Tool } },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string> { { member, "Sdk.Bone" } },
                null,
                null,
                new HashSet<string>(new[] { "Sdk.Bone" }, StringComparer.Ordinal),
                null,
                reader == null
                    ? null
                    : new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { writing.Tool, reader },
                    },
                unkept);
        }

        /// <summary>全件を指して値の組を書き換えるツール。値の組は位置で指す項目を1つ持つ。</summary>
        private static ToolSchema Positioning(string name)
        {
            SchemaItem parent = new SchemaItem(
                "number", null, null, "parent", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);
            SchemaItem value = new SchemaItem(
                null, new[] { parent }, null, "value", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Whole(), value },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>全件を指して並べるツール。値の組は受け取らない。</summary>
        private static ToolSchema Listed(string name)
        {
            SchemaItem items = new SchemaItem(
                null, null, Element(), "items", ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Whole() },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                },
                new SchemaItem(
                    null, new[] { items }, null, null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null);
        }

        /// <summary>対象を全件にする入力。</summary>
        private static SchemaItem Whole()
        {
            return new SchemaItem(
                "boolean", null, null, "all", ItemOrigin.HostInput, false, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Element()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }
    }
}
