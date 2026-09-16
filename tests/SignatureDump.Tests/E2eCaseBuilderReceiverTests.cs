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

        private const string Adder = "model_add_bones";

        private const string Remover = "model_remove_bones";

        private const string Factory = "model_bone";

        private const string Aim = "pmxHandle";

        private const string Parent = "model_add_materials";

        private const string Above = "model_material";

        [Fact]
        public void ARowWhoseBasisSaysItCannotBeReachedIsStillCalled()
        {
            IList<E2eCase> cases = Built(Unreachable(), new[] { Second });

            Assert.Contains(
                cases,
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
        }

        /// <summary>
        /// 根拠の文言は事例の組み立てに関与しない。文言の真偽を確かめる検査が無いので、呼ぶか
        /// どうかを文言では決めない。
        /// </summary>
        [Fact]
        public void ARowWhoseBasisSaysCallingItShowsAPromptIsStillCalled()
        {
            IList<E2eCase> cases = Built(Prompting(), new[] { Second });

            Assert.Contains(
                cases,
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
        }

        [Fact]
        public void AReceiverThatTakesTwoStepsGetsACaseForEachStep()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            string[] making = cases
                .Where(c => string.Equals(c.Purpose, Making, StringComparison.Ordinal))
                .Select(c => c.Tool)
                .ToArray();

            Assert.Equal(new[] { First, Second }, making);
        }

        [Fact]
        public void EachStepBorrowsTheHandleTheStepBeforeItMade()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            E2eCase first = Step(cases, First);
            E2eCase second = Step(cases, Second);

            Assert.NotNull(first.Produces);
            Assert.NotNull(second.Borrowed);
            Assert.Equal(Lent(first.Produces), second.Borrowed.Values.Single());
        }

        [Fact]
        public void TheCallBorrowsTheHandleTheLastStepMade()
        {
            IList<E2eCase> cases = Built(Map(), new[] { First, Second });
            E2eCase called = cases.First(
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
            E2eCase last = Step(cases, Second);

            Assert.NotNull(last.Produces);
            Assert.NotNull(called.Borrowed);
            Assert.Equal(Lent(last.Produces), called.Borrowed.Values.Single());
        }

        /// <summary>
        /// 行を持たない要素を外すツールは、段取りが並びへ加えた要素を相手にする。ここで新しく
        /// 作った要素はまだ並びに無いので、外す相手にできない。
        /// </summary>
        [Fact]
        public void AnElementRemoverBorrowsTheHandleTheSetupAdded()
        {
            IList<E2eCase> cases = Removing(null, Held(Adder));
            E2eCase removing = cases.Single(
                c => string.Equals(c.Tool, Remover, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);

            E2eCase adding = cases.Take(cases.IndexOf(removing)).Last(
                c => string.Equals(c.Tool, Adder, StringComparison.Ordinal));
            E2eCase made = cases.Take(cases.IndexOf(adding)).Last(
                c => string.Equals(c.Tool, Factory, StringComparison.Ordinal));

            Assert.NotNull(removing.Borrowed);
            Assert.Equal(Lent(made.Produces), adding.Borrowed.Values.Single());
            Assert.Equal(Lent(made.Produces), removing.Borrowed.Values.Single());
        }

        /// <summary>
        /// 親の並びに1つも無い要素は並びへ加えられない。外す相手を用意する段は、根に近い親から
        /// 順に加える。
        /// </summary>
        [Fact]
        public void TheSetupAddsTheParentBeforeTheElementItHolds()
        {
            IList<E2eCase> cases = Removing(
                new Dictionary<string, string>(StringComparer.Ordinal) { { Adder, Parent } },
                Held(Adder));
            E2eCase removing = cases.Single(
                c => string.Equals(c.Tool, Remover, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);

            Assert.Equal(
                new[] { Above, Parent, Factory, Adder },
                cases
                    .Take(cases.IndexOf(removing))
                    .Where(c => c.Purpose.Contains("呼び出しの前に並びへ"))
                    .Select(c => c.Tool)
                    .ToArray());
        }

        /// <summary>
        /// 親の位置を指す組で加えるツールでは、借りたハンドルを差し込む先が組の中の道になる。
        /// ハンドルの並びへ差し込むと、その道が引数に無いので値が届かない。
        /// </summary>
        [Fact]
        public void TheSetupPutsTheHandleIntoTheAssignmentWhenTheAdderTakesParents()
        {
            IList<E2eCase> cases = Removing(
                new Dictionary<string, string>(StringComparer.Ordinal) { { Adder, Parent } },
                Placed(Adder));
            E2eCase adding = cases.Last(
                c => string.Equals(c.Tool, Adder, StringComparison.Ordinal)
                    && c.Borrowed != null);

            Assert.Equal("assignments/0/handles/0", adding.Borrowed.Keys.Single());
        }

        /// <summary>
        /// 要素を外すツール1つぶんの検査。<paramref name="parents"/> は親の対応表、
        /// <paramref name="adder"/> は並びへ加えるツールの受け取る形。
        /// </summary>
        private static IList<E2eCase> Removing(
            IDictionary<string, string> parents, ToolSchema adder)
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(new[]
                {
                    Held(Remover), adder, Free(Factory), Held(Parent), Free(Above),
                }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Adder, Factory },
                    { Parent, Above },
                },
                null,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal) { { Remover, Adder } },
                null,
                null,
                parents);
        }

        /// <summary>
        /// 対象を指して呼べば確認が要らなくなるツールは、新しく作った相手を指して呼ぶ。いま
        /// 開いているものを相手にすると、検査が実機の状態を壊す。
        /// </summary>
        [Fact]
        public void AToolThatNeedsNoConfirmationWhenAimedIsAimedAtAFreshReceiver()
        {
            IList<E2eCase> cases = Aiming();
            E2eCase called = cases.Single(
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
            E2eCase made = Step(cases, Second);

            Assert.NotNull(made.Produces);
            Assert.True(called.Arguments.ContainsKey(Aim));
            Assert.False(called.Arguments.ContainsKey(E2eCaseBuilder.ConfirmName));
            Assert.NotNull(called.Borrowed);
            Assert.Equal(Aim, called.Borrowed.Keys.Single());
            Assert.Equal(Lent(made.Produces), called.Borrowed.Values.Single());
        }

        [Fact]
        public void TheConfirmationRefusalStaysForAToolThatIsAimed()
        {
            Assert.Contains(
                Aiming(),
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Refusal
                    && string.Equals(
                        c.Code, E2eCaseBuilder.ConfirmRequired, StringComparison.Ordinal));
        }

        /// <summary>新しく作った相手を指して呼ぶ、確認を要する行の検査。</summary>
        private static IList<E2eCase> Aiming()
        {
            return E2eCaseBuilder.Build(
                Wiping(),
                new ToolSchemaTable(new[] { Aimed(Tool), Free(Second) }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Tool } },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(new[] { RowKey }, StringComparer.Ordinal),
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
                new Dictionary<string, IList<string>>(StringComparer.Ordinal)
                {
                    { Tool, new[] { Second } },
                },
                null,
                null,
                new HashSet<string>(new[] { Tool }, StringComparer.Ordinal));
        }

        /// <summary>受け手を1つのハンドルで指すツール。</summary>
        private static ToolSchema Aimed(string tool)
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
                                "number", null, null, Aim, ItemOrigin.HostInput, false,
                                null, false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>
        /// 読み込む中身が要るツールのために、先に書き出しておく段取り。読み込めるモデルを作れるのは
        /// エディタだけなので、検査の中で書き出して、それを読ませる。
        /// </summary>
        [Fact]
        public void ThePreparationComesBeforeEveryOtherCase()
        {
            IList<E2eCase> cases = Preparing();

            Assert.Equal(
                new[]
                {
                    E2eCaseBuilder.SavingPmdToolName,
                    E2eCaseBuilder.SavingPmxToolName,
                    E2eCaseBuilder.SavingViewSettingToolName,
                },
                cases.Take(3).Select(c => c.Tool).ToArray());
            Assert.Equal(Second, cases[3].Tool);
        }

        [Fact]
        public void ThePreparationPassesThePlaceAndTheConfirmation()
        {
            IList<E2eCase> cases = Preparing();

            Assert.Equal(E2eCaseBuilder.SavedPmdPath, cases[0].Arguments["path"]);
            Assert.Equal(E2eCaseBuilder.SavedPmxPath, cases[1].Arguments["path"]);
            Assert.Equal(E2eCaseBuilder.SavedViewSettingPath, cases[2].Arguments["path"]);
            foreach (E2eCase one in cases.Take(3))
            {
                Assert.Equal(true, one.Arguments[E2eCaseBuilder.ConfirmName]);
                Assert.Equal(E2eExpectation.Success, one.Expectation);
                Assert.Equal("path", one.Writes);
            }
        }

        /// <summary>書き出すツールと、そのあとに続くツールで組み立てた検査。</summary>
        private static IList<E2eCase> Preparing()
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(new[]
                {
                    Saving(E2eCaseBuilder.SavingPmdToolName),
                    Saving(E2eCaseBuilder.SavingPmxToolName),
                    Saving(E2eCaseBuilder.SavingViewSettingToolName),
                    Free(Second),
                }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>());
        }

        /// <summary>いま開いているモデルを書き出すツール。</summary>
        private static ToolSchema Saving(string tool)
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
                                "text", null, null, "path", ItemOrigin.HostInput, true, null,
                                false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
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
            return Rows("受け手を作る手立てが無い。");
        }

        private static ToolMap Prompting()
        {
            return Rows("呼ぶと確認を求められる。");
        }

        private static ToolMap Rows(string basis)
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(RowKey, ToolMapEditKind.Read, null, basis, null, null, null),
            });
        }

        /// <summary>中身を空へ戻す、確認を要する行1件の表。</summary>
        private static ToolMap Wiping()
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(
                    RowKey,
                    ToolMapEditKind.DuplicateEdit,
                    null,
                    "中身を空へ戻す。確認を要する危険な操作である。",
                    null,
                    null,
                    null),
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

        /// <summary>作った要素を、親の位置と要素のハンドルの組で受け取るツール。</summary>
        private static ToolSchema Placed(string tool)
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
                                null,
                                null,
                                new SchemaItem(
                                    null,
                                    new[] { Taken("handles"), Taken("parentIndex") },
                                    null,
                                    null,
                                    ItemOrigin.HostInput,
                                    null,
                                    null,
                                    false,
                                    null,
                                    null,
                                    null,
                                    false,
                                    null),
                                "assignments",
                                ItemOrigin.HostInput,
                                true,
                                null,
                                false,
                                null,
                                null,
                                null,
                                false,
                                null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        private static SchemaItem Taken(string name)
        {
            return new SchemaItem(
                "number", null, null, name, ItemOrigin.HostInput, true, null, false, null, null,
                null, false, null);
        }

        /// <summary>
        /// 受け手を渡さずに呼べるツール。出たハンドルは、実機と同じく応答の並びの中へ入る。
        /// </summary>
        private static ToolSchema Free(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, null, Output(), null, ItemOrigin.HostOutput, null, null, false, null,
                    null, null, false, null),
                null);
        }

        /// <summary>その名前が出したハンドル1つを指す道。</summary>
        private static string Lent(string name)
        {
            return name + "/0";
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }
    }
}
