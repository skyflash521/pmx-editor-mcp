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

        private const string Weight = "PEPlugin.Pmx.IPXWeight";

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
                    new TypeRecord[0],
                    new TypeRecord[0],
                    signatures.Values.ToList()),
                bindings.Where(b => b.Tool != null)
                    .ToDictionary(b => b.Signature.Key, b => b.Tool, StringComparer.Ordinal),
                Assignments());
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
                        Weight,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "weight",
                        "weights",
                        CapabilityOwner.Model),
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
