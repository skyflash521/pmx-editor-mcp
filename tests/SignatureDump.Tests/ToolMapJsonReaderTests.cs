using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolMapJsonReaderTests
    {
        private const string CommonMembers =
            @"""assignment"": ""internalFlow"", ""target"": ""stateRead"",
              ""slotBinding"": { ""return"": ""pmxClone"", ""parameters"": {} }";

        private const string CallLogOnly =
            @"[{ ""effectType"": ""none"", ""effectKey"": """", ""kind"": ""callLogOnly"",
                 ""comparison"": ""exists"" }]";

        /// <summary>行の種別に依らない項目だけを持つ行。種別ごとの項目は呼ぶ側が足す。</summary>
        private static string Row(string key, string rowKind, string editKind, string members)
        {
            return @"{ ""signatureKey"": """ + key + @""", ""capabilityIds"": [""CAP-001""],
                       ""rowKind"": """ + rowKind + @""", ""editKind"": """ + editKind + @""",
                       ""direction"": ""read"", ""basis"": ""根拠。""" + members + "}";
        }

        private static string Common(string members)
        {
            return Row("T.M()", "commonContract", "read", ", " + CommonMembers + members);
        }

        private static string Dispatch(string postcondition, string members)
        {
            return Row(
                "T.N()",
                "directDispatch",
                "read",
                @", ""tool"": ""model_list_vertices"", ""postcondition"": " + postcondition + members);
        }

        private static string Map(params string[] rows)
        {
            return "{ \"rows\": [" + string.Join(",", rows) + "] }";
        }

        private static ToolMapRow Single(string row)
        {
            return Assert.Single(ToolMapJsonReader.Read(Map(row)).Rows);
        }

        private static void Rejects(string row)
        {
            Assert.Throws<FormatException>(() => ToolMapJsonReader.Read(Map(row)));
        }

        [Fact]
        public void ReadsACommonContractRow()
        {
            ToolMapRow row = Single(Common(string.Empty));

            Assert.Equal("T.M()", row.SignatureKey);
            Assert.Equal(new[] { "CAP-001" }, row.CapabilityIds);
            Assert.Equal(ToolMapRowKind.CommonContract, row.RowKind);
            Assert.Equal(ToolMapEditKind.Read, row.EditKind);
            Assert.Equal(OperationDirection.Read, row.Direction);
            Assert.Equal(CommonAssignmentKind.InternalFlow, row.Assignment);
            Assert.Equal("stateRead", row.Target);
            Assert.Equal(BindingSlot.PmxClone, row.SlotBinding.Returned);
            Assert.Null(row.Note);
            Assert.Null(row.DangerKind);
            Assert.Null(row.UpdateSpec);
            Assert.Null(row.Tool);
            Assert.Null(row.Postcondition);
        }

        [Fact]
        public void ReadsADirectDispatchRow()
        {
            ToolMapRow row = Single(Dispatch(CallLogOnly, string.Empty));

            Assert.Equal("model_list_vertices", row.Tool);
            Postcondition judgement = Assert.Single(row.Postcondition);
            Assert.Equal(EffectType.None, judgement.EffectType);
            Assert.Equal(string.Empty, judgement.EffectKey);
            Assert.Equal(EffectCheckKind.CallLogOnly, judgement.Kind);
            Assert.Equal(EffectComparison.Exists, judgement.Comparison);
            Assert.False(judgement.HasExpected);
            Assert.Null(judgement.Setup);
        }

        [Fact]
        public void ReadsAnEventRow()
        {
            ToolMapRow row = Single(Row(
                "T.E()", "eventBranch", "read", @", ""eventType"": ""view.mouse_click"""));

            Assert.Equal("view.mouse_click", row.EventType);
        }

        [Fact]
        public void ReadsASchemaEmbeddedRow()
        {
            ToolMapRow row = Single(Row(
                "T.P()",
                "schemaEmbedded",
                "read",
                @", ""embeddedIn"": [""model_list_vertices"", ""model_update_vertices""]"));

            Assert.Equal(
                new[] { "model_list_vertices", "model_update_vertices" }, row.EmbeddedIn);
        }

        [Fact]
        public void RejectsAnUnknownMember()
        {
            Rejects(Common(@", ""reason"": ""余分。"""));
        }

        [Fact]
        public void RejectsAMissingRequiredMember()
        {
            Rejects(@"{ ""signatureKey"": ""T.M()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": ""commonContract"", ""editKind"": ""read"", " + CommonMembers + "}");
        }

        [Fact]
        public void RejectsAMemberThatTheRowKindCannotHave()
        {
            Rejects(Common(@", ""tool"": ""model_clear_pmx"""));
            Rejects(Common(@", ""eventType"": ""view.mouse_click"""));
            Rejects(Dispatch(CallLogOnly, @", ""embeddedIn"": [""model_list_vertices""]"));
        }

        [Fact]
        public void RequiresTheToolAndThePostconditionOnADispatchRow()
        {
            Rejects(Row("T.N()", "directDispatch", "read", @", ""tool"": ""model_list_vertices"""));
            Rejects(Row("T.N()", "directDispatch", "read", @", ""postcondition"": " + CallLogOnly));
        }

        [Fact]
        public void RequiresTheEventTypeOnAnEventRow()
        {
            Rejects(Row("T.E()", "eventBranch", "read", string.Empty));
        }

        [Fact]
        public void RequiresTheAssignmentOnACommonContractRow()
        {
            Rejects(Row("T.M()", "commonContract", "read", string.Empty));
        }

        [Fact]
        public void RequiresTheUpdateSpecOnlyOnADuplicateEditRow()
        {
            Rejects(Row("T.M()", "commonContract", "duplicateEdit", ", " + CommonMembers));
            Rejects(Common(@", ""updateSpec"": { ""refresh"": [] }"));
        }

        [Fact]
        public void ReadsTheUpdateSpec()
        {
            ToolMapRow row = Single(Row(
                "T.M()",
                "commonContract",
                "duplicateEdit",
                ", " + CommonMembers
                    + @", ""updateSpec"": { ""update"": ""Vertex"", ""refresh"": [""model"", ""view""] }"));

            Assert.Equal("Vertex", row.UpdateSpec.Update);
            Assert.Equal(new[] { RefreshTarget.Model, RefreshTarget.View }, row.UpdateSpec.Refresh);
        }

        [Fact]
        public void ReadsAnUpdateSpecThatReflectsTheWholeModel()
        {
            ToolMapRow row = Single(Row(
                "T.M()",
                "commonContract",
                "duplicateEdit",
                ", " + CommonMembers + @", ""updateSpec"": { ""refresh"": [] }"));

            Assert.Null(row.UpdateSpec.Update);
            Assert.Empty(row.UpdateSpec.Refresh);
        }

        [Fact]
        public void RejectsTheSameRefreshTargetTwice()
        {
            Rejects(Row(
                "T.M()",
                "commonContract",
                "duplicateEdit",
                ", " + CommonMembers + @", ""updateSpec"": { ""refresh"": [""view"", ""view""] }"));
        }

        [Fact]
        public void RejectsRowsThatAreNotInAscendingOrder()
        {
            Assert.Throws<FormatException>(() => ToolMapJsonReader.Read(
                Map(Dispatch(CallLogOnly, string.Empty), Common(string.Empty))));
        }

        [Fact]
        public void RejectsTheSameRowKeyTwice()
        {
            Assert.Throws<FormatException>(() => ToolMapJsonReader.Read(
                Map(Common(string.Empty), Common(string.Empty))));
        }

        [Theory]
        [InlineData("[]")]
        [InlineData(@"[""CAP-001"", ""CAP-001""]")]
        public void RejectsCapabilityIdsThatAreNotOneOrMoreDistinctIds(string ids)
        {
            Rejects(@"{ ""signatureKey"": ""T.M()"", ""capabilityIds"": " + ids + @",
                        ""rowKind"": ""commonContract"", ""editKind"": ""read"",
                        ""direction"": ""read"", ""basis"": ""根拠。"", " + CommonMembers + "}");
        }

        [Fact]
        public void RequiresAtLeastOneEmbeddingTarget()
        {
            Rejects(Row("T.P()", "schemaEmbedded", "read", @", ""embeddedIn"": []"));
        }

        [Fact]
        public void RejectsTheSameEmbeddingTargetTwice()
        {
            Rejects(Row(
                "T.P()",
                "schemaEmbedded",
                "read",
                @", ""embeddedIn"": [""model_list_vertices"", ""model_list_vertices""]"));
        }

        [Fact]
        public void RequiresAtLeastOneJudgement()
        {
            Rejects(Dispatch("[]", string.Empty));
        }

        [Fact]
        public void RejectsTheSameEffectTwiceInOneRow()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""none"", ""effectKey"": """", ""kind"": ""callLogOnly"",
                     ""comparison"": ""exists"" },
                   { ""effectType"": ""none"", ""effectKey"": """", ""kind"": ""callLogOnly"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void ReadsAReadbackJudgement()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": ""arg:index"" },
                     ""valuePath"": ""items[].name"", ""comparison"": ""equals"",
                     ""expected"": ""sdkArg:value"" }]",
                string.Empty));

            Postcondition judgement = Assert.Single(row.Postcondition);
            Assert.Equal("model_list_vertices", judgement.ObserverTool);
            Assert.Equal("arg:index", judgement.ObserverArgs["index"]);
            Assert.Equal("items[].name", judgement.ValuePath);
            Assert.Equal("sdkArg:value", judgement.Expected);
            Assert.True(judgement.HasExpected);
        }

        [Fact]
        public void RequiresTheObserverOnAReadbackJudgement()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""valuePath"": ""name"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void RefusesTheObserverWhenTheComparisonDecidesWhatToWatch()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""observableChange"", ""effectKey"": """",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""comparison"": ""anyChanged"",
                     ""setup"": [{ ""tag"": ""initPmx"" }] }]",
                string.Empty));
        }

        [Fact]
        public void RequiresTheExpectedValueOnAComparisonThatUsesIt()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""countChanged"", ""effectKey"": """",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": ""total"",
                     ""comparison"": ""deltaEquals"" }]",
                string.Empty));
        }

        [Fact]
        public void RefusesTheExpectedValueOnAComparisonThatDoesNotUseIt()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""none"", ""effectKey"": """", ""kind"": ""callLogOnly"",
                     ""comparison"": ""exists"", ""expected"": 1 }]",
                string.Empty));
        }

        [Fact]
        public void RequiresANumberForTheExpectedDifference()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""countChanged"", ""effectKey"": """",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": ""total"",
                     ""comparison"": ""deltaEquals"", ""expected"": ""1"" }]",
                string.Empty));
        }

        [Fact]
        public void ReadsTheExpectedDifferenceAsANumber()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""countChanged"", ""effectKey"": """",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": ""total"",
                     ""comparison"": ""deltaEquals"", ""expected"": 1 }]",
                string.Empty));

            Assert.Equal(1, Assert.Single(row.Postcondition).Expected);
        }

        [Fact]
        public void RejectsAnObserverBindingThatIsNotAReference()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": ""index"" }, ""valuePath"": ""name"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Theory]
        [InlineData("arg:targets[].index")]
        [InlineData("arg:targets[].frames[]")]
        [InlineData("result:items[].name")]
        [InlineData("sdkArg:st_x")]
        public void AcceptsEachReferenceSpace(string reference)
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": """ + reference + @""" },
                     ""valuePath"": """", ""comparison"": ""exists"" }]",
                string.Empty));

            Assert.Equal(reference, Assert.Single(row.Postcondition).ObserverArgs["index"]);
        }

        [Theory]
        [InlineData("arg:targets[].frames[].index")]
        [InlineData("sdkArg:items[].name")]
        [InlineData("setupOut:")]
        public void RejectsAReferenceThatIsNotOfItsSpaceForm(string reference)
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": """ + reference + @""" },
                     ""valuePath"": """", ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Theory]
        [InlineData("items[].name[]")]
        [InlineData("a.b")]
        [InlineData("Name")]
        public void RejectsAValuePathThatIsDeeperThanTheContractAllows(string path)
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": """ + path + @""",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void ReadsTheSetupOfAnObservableChange()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""observableChange"", ""effectKey"": """",
                     ""kind"": ""readback"", ""comparison"": ""anyChanged"",
                     ""setup"": [
                       { ""tag"": ""initPmx"" },
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""added"" },
                       { ""tag"": ""callTool"", ""tool"": ""model_update_vertices"",
                         ""args"": { ""index"": ""setupOut:added"",
                                     ""value"": ""sample:PEPlugin.SDX.V3"" } }
                     ] }]",
                string.Empty));

            Postcondition judgement = Assert.Single(row.Postcondition);
            Assert.Equal(
                new[] { SetupTag.InitPmx, SetupTag.AddElement, SetupTag.CallTool },
                judgement.Setup.Select(s => s.Tag));
            Assert.Equal("added", judgement.Setup[1].Out);
            Assert.Equal("vertex", judgement.Setup[1].ElementType);
            Assert.Equal("model_update_vertices", judgement.Setup[2].ToolName);
            Assert.Equal("sample:PEPlugin.SDX.V3", judgement.Setup[2].Args["value"]);
        }

        [Fact]
        public void RequiresTheSetupExactlyOnTheTwoEffectsThatNeedAKnownState()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""observableChange"", ""effectKey"": """",
                     ""kind"": ""readback"", ""comparison"": ""anyChanged"" }]",
                string.Empty));

            Rejects(Dispatch(
                @"[{ ""effectType"": ""none"", ""effectKey"": """", ""kind"": ""callLogOnly"",
                     ""comparison"": ""exists"", ""setup"": [{ ""tag"": ""initPmx"" }] }]",
                string.Empty));
        }

        [Fact]
        public void RefusesAnOutputNameOnAnOperationThatOutputsNothing()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [{ ""tag"": ""initPmx"", ""out"": ""x"" }] }]",
                string.Empty));
        }

        [Fact]
        public void RejectsTwoSetupOperationsThatOutputTheSameName()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""a"" },
                       { ""tag"": ""addElement"", ""elementType"": ""bone"", ""out"": ""a"" }
                     ] }]",
                string.Empty));
        }

        [Theory]
        [InlineData("PEPlugin.SDX.V3")]
        [InlineData("PEPlugin.Vme.IPEVmePrimaryValue<1>")]
        [InlineData("System.Collections.Generic.IList<PEPlugin.Pmd.IPEBody>")]
        [InlineData("System.Action<System.Int32,T>")]
        [InlineData("System.Byte[]")]
        [InlineData("PXCPlugin.UIModel.PXUIModelHelper+TextControl")]
        public void AcceptsASampledTypeSpelledAsThePublicApiEnumerationDoes(string type)
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [{ ""tag"": ""callTool"", ""tool"": ""model_list_vertices"",
                       ""args"": { ""value"": ""sample:" + type + @""" } }] }]",
                string.Empty));

            Assert.Equal(
                "sample:" + type,
                Assert.Single(row.Postcondition).Setup[0].Args["value"]);
        }

        [Fact]
        public void RejectsAReferenceToAnOutputThatNoSetupOperationProduces()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""added"" },
                       { ""tag"": ""callTool"", ""tool"": ""model_list_vertices"",
                         ""args"": { ""index"": ""setupOut:missing"" } }
                     ] }]",
                string.Empty));

            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": ""setupOut:missing"" },
                     ""valuePath"": ""name"", ""comparison"": ""exists"",
                     ""setup"": [{ ""tag"": ""initPmx"" }] }]",
                string.Empty));

            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": ""name"",
                     ""comparison"": ""equals"", ""expected"": ""setupOut:missing"",
                     ""setup"": [{ ""tag"": ""initPmx"" }] }]",
                string.Empty));
        }

        [Fact]
        public void RejectsASetupArgumentThatPointsAtAnOutputProducedLater()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [
                       { ""tag"": ""callTool"", ""tool"": ""model_list_vertices"",
                         ""args"": { ""index"": ""setupOut:added"" } },
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""added"" }
                     ] }]",
                string.Empty));
        }

        [Fact]
        public void AcceptsAnObserverBindingThatPointsAtAnOutputProducedByAnyOperation()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": ""setupOut:added"" },
                     ""valuePath"": ""name"", ""comparison"": ""exists"",
                     ""setup"": [
                       { ""tag"": ""initPmx"" },
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""added"" }
                     ] }]",
                string.Empty));

            Assert.Equal("setupOut:added", Assert.Single(row.Postcondition).ObserverArgs["index"]);
        }

        [Fact]
        public void AcceptsAReferenceToAnOutputThatASetupOperationProduces()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""index"": ""setupOut:added"" },
                     ""valuePath"": ""name"", ""comparison"": ""exists"",
                     ""setup"": [
                       { ""tag"": ""addElement"", ""elementType"": ""vertex"", ""out"": ""added"" }
                     ] }]",
                string.Empty));

            Assert.Equal("setupOut:added", Assert.Single(row.Postcondition).ObserverArgs["index"]);
        }

        [Fact]
        public void RejectsASampleReferenceThatIsNotOfTheDefinedForm()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                     ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                     ""setup"": [{ ""tag"": ""callTool"", ""tool"": ""model_list_vertices"",
                       ""args"": { ""value"": ""sample3:V3"" } }] }]",
                string.Empty));
        }

        [Fact]
        public void ReadsTheDangerKind()
        {
            ToolMapRow row = Single(Common(@", ""dangerKind"": ""overwrite"""));

            Assert.Equal(DangerKind.Overwrite, row.DangerKind);
        }

        [Fact]
        public void ReadsTheNote()
        {
            ToolMapRow row = Single(Common(@", ""note"": ""一次資料で利用非推奨"""));

            Assert.Equal("一次資料で利用非推奨", row.Note);
        }

        [Theory]
        [InlineData("embedded", "read", "read")]
        [InlineData("commonContract", "sessionOnly", "read")]
        [InlineData("commonContract", "read", "in")]
        public void RejectsAValueThatIsNotInItsClosedSet(
            string rowKind, string editKind, string direction)
        {
            Rejects(@"{ ""signatureKey"": ""T.M()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": """ + rowKind + @""", ""editKind"": """ + editKind + @""",
                        ""direction"": """ + direction + @""", ""basis"": ""根拠。"", "
                        + CommonMembers + "}");
        }

        [Fact]
        public void RejectsADangerKindThatIsNotInItsClosedSet()
        {
            Rejects(Common(@", ""dangerKind"": ""delete"""));
        }

        [Theory]
        [InlineData("1Vertex")]
        [InlineData("Vertex.Bone")]
        public void RejectsAnUpdateSpecWhoseUpdateIsNotAnEnumeratorName(string update)
        {
            Rejects(Row(
                "T.M()",
                "commonContract",
                "duplicateEdit",
                ", " + CommonMembers + @", ""updateSpec"": { ""update"": """ + update
                    + @""", ""refresh"": [] }"));
        }

        [Fact]
        public void ReadsANestedLiteralAsTheExpectedValue()
        {
            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""position"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": ""items[].position"",
                     ""comparison"": ""equals"",
                     ""expected"": [{ ""x"": 1, ""y"": 2 }] }]",
                string.Empty));

            Assert.IsType<object[]>(Assert.Single(row.Postcondition).Expected);
        }

        [Theory]
        [InlineData(@"[{ ""X"": 1 }]")]
        [InlineData(@"{ ""inner"": { ""X"": 1 } }")]
        public void RejectsALiteralWhoseMemberNameIsNotAValueMemberName(string expected)
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""position"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": {}, ""valuePath"": """",
                     ""comparison"": ""equals"", ""expected"": " + expected + " }]",
                string.Empty));
        }

        [Fact]
        public void RejectsAnObserverArgumentNameThatIsNotAnArgumentName()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""model_list_vertices"",
                     ""observerArgs"": { ""Index"": ""arg:index"" }, ""valuePath"": ""name"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void RejectsAToolNameThatIsNotOfTheToolNameForm()
        {
            Rejects(Row(
                "T.N()",
                "directDispatch",
                "read",
                @", ""tool"": ""Model_List"", ""postcondition"": " + CallLogOnly));

            Rejects(Dispatch(
                @"[{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"",
                     ""kind"": ""readback"", ""observerTool"": ""ModelListVertices"",
                     ""observerArgs"": {}, ""valuePath"": ""name"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void RequiresAnEffectKeyOnAJudgementThatWatchesAFile()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""fileWritten"", ""effectKey"": """", ""kind"": ""file"",
                     ""comparison"": ""exists"" }]",
                string.Empty));

            ToolMapRow row = Single(Dispatch(
                @"[{ ""effectType"": ""fileWritten"", ""effectKey"": ""path"", ""kind"": ""file"",
                     ""comparison"": ""exists"" }]",
                string.Empty));

            Assert.Equal("path", Assert.Single(row.Postcondition).EffectKey);
        }

        [Fact]
        public void RejectsAnEffectKeyThatIsNotAString()
        {
            Rejects(Dispatch(
                @"[{ ""effectType"": ""none"", ""effectKey"": 1, ""kind"": ""callLogOnly"",
                     ""comparison"": ""exists"" }]",
                string.Empty));
        }

        [Fact]
        public void RejectsATargetThatIsNotOfTheFormItsAssignmentRequires()
        {
            Rejects(Row(
                "T.M()",
                "commonContract",
                "read",
                @", ""assignment"": ""tool"", ""target"": ""Model_List"",
                   ""slotBinding"": { ""parameters"": {} }"));

            Rejects(Row(
                "T.M()",
                "commonContract",
                "read",
                @", ""assignment"": ""internalFlow"", ""target"": ""release"",
                   ""slotBinding"": { ""parameters"": {} }"));
        }

        [Fact]
        public void RejectsTextThatIsNotJson()
        {
            Assert.Throws<FormatException>(() => ToolMapJsonReader.Read("rows"));
        }

        [Fact]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ToolMapJsonReader.Read(null));
        }
    }
}
