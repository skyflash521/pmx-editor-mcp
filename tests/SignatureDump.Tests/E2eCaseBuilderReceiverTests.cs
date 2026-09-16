using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>受け手のハンドルを順に作ってから呼ぶ検査の組み立て。</summary>
    public sealed class E2eCaseBuilderReceiverTests
    {
        private const string RowKey = "PEPlugin.Pmx.IPXBone.Wipe()";

        private const string Tool = "model_wipe_bone";

        private const string First = "model_pmx";

        private const string Second = "model_bone";

        private const string Making = "呼び出しの相手を1つ作れること";

        [Fact(Skip = "impl pending: 呼び先まで届かせられないと根拠が述べる行でも受け手の列があれば呼ぶ")]
        public void ARowWhoseBasisSaysItCannotBeReachedIsStillCalled()
        {
            IList<E2eCase> cases = Built(Unreachable(), new[] { Second });

            Assert.Contains(
                cases,
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
        }

        [Fact(Skip = "impl pending: 二段掛かる受け手は段ごとに1件ずつ相手を作る検査を並べる")]
        public void AReceiverThatTakesTwoStepsGetsACaseForEachStep()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            string[] making = cases
                .Where(c => string.Equals(c.Purpose, Making, StringComparison.Ordinal))
                .Select(c => c.Tool)
                .ToArray();

            Assert.Equal(new[] { First, Second }, making);
        }

        [Fact(Skip = "impl pending: 二段目は一段目が出したハンドルを借りて呼ぶ")]
        public void EachStepBorrowsTheHandleTheStepBeforeItMade()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            E2eCase first = Step(cases, First);
            E2eCase second = Step(cases, Second);

            Assert.NotNull(first.Produces);
            Assert.NotNull(second.Borrowed);
            Assert.Equal(first.Produces, second.Borrowed.Values.Single());
        }

        [Fact(Skip = "impl pending: 最後の呼び出しは列の終わりが出したハンドルを借りる")]
        public void TheCallBorrowsTheHandleTheLastStepMade()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            E2eCase called = cases.First(
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
            E2eCase last = Step(cases, Second);

            Assert.NotNull(last.Produces);
            Assert.NotNull(called.Borrowed);
            Assert.Equal(last.Produces, called.Borrowed.Values.Single());
        }

        /// <summary>その段が相手を1つ作る検査。</summary>
        private static E2eCase Step(IList<E2eCase> cases, string tool)
        {
            return cases.Single(
                c => string.Equals(c.Purpose, Making, StringComparison.Ordinal)
                    && string.Equals(c.Tool, tool, StringComparison.Ordinal));
        }

        private static IList<E2eCase> Built(ToolMap map, IList<string> path)
        {
            return E2eCaseBuilder.Build(
                map,
                new ToolSchemaTable(new[] { Held(Tool), Free(First), Free(Second) }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Tool } },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, IList<string>>(StringComparer.Ordinal) { { Tool, path } });
        }

        private static ToolMap Map()
        {
            return Rows("持っているものを返すだけである。");
        }

        private static ToolMap Unreachable()
        {
            return Rows("受け手を作る手立てが無いので" + E2eCaseBuilder.UnreachableReason + "。");
        }

        private static ToolMap Rows(string basis)
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(RowKey, ToolMapEditKind.Read, null, basis, null, null, null),
            });
        }

        /// <summary>受け手をハンドルで指すツール。</summary>
        private static ToolSchema Held(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[]
                        {
                            new SchemaItem(
                                "number", null, null, "handles", ItemOrigin.HostInput, true, null,
                                false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>受け手を渡さずに呼べるツール。</summary>
        private static ToolSchema Free(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }
    }
}
