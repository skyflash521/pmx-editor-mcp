using System;
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
            return new[] { new ToolDefinition(Listing, "頂点の一覧", ListingSchema) };
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
