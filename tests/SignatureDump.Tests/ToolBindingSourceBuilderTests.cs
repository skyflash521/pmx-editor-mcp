using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>ツールの名前から呼ぶ行へ結び付ける表。</summary>
    public sealed class ToolBindingSourceBuilderTests
    {
        private const string Form = "PEPlugin.Form.IPEFormConnector";

        private const string Held = "PXCPlugin.UIModel.IPXUIModel";

        private const string CPluginConnector = "PXCPlugin.IPXCPluginConnector";

        private const string GetTool = "session_get_form_connector";

        private const string UpdateTool = "session_update_form_connector";

        private const string Connector = "PEPlugin.Pmx.IPXPmxConnector";

        private const string StateReadKey = Connector + ".GetCurrentState()";

        private const string CommitKey = Connector + ".Update(PEPlugin.Pmx.IPXPmx)";

        private const string StopUndoKey = Connector + ".LockUndo()";

        private const string ResumeUndoKey = Connector + ".UnlockUndo()";

        private const string Bridge = "PXCPlugin.PXCBridge";

        private const string BridgeConnector = "PXCPlugin.IPXCPluginConnector";

        private const string BridgeReadKey = Bridge + ".GetCurrentPmx(" + BridgeConnector + ")";

        private const string BridgeCommitKey =
            Bridge + ".UpdatePmx(" + BridgeConnector + ",PEPlugin.Pmx.IPXPmx,System.Boolean)";

        private const string Pmx = "PEPlugin.Pmx.IPXPmx";

        private const string ListKey = Pmx + ".Vertex()";

        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Leaf = "PEPlugin.Pmx.IPXLeaf";

        private const string Kept = "PEPlugin.Vme.IPEVmeKept";

        private const string KeptLeaf = "PEPlugin.Vme.IPEVmeKeptLeaf";

        private const string KeptListKey = Pmx + ".Kept()";

        private const string Weight = "PEPlugin.Pmx.IPXWeight";

        private const string Info = "Sdk.Info";

        private const string Option = "Sdk.Option";

        private const string WeightKey = Vertex + ".Weight()";

        [Fact]
        public void ACallCarriesTheRowTheReceiverAndTheArgumentTypes()
        {
            ToolBindingSource source = Build(
                Dispatched("session_open_pmx_file", Method("OpenPMXFile", "System.Boolean", "path")));

            Assert.Contains(
                "calls.Add(\"session_open_pmx_file\", new ToolCall[] { new ToolCall(\"" + Form
                    + ".OpenPMXFile(System.String)\", new ToolReceiver(ToolReceiverKind.Connection,"
                    + " \"" + Form + "\", EditKind.DirectChange), ToolAccess.Whole(),"
                    + " DangerKind.None, new ToolArgument[] { new ToolArgument(\"path\","
                    + " typeof(global::System.String)) }, new ToolArgument[] {  },"
                    + " typeof(global::System.Boolean)) });",
                source.Text);
        }

        [Fact]
        public void ACallThatReturnsNothingCarriesNoResultType()
        {
            ToolBindingSource source = Build(
                Dispatched("session_undo", Method("Undo", "System.Void")));

            Assert.Contains(
                "new ToolArgument[] {  }, new ToolArgument[] {  }, null) });", source.Text);
        }

        [Fact]
        public void AStaticMemberTakesNoReceiver()
        {
            SignatureRecord signature = new SignatureRecord(
                "Sdk.Bridge.Ping()",
                "Sdk.Bridge",
                MemberKind.Method,
                "Ping",
                true,
                0,
                new ParameterRecord[0],
                "System.Void",
                false,
                false,
                OperationDirection.Write);

            ToolBindingSource source = Build(Dispatched("model_ping", signature));

            Assert.Contains(
                "new ToolCall(\"Sdk.Bridge.Ping()\","
                    + " new ToolReceiver(ToolReceiverKind.Connection, null, EditKind.DirectChange),"
                    + " ToolAccess.Whole(), DangerKind.None,",
                source.Text);
        }

        [Fact]
        public void ACallOnAHandleTargetTakesItsReceiverFromAHandle()
        {
            ToolBindingSource source = Build(
                Dispatched("view_do_it_held_thing", HeldMethod("DoIt", "System.Void")));

            Assert.Contains(
                "new ToolReceiver(ToolReceiverKind.Handle, \"" + Held + "\","
                    + " EditKind.DirectChange, false, item => item is global::" + Held + ")",
                source.Text);
        }

        [Fact]
        public void ACallOnAConnectorDoesNotTakeItsReceiverFromAHandle()
        {
            ToolBindingSource source = Build(
                Dispatched("session_do_it", Method("DoIt", "System.Void")));

            Assert.DoesNotContain("ToolReceiverKind.Handle", source.Text);
        }

        [Fact]
        public void AnArgumentOfAHandleTargetIsTakenAsAHandle()
        {
            ToolBindingSource source = Build(
                Dispatched("session_take_it", Taking("TakeIt", "System.Void", Held)));

            Assert.Contains(
                "new ToolArgument(\"one\", typeof(global::" + Held + "), false, null, false,"
                    + " typeof(global::" + Held + "))",
                source.Text);
        }

        [Fact]
        public void AnIssuingCallCarriesTheRowThatLetsTheIssuedThingGo()
        {
            ToolBindingSource source = Build(
                Issuing("session_make_it", Method("MakeIt", Held)),
                Released(HeldMethod("Drop", "System.Void")));

            Assert.Contains(
                "typeof(global::" + Held + "), typeof(global::" + Held + "), null, \""
                    + Held + ".Drop()\", false)",
                source.Text);
        }

        [Fact]
        public void ACallThatIssuesThingsInARowCarriesTheElementTypeAndTheMarkOfThat()
        {
            ToolBindingSource source = Build(
                Issuing("session_make_them", Method("MakeThem", Held + "[]")),
                Released(HeldMethod("Drop", "System.Void")));

            Assert.Contains(
                "typeof(global::" + Held + "[]), typeof(global::" + Held + "), null, \""
                    + Held + ".Drop()\", false, true, true)",
                source.Text);
        }

        [Fact]
        public void EveryCallOfAToolThatIssuesThingsInARowAlsoRespondsInARow()
        {
            ToolBindingSource source = Build(
                Issuing("session_make_them", Method("MakeThem", Held + "[]", "one")),
                Issuing("session_make_them", Method("MakeThem", Held)),
                Released(HeldMethod("Drop", "System.Void")));

            Assert.Contains(
                "typeof(global::" + Held + "), typeof(global::" + Held + "), null, \""
                    + Held + ".Drop()\", false, false, true)",
                source.Text);
        }

        [Fact]
        public void ACallThatIssuesAThingWithNoWayToLetItGoCarriesNoSuchRow()
        {
            ToolBindingSource source = Build(
                Issuing("session_make_it", Method("MakeIt", Held)));

            Assert.Contains(
                "typeof(global::" + Held + "), typeof(global::" + Held + "))", source.Text);
        }

        [Fact]
        public void AThingLetGoByItsOwnerCarriesTheRowAndTheMarkOfThatForm()
        {
            ToolBindingSource source = Build(
                Issuing("session_make_it", Method("MakeIt", Held)),
                Released(Taking("LetGo", "System.Void", Held)));

            Assert.Contains(
                "typeof(global::" + Held + "), typeof(global::" + Held + "), null, \""
                    + Form + ".LetGo(" + Held + ")\", true)",
                source.Text);
        }

        [Fact]
        public void AThingWhoseWayToLetItGoCannotBeReachedIsRefused()
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Build(
                    Issuing("session_make_it", HeldMethod("MakeIt", Held)),
                    Released(Taking("LetGo", "System.Void", Held))));

            Assert.Contains("手放す手順を呼べない形", thrown.Message);
        }

        [Fact]
        public void TheConnectorArgumentIsPutInByTheHost()
        {
            ToolBindingSource source = Build(
                Dispatched("session_take_it", Taking("TakeIt", "System.Void", CPluginConnector)));

            Assert.Contains(
                "new ToolArgument(\"one\", typeof(global::" + CPluginConnector + "), true, null, true)",
                source.Text);
        }

        [Theory]
        [InlineData("Close", "System.Void", "DangerKind.Shutdown")]
        [InlineData("InitializePMX", "System.Void", "DangerKind.Reset")]
        [InlineData("SavePMXFile", "System.Void", "DangerKind.Overwrite")]
        public void TheDangerOfTheMemberComesOutWithTheCall(
            string member, string valueType, string danger)
        {
            ToolBindingSource source = Build(
                Dispatched("session_" + member.ToLowerInvariant(), Method(member, valueType)));

            Assert.Contains(danger, source.Text);
        }

        [Fact]
        public void TheDangerousToolsTakeNamesOfTheirOwnAndTheSafeOnesDoNot()
        {
            ToolBindingSource source = Build(
                Dispatched("session_close", Method("Close", "System.Void")),
                Dispatched("session_initialize_pmx", Method("InitializePMX", "System.Void")),
                Dispatched("session_save_pmx_file", Method("SavePMXFile", "System.Void", "path")),
                Dispatched("session_undo", Method("Undo", "System.Void")));

            Assert.Equal(
                new[]
                {
                    "session_close", "session_initialize_pmx", "session_save_pmx_file",
                    "session_undo",
                },
                source.Calls.ToArray());
            Assert.Equal(3, Occurrences(source.Text, "DangerKind.Shutdown")
                + Occurrences(source.Text, "DangerKind.Reset")
                + Occurrences(source.Text, "DangerKind.Overwrite"));
            Assert.Equal(1, Occurrences(source.Text, "DangerKind.None"));
        }

        [Fact]
        public void AReadableItemGoesToTheGettingToolAndAWritableOneAlsoToTheUpdatingTool()
        {
            ToolBindingSource source = Build(
                Embedded(Property("UndoCount", "System.Int32", true, false), GetTool),
                Embedded(
                    Property("PmxFormActivate", "System.Boolean", true, true), GetTool, UpdateTool));

            Assert.Equal(new[] { GetTool, UpdateTool }, source.Aggregations.ToArray());
            Assert.Contains(
                "aggregations.Add(\"" + GetTool + "\", new ToolFields(false, false,"
                    + " new ToolReceiver(ToolReceiverKind.Connection, \"" + Form
                    + "\", EditKind.Read), ToolAccess.Whole(), new ToolFieldSet[]",
                source.Text);
            Assert.Contains(
                "aggregations.Add(\"" + UpdateTool + "\", new ToolFields(true, false,"
                    + " new ToolReceiver(ToolReceiverKind.Connection, \"" + Form
                    + "\", EditKind.ViewSession), ToolAccess.Whole(), new ToolFieldSet[]",
                source.Text);
            Assert.Contains(
                "new ToolField(\"undoCount\", \"" + Form
                    + ".UndoCount()\", typeof(global::System.Int32)),",
                source.Text);
            Assert.Equal(1, Occurrences(source.Text, "\"undoCount\""));
            Assert.Equal(2, Occurrences(source.Text, "\"pmxFormActivate\""));
        }

        [Fact]
        public void AnItemThatCannotBeWrittenIsRefusedInTheUpdatingTool()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Build(Embedded(Property("UndoCount", "System.Int32", true, false), UpdateTool)));

            Assert.StartsWith("書き込めない項目が更新へ持ち込まれている:", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemThatCannotBeReadIsRefusedInTheGettingTool()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Build(Embedded(Property("Hidden", "System.Int32", false, true), GetTool)));

            Assert.StartsWith("読み取れない項目が取得へ持ち込まれている:", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemEmbeddedInAnotherToolIsNotCollectedHere()
        {
            ToolBindingSource source = Build(
                Embedded(Property("UndoCount", "System.Int32", true, false), "view.click"));

            Assert.Empty(source.Aggregations);
        }

        [Fact]
        public void AnOwningListBringsTheAddingAndRemovingToolsOfItsElement()
        {
            ToolBindingSource source = Build(Collection());

            Assert.Equal(
                new[] { "model_add_vertices", "model_remove_vertices" }, source.Elements.ToArray());
            Assert.Contains(
                "elements.Add(\"model_add_vertices\", new ToolElements(false,"
                    + " new ToolReceiver(ToolReceiverKind.Pmx, null, EditKind.DuplicateEdit),"
                    + " new ToolAccess(ToolAccessKind.Element, \"" + ListKey
                    + "\", new ToolHop[] {  }, true, typeof(global::" + Vertex
                    + "), item => item is global::" + Vertex + ", \"vertex\", null, null)));",
                source.Text);
            Assert.Contains(
                "elements.Add(\"model_remove_vertices\", new ToolElements(true,", source.Text);
        }

        [Fact]
        public void AListOfPositionedItemsCarriesTheTypesItsItemsCanTake()
        {
            ToolBindingSource source = Build(Kinds(Vertex, Leaf), Collection());

            Assert.Contains(
                "new ToolItem(\"leaf\", typeof(global::" + Leaf + "), item => item is global::"
                    + Leaf + ")",
                source.Text);
        }

        [Fact]
        public void AListOfHandledItemsCarriesNoSuchTypes()
        {
            ToolBindingSource source = Build(Kinds(Kept, KeptLeaf), Kepts());

            Assert.DoesNotContain("new ToolItem(", source.Text);
        }

        [Fact]
        public void AnOwningListAlsoBringsTheRelayThatReadsAndWritesIt()
        {
            ToolBindingSource source = Build(Collection());

            Assert.Contains(
                "lists.Add(\"" + ListKey + "\",", source.Text);
            Assert.Contains(
                "new SdkList(owner => ((global::" + Pmx + ")owner).Vertex.Count,"
                    + " (owner, index) => ((global::" + Pmx + ")owner).Vertex[index],"
                    + " (owner, item) => ((global::" + Pmx + ")owner).Vertex.Add((global::"
                    + Vertex + ")item),"
                    + " (owner, index) => ((global::" + Pmx + ")owner).Vertex.RemoveAt(index)));",
                source.Text);
        }

        [Fact]
        public void TheFlowsNameTheRowsThatDuplicateAndReflectTheCurrentModel()
        {
            ToolBindingSource source = Build(Collection());

            Assert.Contains(
                "new PmxFlow(\"" + StateReadKey + "\", \"" + CommitKey + "\", \"" + Connector
                    + "\", new FlowSlot[] {  }, new FlowSlot[] { FlowSlot.Pmx }, \"" + StopUndoKey
                    + "\", \"" + ResumeUndoKey + "\");",
                source.Text);
            Assert.Contains(
                "new PmxFlow(\"" + BridgeReadKey + "\", \"" + BridgeCommitKey
                    + "\", null, new FlowSlot[] { FlowSlot.Connector },"
                    + " new FlowSlot[] { FlowSlot.Connector, FlowSlot.Pmx, FlowSlot.UndoLock });",
                source.Text);
        }

        [Fact]
        public void AListUnderAnElementCarriesTheTypeThatHoldsItSoTheParentCanBePointedByHandle()
        {
            ToolBindingSource source = Build(Collection(), Weights());

            Assert.Contains(
                "new ToolAccess(ToolAccessKind.Element, \"" + WeightKey
                    + "\", new ToolHop[] { new ToolHop(\"" + ListKey + "\", true) },"
                    + " true, typeof(global::" + Weight + "), item => item is global::" + Weight
                    + ", \"weight\", null, typeof(global::" + Vertex + "))",
                source.Text);
        }

        [Fact]
        public void ThePmxComesFromTheHostAndOtherOperationTargetsComeAsPositions()
        {
            SignatureRecord signature = new SignatureRecord(
                Form + ".Shape(" + Pmx + "," + Vertex + ")",
                Form,
                MemberKind.Method,
                "Shape",
                false,
                0,
                new[]
                {
                    new ParameterRecord("pmx", Pmx, ParameterDirection.In, false),
                    new ParameterRecord("vertex", Vertex, ParameterDirection.In, false),
                },
                "System.Void",
                false,
                false,
                OperationDirection.Write);

            ToolBindingSource source = Build(
                Collection(), Dispatched("session_shape_form_connector", signature));

            Assert.Contains(
                "new ToolArgument(\"pmx\", typeof(global::" + Pmx + "), true),"
                    + " new ToolArgument(\"vertex\", typeof(global::" + Vertex + "), false,"
                    + " new ToolAccess(ToolAccessKind.Element, \"" + ListKey
                    + "\", new ToolHop[] {  }, true, typeof(global::" + Vertex
                    + "), item => item is global::" + Vertex + ", \"vertex\", null, null))",
                source.Text);
        }

        /// <summary>継いだ型を1つ持つ題材の型の並び。</summary>
        private static IList<TypeRecord> Kinds(string baseType, string derived)
        {
            return new[]
            {
                new TypeRecord(
                    baseType,
                    TypeKind.Interface,
                    false,
                    false,
                    false,
                    new string[0],
                    new string[0]),
                new TypeRecord(
                    derived,
                    TypeKind.Interface,
                    false,
                    false,
                    false,
                    new[] { baseType },
                    new string[0]),
            };
        }

        /// <summary>ハンドルで指す型を並べるリストの題材。</summary>
        private static Binding Kepts()
        {
            SignatureRecord signature = new SignatureRecord(
                KeptListKey,
                Pmx,
                MemberKind.Property,
                "Kept",
                false,
                0,
                new ParameterRecord[0],
                "System.Collections.Generic.IList<" + Kept + ">",
                true,
                false,
                OperationDirection.Read);

            return new Binding(
                signature,
                null,
                new ToolMapRow(
                    KeptListKey, ToolMapEditKind.Read, null, "題材の根拠。", null, null, null));
        }

        private static Binding Collection()
        {
            SignatureRecord signature = new SignatureRecord(
                ListKey,
                Pmx,
                MemberKind.Property,
                "Vertex",
                false,
                0,
                new ParameterRecord[0],
                "System.Collections.Generic.IList<" + Vertex + ">",
                true,
                false,
                OperationDirection.Read);

            return new Binding(
                signature,
                null,
                new ToolMapRow(
                    ListKey, ToolMapEditKind.Read, null, "題材の根拠。", null, null, null));
        }

        /// <summary>要素の下にある、もう一段深いリストの題材。</summary>
        private static Binding Weights()
        {
            SignatureRecord signature = new SignatureRecord(
                WeightKey,
                Vertex,
                MemberKind.Property,
                "Weight",
                false,
                0,
                new ParameterRecord[0],
                "System.Collections.Generic.IList<" + Weight + ">",
                true,
                false,
                OperationDirection.Read);

            return new Binding(
                signature,
                null,
                new ToolMapRow(
                    WeightKey, ToolMapEditKind.Read, null, "題材の根拠。", null, null, null));
        }

        private static int Occurrences(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal);
                at >= 0;
                at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        private static ToolBindingSource Build(params Binding[] bindings)
        {
            return Build(new TypeRecord[0], bindings);
        }

        private static ToolBindingSource Build(
            IList<TypeRecord> types, params Binding[] bindings)
        {
            Dictionary<string, SignatureRecord> signatures = bindings.ToDictionary(
                b => b.Signature.Key, b => b.Signature, StringComparer.Ordinal);
            foreach (SignatureRecord flow in Flows())
            {
                signatures.Add(flow.Key, flow);
            }

            return ToolBindingSourceBuilder.Build(
                new ToolMap(bindings.Select(b => b.Row).ToList()),
                Roles(),
                new InventoryRecord(
                    "題材",
                    "0.0.0.0",
                    types.ToList(),
                    new TypeRecord[0],
                    signatures.Values.ToList()),
                bindings.Where(b => b.Tool != null)
                    .ToDictionary(b => b.Signature.Key, b => b.Tool, StringComparer.Ordinal),
                Assignments(),
                new ToolSchemaTable(new ToolSchema[0]));
        }

        /// <summary>複製編集の流れが通る2つのシグネチャ。組み立てはこの2つを名指しする。</summary>
        private static IList<SignatureRecord> Flows()
        {
            return new[]
            {
                new SignatureRecord(
                    StateReadKey,
                    Connector,
                    MemberKind.Method,
                    "GetCurrentState",
                    false,
                    0,
                    new ParameterRecord[0],
                    "PEPlugin.Pmx.IPXPmx",
                    false,
                    false,
                    OperationDirection.Read),
                new SignatureRecord(
                    CommitKey,
                    Connector,
                    MemberKind.Method,
                    "Update",
                    false,
                    0,
                    new[]
                    {
                        new ParameterRecord(
                            "pmx", "PEPlugin.Pmx.IPXPmx", ParameterDirection.In, false),
                    },
                    "System.Void",
                    false,
                    false,
                    OperationDirection.Write),
                new SignatureRecord(
                    BridgeReadKey,
                    Bridge,
                    MemberKind.Method,
                    "GetCurrentPmx",
                    true,
                    0,
                    new[]
                    {
                        new ParameterRecord("c", BridgeConnector, ParameterDirection.In, false),
                    },
                    "PEPlugin.Pmx.IPXPmx",
                    false,
                    false,
                    OperationDirection.Read),
                new SignatureRecord(
                    BridgeCommitKey,
                    Bridge,
                    MemberKind.Method,
                    "UpdatePmx",
                    true,
                    0,
                    new[]
                    {
                        new ParameterRecord("c", BridgeConnector, ParameterDirection.In, false),
                        new ParameterRecord(
                            "pmx", "PEPlugin.Pmx.IPXPmx", ParameterDirection.In, false),
                        new ParameterRecord("undo", "System.Boolean", ParameterDirection.In, false),
                    },
                    "System.Void",
                    false,
                    false,
                    OperationDirection.Write),
                new SignatureRecord(
                    StopUndoKey,
                    Connector,
                    MemberKind.Method,
                    "LockUndo",
                    false,
                    0,
                    new ParameterRecord[0],
                    "System.Void",
                    false,
                    false,
                    OperationDirection.Write),
                new SignatureRecord(
                    ResumeUndoKey,
                    Connector,
                    MemberKind.Method,
                    "UnlockUndo",
                    false,
                    0,
                    new ParameterRecord[0],
                    "System.Void",
                    false,
                    false,
                    OperationDirection.Write),
            };
        }

        private static CommonAssignmentTable Assignments()
        {
            return new CommonAssignmentTable(
                new[]
                {
                    new CommonAssignmentRecord(
                        Held + ".Drop()",
                        CommonAssignmentKind.Tool,
                        "session_release_handle",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        Form + ".LetGo(" + Held + ")",
                        CommonAssignmentKind.Tool,
                        "session_release_handle",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        StateReadKey, CommonAssignmentKind.InternalFlow, "stateRead", "題材の根拠。"),
                    new CommonAssignmentRecord(
                        CommitKey,
                        CommonAssignmentKind.InternalFlow,
                        "duplicateEdit",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        BridgeReadKey,
                        CommonAssignmentKind.InternalFlow,
                        "stateRead",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        BridgeCommitKey,
                        CommonAssignmentKind.InternalFlow,
                        "duplicateEdit",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        StopUndoKey,
                        CommonAssignmentKind.CommonArg,
                        "suppressUndo",
                        "題材の根拠。"),
                    new CommonAssignmentRecord(
                        ResumeUndoKey,
                        CommonAssignmentKind.CommonArg,
                        "suppressUndo",
                        "題材の根拠。"),
                });
        }

        [Fact]
        public void ACallThatReturnsACarriedTypeAlsoCarriesTheRowsOfItsItems()
        {
            ToolBindingSource source = Build(
                Dispatched("session_info", Method("GetInfo", Info)),
                Embedded(Carried(Info, "Name", "System.String"), "session_info"),
                Embedded(Carried(Info, "Option", Option), "session_info"),
                Embedded(Carried(Option, "Bootup", "System.Boolean"), "session_info"));

            Assert.Contains(
                "typeof(global::" + Info + "), null, new ToolField[] { new ToolField(\"name\", \""
                    + Info + ".Name()\", typeof(global::System.String)), new ToolField(\"option\","
                    + " \"" + Info + ".Option()\", typeof(global::" + Option + "),"
                    + " new ToolField[] { new ToolField(\"bootup\", \"" + Option
                    + ".Bootup()\", typeof(global::System.Boolean)) }) })",
                source.Text);
        }

        [Fact]
        public void ACallThatReturnsCarriedTypesInARowCarriesTheMarkOfWritingThemOneByOne()
        {
            ToolBindingSource source = Build(
                Dispatched("session_infos", Method("GetInfos", Info + "[]")),
                Embedded(Carried(Info, "Name", "System.String"), "session_infos"));

            Assert.Contains(
                "typeof(global::" + Info + "[]), null, new ToolField[] { new ToolField(\"name\", \""
                    + Info + ".Name()\", typeof(global::System.String)) }, null, false, true)",
                source.Text);
        }

        [Fact]
        public void OnlyTheItemsThatCanBeReadGoIntoWhatTheCallReturns()
        {
            ToolBindingSource source = Build(
                Dispatched("session_info", Method("GetInfo", Info)),
                Embedded(Carried(Info, "Name", "System.String"), "session_info"),
                Embedded(Written(Info, "Hidden", "System.String"), "session_info"));

            Assert.Contains("\"name\"", source.Text);
            Assert.DoesNotContain("\"hidden\"", source.Text);
        }

        [Fact]
        public void ACallThatReturnsACarriedTypeWithoutItsItemsIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Build(Dispatched("session_info", Method("GetInfo", Info))));

            Assert.StartsWith(
                "返す運搬用の型の項目を持ち込む行が無い:", error.Message, StringComparison.Ordinal);
        }

        private static TypeRoleTable Roles()
        {
            return new TypeRoleTable(
                new[]
                {
                    new TypeRoleRecord(
                        Form,
                        TypeRole.Connector,
                        "題材の根拠。",
                        "form_connector",
                        string.Empty,
                        CapabilityOwner.Session),
                    new TypeRoleRecord(
                        "Sdk.Bridge",
                        TypeRole.Connector,
                        "題材の根拠。",
                        "bridge",
                        string.Empty,
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Pmx,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "pmx",
                        "pmxes",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Vertex,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "vertex",
                        "vertices",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Leaf,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "leaf",
                        "leaves",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Kept,
                        TypeRole.HandleTarget,
                        "題材の根拠。",
                        "kept",
                        "kepts",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        KeptLeaf,
                        TypeRole.HandleTarget,
                        "題材の根拠。",
                        "kept_leaf",
                        "kept_leaves",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Weight,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "weight",
                        "weights",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Held,
                        TypeRole.HandleTarget,
                        "題材の根拠。",
                        "held_thing",
                        "held_things",
                        CapabilityOwner.View),
                    new TypeRoleRecord(Info, TypeRole.Dto, "題材の根拠。"),
                    new TypeRoleRecord(Option, TypeRole.Dto, "題材の根拠。"),
                },
                new HandleIssuanceRecord[0],
                new[]
                {
                    new ElementCollectionRecord(ListKey, true, "題材の根拠。", new[] { ListKey }),
                    new ElementCollectionRecord(
                        WeightKey, true, "題材の根拠。", new[] { ListKey, WeightKey }),
                });
        }

        private static Binding Dispatched(string tool, SignatureRecord signature)
        {
            return new Binding(
                signature,
                tool,
                new ToolMapRow(
                    signature.Key,
                    ToolMapEditKind.DirectChange,
                    null,
                    "題材の根拠。",
                    new[]
                    {
                        new Postcondition(
                            EffectType.None,
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
                    null));
        }

        /// <summary>生成物を返す呼び出しの束縛。返り値を台帳へ預ける行になる。</summary>
        private static Binding Issuing(string tool, SignatureRecord signature)
        {
            return new Binding(
                signature,
                tool,
                new ToolMapRow(
                    signature.Key,
                    ToolMapEditKind.DirectChange,
                    null,
                    "題材の根拠。",
                    new[]
                    {
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
                    },
                    null,
                    null));
        }

        /// <summary>解放のツールが受け持つと定めたメンバーの束縛。独立したツールを持たない。</summary>
        private static Binding Released(SignatureRecord signature)
        {
            return new Binding(
                signature,
                null,
                new ToolMapRow(
                    signature.Key, ToolMapEditKind.DirectChange, null, "題材の根拠。",
                    null, null, null));
        }

        private static Binding Embedded(SignatureRecord signature, params string[] tools)
        {
            return new Binding(
                signature,
                null,
                new ToolMapRow(
                    signature.Key,
                    ToolMapEditKind.ViewSession,
                    null,
                    "題材の根拠。",
                    null,
                    null,
                    tools));
        }

        /// <summary>ハンドル操作型が宣言するメソッド。</summary>
        private static SignatureRecord HeldMethod(string member, string valueType)
        {
            return new SignatureRecord(
                Held + "." + member + "()",
                Held,
                MemberKind.Method,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        /// <summary>引数1つを取るメソッド。引数の型は題材の側で決める。</summary>
        private static SignatureRecord Taking(string member, string valueType, string parameterType)
        {
            return new SignatureRecord(
                Form + "." + member + "(" + parameterType + ")",
                Form,
                MemberKind.Method,
                member,
                false,
                0,
                new[] { new ParameterRecord("one", parameterType, ParameterDirection.In, false) },
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        private static SignatureRecord Method(
            string member, string valueType, params string[] parameters)
        {
            return new SignatureRecord(
                Form + "." + member + "("
                    + string.Join(",", parameters.Select(p => "System.String").ToArray()) + ")",
                Form,
                MemberKind.Method,
                member,
                false,
                0,
                parameters
                    .Select(p => new ParameterRecord(p, "System.String", ParameterDirection.In, false))
                    .ToArray(),
                valueType,
                false,
                false,
                OperationDirection.Write);
        }

        /// <summary>運搬用の型が持つ、書き込みだけの項目。</summary>
        private static SignatureRecord Written(string owner, string member, string valueType)
        {
            return new SignatureRecord(
                owner + "." + member + "()",
                owner,
                MemberKind.Property,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                false,
                true,
                OperationDirection.Write);
        }

        /// <summary>運搬用の型が持つ、読み取りだけの項目。</summary>
        private static SignatureRecord Carried(string owner, string member, string valueType)
        {
            return new SignatureRecord(
                owner + "." + member + "()",
                owner,
                MemberKind.Property,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);
        }

        private static SignatureRecord Property(
            string member, string valueType, bool canRead, bool canWrite)
        {
            return new SignatureRecord(
                Form + "." + member + "()",
                Form,
                MemberKind.Property,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                canRead,
                canWrite,
                canWrite ? OperationDirection.Write : OperationDirection.Read);
        }

        /// <summary>1つの行と、そのシグネチャと、持つならツールの名前。</summary>
        private sealed class Binding
        {
            public Binding(SignatureRecord signature, string tool, ToolMapRow row)
            {
                Signature = signature;
                Tool = tool;
                Row = row;
            }

            public SignatureRecord Signature { get; }

            public string Tool { get; }

            public ToolMapRow Row { get; }
        }
    }
}
