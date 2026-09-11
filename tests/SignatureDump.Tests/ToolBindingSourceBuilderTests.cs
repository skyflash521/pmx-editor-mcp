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

        [Fact]
        public void ACallCarriesTheRowTheReceiverAndTheArgumentTypes()
        {
            ToolBindingSource source = Build(
                Dispatched("session_open_pmx_file", Method("OpenPMXFile", "System.Boolean", "path")));

            Assert.Contains(
                "calls.Add(\"session_open_pmx_file\", new ToolCall(\"" + Form
                    + ".OpenPMXFile(System.String)\", \"" + Form + "\", DangerKind.None,"
                    + " new ToolArgument[] { new ToolArgument(\"path\","
                    + " typeof(global::System.String)) }, typeof(global::System.Boolean)));",
                source.Text);
        }

        [Fact]
        public void ACallThatReturnsNothingCarriesNoResultType()
        {
            ToolBindingSource source = Build(
                Dispatched("session_undo", Method("Undo", "System.Void")));

            Assert.Contains("new ToolArgument[] {  }, null));", source.Text);
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

            Assert.Contains("new ToolCall(\"Sdk.Bridge.Ping()\", null, DangerKind.None,", source.Text);
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
                "aggregations.Add(\"" + GetTool + "\", new ToolFields(false, new ToolField[]",
                source.Text);
            Assert.Contains(
                "aggregations.Add(\"" + UpdateTool + "\", new ToolFields(true, new ToolField[]",
                source.Text);
            Assert.Contains(
                "new ToolField(\"undoCount\", \"" + Form + ".UndoCount()\", \"" + Form
                    + "\", typeof(global::System.Int32)),",
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
            return ToolBindingSourceBuilder.Build(
                new ToolMap(bindings.Select(b => b.Row).ToList()),
                Roles(),
                bindings.ToDictionary(
                    b => b.Signature.Key, b => b.Signature, StringComparer.Ordinal),
                bindings.Where(b => b.Tool != null)
                    .ToDictionary(b => b.Signature.Key, b => b.Tool, StringComparer.Ordinal));
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
                },
                new HandleIssuanceRecord[0],
                new ElementCollectionRecord[0]);
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
