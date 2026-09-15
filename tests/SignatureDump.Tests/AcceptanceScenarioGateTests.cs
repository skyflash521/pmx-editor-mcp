using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>受入シナリオの定義が、実行器の解せる形と、登録される定義と要求に合うことの照合。</summary>
    public sealed class AcceptanceScenarioGateTests
    {
        private const string Task = "モデルの構造の把握";

        private const string Condition = "標準のMCPで動く製品にし、特定のクライアントに縛られない";

        private const string Listing = "model_list_vertices";

        private const string ListingSchema =
            @"{""type"":""object"",""properties"":{""all"":{""type"":""boolean""},"
                + @"""limit"":{""type"":""number"",""minimum"":1}},""required"":[""all""],"
                + @"""additionalProperties"":false}";

        private const string Adding = "model_add_vertices";

        private const string AddingSchema =
            @"{""type"":""object"",""properties"":{""handles"":{""type"":""array"","
                + @"""items"":{""type"":""number""},""minItems"":1}},"
                + @"""required"":[""handles""],""additionalProperties"":false}";

        [Fact]
        public void AScenarioThatCallsARegisteredToolWithArgumentsItsSchemaAcceptsIsAccepted()
        {
            Require(Scenario(Task, Step(Listing, @"{""all"":true}")));
        }

        [Fact]
        public void AScenarioThatCallsAToolNoDefinitionRegistersIsRejected()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, Step("model_list_wings", @"{""all"":true}"))));

            Assert.Contains("model_list_wings", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AScenarioWhoseArgumentsTheInputSchemaRefusesIsRejected()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, Step(Listing, @"{""all"":true,""limit"":0}"))));

            Assert.Contains(Listing, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AScenarioThatOmitsARequiredArgumentIsRejected()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, Step(Listing, @"{}"))));

            Assert.Contains(Listing, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AScenarioThatPointsAtARequirementTheDocumentDoesNotNameIsRejected()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario("速いこと", Step(Listing, @"{""all"":true}"))));

            Assert.Contains("速いこと", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ScenariosThatLeaveATaskWithoutAnyScenarioAreRejected()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Condition, Step(Listing, @"{""all"":true}"))));

            Assert.Contains(Task, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TwoScenariosWithTheSameNumberAreRejected()
        {
            string one = Body(1, Task, Step(Listing, @"{""all"":true}"));
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(one + "," + one));

            Assert.Contains("二度現れる", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AStepThatBorrowsAValueNoEarlierStepRecordsIsRejected()
        {
            string step = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true,""limit"":{""$from"":""count""}},"
                + @"""expect"":{""ok"":true}}";
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, step)));

            Assert.Contains("count", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AStepThatBorrowsAValueAnEarlierStepRecordsIsAccepted()
        {
            string recording = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},""expect"":{""ok"":true},"
                + @"""record"":{""name"":""count"",""path"":""total"",""shape"":""number""}}";
            string borrowing = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true,""limit"":{""$from"":""count""}},"
                + @"""expect"":{""ok"":true}}";

            Require(Scenario(Task, recording + "," + borrowing));
        }

        [Fact]
        public void AStepThatBorrowsARowOfNumbersIntoAnInputThatTakesARowIsAccepted()
        {
            string recording = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},""expect"":{""ok"":true},"
                + @"""record"":{""name"":""made"",""path"":"""",""shape"":""numbers""}}";
            string borrowing = @"{""kind"":""tool"",""tool"":""" + Adding
                + @""",""arguments"":{""handles"":{""$from"":""made""}},"
                + @"""expect"":{""ok"":true}}";

            Require(Scenario(Task, recording + "," + borrowing));
        }

        [Fact]
        public void AStepThatBorrowsARowOfNumbersIntoAnInputThatTakesOneNumberIsRejected()
        {
            string recording = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},""expect"":{""ok"":true},"
                + @"""record"":{""name"":""made"",""path"":"""",""shape"":""numbers""}}";
            string borrowing = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true,""limit"":{""$from"":""made""}},"
                + @"""expect"":{""ok"":true}}";
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, recording + "," + borrowing)));

            Assert.Contains(Listing, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnImageExpectationThatPointsAtSomethingNotCapturedIsRejected()
        {
            string step = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},"
                + @"""expect"":{""ok"":true,""image"":{""capturedAs"":""viewSize""}}}";
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, step)));

            Assert.Contains("viewSize", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnExpectationTheRunnerCannotEvaluateIsRejected()
        {
            string step = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},""expect"":{""looksRight"":true}}";
            FormatException error = Assert.Throws<FormatException>(
                () => AcceptanceScenarioGate.Read(
                    @"{""scenarios"":[" + Body(1, Task, step) + "]}"));

            Assert.Contains("実行器の解せる形", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AStepOfAKindTheRunnerDoesNotKnowIsRejected()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => AcceptanceScenarioGate.Read(
                    @"{""scenarios"":[" + Body(1, Task, @"{""kind"":""wait"",""seconds"":3}")
                        + "]}"));

            Assert.Contains("実行器の解せる形", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ANoticeExpectationCanPointAtTheEditorAnEarlierControlStepRecords()
        {
            string launching = @"{""kind"":""control"",""action"":""launch"","
                + @"""record"":{""name"":""editorA"",""shape"":""number""}}";
            string pinging = @"{""kind"":""tool"",""tool"":""ping"",""arguments"":{},"
                + @"""expect"":{""ok"":true,""notice"":{""editor"":{""$from"":""editorA""}}}}";
            string listing = @"{""kind"":""tool"",""tool"":""" + Listing
                + @""",""arguments"":{""all"":true},""expect"":{""ok"":true}}";

            Require(Scenario(Task, launching + "," + pinging + "," + listing));
        }

        [Fact]
        public void AFixedToolCalledWithArgumentsIsRejected()
        {
            string step = @"{""kind"":""tool"",""tool"":""ping"",""arguments"":{""times"":2},"
                + @"""expect"":{""ok"":true}}";
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(Scenario(Task, step + "," + Step(Listing, @"{""all"":true}"))));

            Assert.Contains("ping", error.Message, StringComparison.Ordinal);
        }

        private static void Require(string scenarios)
        {
            AcceptanceScenarioGate.Require(
                AcceptanceScenarioGate.Read(@"{""scenarios"":[" + scenarios + "]}"),
                Definitions(),
                Fixed(),
                Requirements());
        }

        /// <summary>
        /// 実物が立てる期待の形が題材に無ければ落ちる。突き合わせは題材で走るので、題材に無い
        /// 形は、実行器がそれを見ていなくても気づけないまま通る。
        /// </summary>
        [Fact]
        public void AFormTheStubDoesNotCarryIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AcceptanceScenarioGate.RequireCoveredByStub(
                    Cases(@"{""kind"":""tool"",""tool"":""one"",""arguments"":{},"
                        + @"""expect"":{""ok"":true,""code"":""TOOL_X""}}"),
                    Cases(@"{""kind"":""tool"",""tool"":""one"",""arguments"":{},"
                        + @"""expect"":{""ok"":true}}")));

            Assert.Contains("code", error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 同じ名前でも中身が別の形を立てる期待は、別の形として数える。名前だけで数えると、
        /// 題材が片方しか持たないまま覆えたことになる。
        /// </summary>
        [Fact]
        public void TwoFormsUnderTheSameNameAreCountedApart()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AcceptanceScenarioGate.RequireCoveredByStub(
                    Cases(@"{""kind"":""tool"",""tool"":""one"",""arguments"":{},"
                        + @"""expect"":{""ok"":true,""notice"":{""changedTo"":{""$from"":""a""}}}}"),
                    Cases(@"{""kind"":""tool"",""tool"":""one"",""arguments"":{},"
                        + @"""expect"":{""ok"":true,""notice"":{""editor"":{""$from"":""a""}}}}")));

            Assert.Contains("notice.changed", error.Message, StringComparison.Ordinal);
        }

        /// <summary>実物が頼む操作の種類が題材に無ければ落ちる。</summary>
        [Fact]
        public void AnActionTheStubDoesNotCarryIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AcceptanceScenarioGate.RequireCoveredByStub(
                    Cases(@"{""kind"":""control"",""action"":""undo"","
                        + @"""editor"":{""$from"":""a""}}"),
                    Cases(@"{""kind"":""control"",""action"":""closeAll""}")));

            Assert.Contains("undo", error.Message, StringComparison.Ordinal);
        }

        /// <summary>題材が実物の形と操作をすべて持つなら通る。</summary>
        [Fact]
        public void AStubThatCarriesEveryFormAndActionPasses()
        {
            AcceptanceScenarioGate.RequireCoveredByStub(
                Cases(@"{""kind"":""control"",""action"":""closeAll""}"),
                Cases(@"{""kind"":""control"",""action"":""closeAll""},"
                    + @"{""kind"":""control"",""action"":""undo"",""editor"":{""$from"":""a""}}"));
        }

        /// <summary>実物が置く置き場の段の種類が題材に無ければ落ちる。</summary>
        [Fact]
        public void AFileActionTheStubDoesNotCarryIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AcceptanceScenarioGate.RequireCoveredByStub(
                    Cases(@"{""kind"":""file"",""action"":""removeTree"",""path"":""held""}"),
                    Cases(@"{""kind"":""file"",""action"":""ensureDirectory"","
                        + @"""path"":""held""}")));

            Assert.Contains("removeTree", error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 実物が応答を作る相手を起こし直すのに題材が起こし直さなければ落ちる。起こした回数を
        /// 見る突き合わせは、段が0件でも数が合ってしまうので、この経路が走らないまま通る。
        /// </summary>
        [Fact]
        public void ARestartTheStubDoesNotCarryIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AcceptanceScenarioGate.RequireCoveredByStub(
                    Cases(@"{""kind"":""server"",""action"":""restart""}"),
                    Cases(@"{""kind"":""control"",""action"":""closeAll""}")));

            Assert.Contains("restart", error.Message, StringComparison.Ordinal);
        }

        /// <summary>段だけを差し替えた定義。形と操作の照合はこれだけを読む。</summary>
        private static JsonNode Cases(string steps)
        {
            return JsonNode.Parse(
                @"{""scenarios"":[" + Body(1, "読み取りができる", steps) + "]}");
        }

        private static string Scenario(string requirement, string steps)
        {
            return Body(1, requirement, steps);
        }

        private static string Body(int id, string requirement, string steps)
        {
            return @"{""id"":" + id + @",""title"":""読み取り"",""requirements"":[""" + requirement
                + @"""],""steps"":[" + steps + "]}";
        }

        private static string Step(string tool, string arguments)
        {
            return @"{""kind"":""tool"",""tool"":""" + tool + @""",""arguments"":" + arguments
                + @",""expect"":{""ok"":true}}";
        }

        private static IList<ToolDefinition> Definitions()
        {
            return new[]
            {
                new ToolDefinition(Listing, "頂点の一覧", ListingSchema, false),
                new ToolDefinition(Adding, "頂点の追加", AddingSchema, false),
            };
        }

        private static ISet<string> Fixed()
        {
            return new HashSet<string>(new[] { "ping" }, StringComparer.Ordinal);
        }

        private static RequirementNames Requirements()
        {
            return new RequirementNames(new[] { Task }, new[] { Condition });
        }
    }
}
