using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>スキーマ正本のツールが実機の検査に覆われることの照合。</summary>
    public sealed class ToolCoverageGateTests
    {
        private const string Bone = "PEPlugin.Pmx.IPXBone";

        private const string Wipe = Bone + ".Wipe()";

        private const string WipeByName = Bone + ".Wipe(System.String)";

        private const string Counting = Bone + ".Count()";

        private const string WipeTool = "model_wipe_bone";

        private const string ListTool = "model_list_bones";

        private const string PollTool = "view_poll_events";

        private const string HandleCreated = "HandleCreated/";

        [Fact]
        public void AToolIsCoveredByACaseThatReachesIt()
        {
            Require(Schemas(WipeTool), Map(Plain(Wipe)), Named(Wipe, WipeTool), Reached(WipeTool));
        }

        [Fact]
        public void AToolWithoutAnyCaseIsNotCovered()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool, ListTool),
                    Map(Plain(Wipe)),
                    Named(Wipe, WipeTool),
                    Reached(ListTool)));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolThatIsOnlyRefusedIsNotCovered()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool), Map(Plain(Wipe)), Named(Wipe, WipeTool), Refused(WipeTool)));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolWhoseRowDeclaresAnEffectIsNotCoveredByACallThatDoesNotCheckIt()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool),
                    Map(Declaring(Wipe)),
                    Named(Wipe, WipeTool),
                    Reached(WipeTool)));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolWhoseRowDeclaresAnEffectIsCoveredByTheCaseThatChecksIt()
        {
            Require(
                Schemas(WipeTool),
                Map(Declaring(Wipe)),
                Named(Wipe, WipeTool),
                Reached(WipeTool).Concat(Checking(ListTool, Wipe)).ToArray());
        }

        [Fact]
        public void AToolReachedOnlyByAnAcceptanceScenarioIsCovered()
        {
            Require(
                Schemas(WipeTool),
                Map(Plain(Wipe)),
                Named(Wipe, WipeTool),
                new E2eCase[0],
                succeeding: WipeTool);
        }

        /// <summary>
        /// 受入シナリオの段は呼び出しが成功したことしか見ないので、宣言した効果が起きたかどうかを
        /// 一度も確かめていない。効果を宣言する行のツールは、この段だけでは覆われない。
        /// </summary>
        [Fact]
        public void AToolWhoseRowDeclaresAnEffectIsNotCoveredByAnAcceptanceScenarioAlone()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool),
                    Map(Declaring(Wipe)),
                    Named(Wipe, WipeTool),
                    new E2eCase[0],
                    succeeding: WipeTool));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolWithoutAnyRowIsJudgedToo()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool, PollTool),
                    Map(Plain(Wipe)),
                    Named(Wipe, WipeTool),
                    Reached(WipeTool)));

            Assert.Contains(PollTool, error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 同じ名前のツールを持つ行が複数あるのは多重定義の呼び分けで、どの呼び分けを通ったかは
        /// ツールの名前からは言えない。効果を宣言する呼び分けが1つでも確かめられていなければ、
        /// その宣言は一度も見られていない。
        /// </summary>
        [Fact]
        public void AToolIsNotCoveredWhenOnlyOneOfItsDeclaringRowsIsChecked()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool),
                    Map(Declaring(Wipe), Declaring(WipeByName)),
                    Named(Wipe, WipeTool, WipeByName, WipeTool),
                    Reached(WipeTool).Concat(Checking(ListTool, Wipe)).ToArray()));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolIsCoveredWhenEveryOneOfItsDeclaringRowsIsChecked()
        {
            Require(
                Schemas(WipeTool),
                Map(Declaring(Wipe), Declaring(WipeByName)),
                Named(Wipe, WipeTool, WipeByName, WipeTool),
                Reached(WipeTool)
                    .Concat(Checking(ListTool, Wipe))
                    .Concat(Checking(ListTool, WipeByName))
                    .ToArray());
        }

        /// <summary>
        /// 効果を宣言しない行は確かめる宣言を持たないので、同じツールの宣言する行の判定が要る
        /// かどうかに関わらない。
        /// </summary>
        [Fact]
        public void ARowThatDeclaresNoEffectDoesNotAskForACheck()
        {
            Require(
                Schemas(WipeTool),
                Map(Declaring(Wipe), Plain(Counting)),
                Named(Wipe, WipeTool, Counting, WipeTool),
                Reached(WipeTool).Concat(Checking(ListTool, Wipe)).ToArray());
        }

        /// <summary>
        /// 根拠が届かせられないと述べていても、そのツールは判定に入る。文言の真偽を確かめる検査が
        /// 無いので、述べたことを覆いの代わりにはできない。
        /// </summary>
        [Fact]
        public void AToolIsJudgedEvenWhenItsRowSaysItCannotBeReached()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Schemas(WipeTool),
                    Map(Unreachable(Wipe)),
                    Named(Wipe, WipeTool),
                    Refused(WipeTool)));

            Assert.Contains(WipeTool, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            ToolSchemaTable schemas = Schemas(WipeTool);
            ToolMap map = Map(Plain(Wipe));
            IDictionary<string, string> named = Named(Wipe, WipeTool);
            E2eCase[] cases = Reached(WipeTool);
            ISet<string> succeeding = new HashSet<string>(StringComparer.Ordinal);

            Assert.Throws<ArgumentNullException>(
                () => ToolCoverageGate.Require(null, map, named, cases, succeeding));
            Assert.Throws<ArgumentNullException>(
                () => ToolCoverageGate.Require(schemas, null, named, cases, succeeding));
            Assert.Throws<ArgumentNullException>(
                () => ToolCoverageGate.Require(schemas, map, null, cases, succeeding));
            Assert.Throws<ArgumentNullException>(
                () => ToolCoverageGate.Require(schemas, map, named, null, succeeding));
            Assert.Throws<ArgumentNullException>(
                () => ToolCoverageGate.Require(schemas, map, named, cases, null));
        }

        /// <summary>受入シナリオで成功を期待するツールを名前で指して照合する。</summary>
        private static void Require(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, string> toolsByRow,
            IList<E2eCase> cases,
            string succeeding = null)
        {
            ToolCoverageGate.Require(
                schemas,
                map,
                toolsByRow,
                cases,
                succeeding == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(new[] { succeeding }, StringComparer.Ordinal));
        }

        /// <summary>名前だけを持つスキーマ正本。覆いの判定は名前しか読まない。</summary>
        private static ToolSchemaTable Schemas(params string[] tools)
        {
            return new ToolSchemaTable(tools
                .Select(t => new ToolSchema(
                    t,
                    new SchemaBranch[0],
                    new SchemaItem(
                        "number", null, null, null, null, null, null, false, null, null, null,
                        false, null),
                    null))
                .ToArray());
        }

        private static ToolMap Map(params ToolMapRow[] rows)
        {
            return new ToolMap(rows);
        }

        /// <summary>効果を宣言しない行。</summary>
        private static ToolMapRow Plain(string key)
        {
            return new ToolMapRow(
                key, ToolMapEditKind.Read, null, "持っているものを返すだけである。", null, null, null);
        }

        /// <summary>呼び先まで届かせられないと述べる行。</summary>
        private static ToolMapRow Unreachable(string key)
        {
            return new ToolMapRow(
                key,
                ToolMapEditKind.Read,
                null,
                "受け手を作る手立てが無いので" + E2eCaseBuilder.UnreachableReason + "。",
                null,
                null,
                null);
        }

        /// <summary>出たハンドルを引くと述べる行。</summary>
        private static ToolMapRow Declaring(string key)
        {
            return new ToolMapRow(
                key,
                ToolMapEditKind.DirectChange,
                null,
                "ハンドルを出す。",
                new[]
                {
                    new Postcondition(
                        EffectType.HandleCreated,
                        string.Empty,
                        EffectCheckKind.Handle,
                        ListTool,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { "handles", ReferenceSpace.Result },
                        },
                        null,
                        EffectComparison.Exists,
                        null,
                        false,
                        null),
                },
                null,
                null);
        }

        /// <summary>行キーとツールの名前を交互に並べたものから、行キーからツールの名前への表へ。</summary>
        private static IDictionary<string, string> Named(params string[] pairs)
        {
            Dictionary<string, string> named =
                new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < pairs.Length; index += 2)
            {
                named.Add(pairs[index], pairs[index + 1]);
            }

            return named;
        }

        /// <summary>呼び先まで届いたことを見る検査。</summary>
        private static E2eCase[] Reached(string tool)
        {
            return new[]
            {
                new E2eCase(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    tool,
                    "呼び出して成功すること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Called,
                    null),
            };
        }

        /// <summary>入口で断られることを見る検査。</summary>
        private static E2eCase[] Refused(string tool)
        {
            return new[]
            {
                new E2eCase(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    tool,
                    "断ること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Refusal,
                    E2eCaseBuilder.InvalidHandle),
            };
        }

        /// <summary>宣言した効果を確かめる検査。</summary>
        private static E2eCase[] Checking(string tool, string rowKey)
        {
            return new[]
            {
                new E2eCase(
                    rowKey,
                    string.Empty,
                    string.Empty,
                    tool,
                    "出したハンドルを引けること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Success,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    HandleCreated),
            };
        }
    }
}
