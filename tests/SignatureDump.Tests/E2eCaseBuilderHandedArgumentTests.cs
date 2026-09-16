using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>引数でハンドルを取る呼び出しの組み立て。渡す相手も作ってから呼ぶ。</summary>
    public sealed class E2eCaseBuilderHandedArgumentTests
    {
        private const string RowKey = "PEPlugin.Vmd.IPEVmdBoneKey.CompareTo(PEPlugin.Vmd.IPEVmdFrameKey)";

        private const string Tool = "motion_compare_to_vmd_bone_key";

        private const string Receiver = "motion_create_vmd_bone_key";

        private const string Other = "motion_create_vmd_bas_camera_key";

        private const string OtherType = "PEPlugin.Vmd.IPEVmdFrameKey";

        private const string Handing = "引数に渡す相手を1つ作れること";

        private const string Handed = "args/other";

        [Fact]
        public void TheOneTheArgumentPointsToIsMadeBeforeTheCall()
        {
            IList<E2eCase> cases = Built(Makers());

            Assert.Equal(
                new[] { Other },
                cases
                    .Where(c => string.Equals(c.Purpose, Handing, StringComparison.Ordinal))
                    .Select(c => c.Tool)
                    .ToArray());
            Assert.True(
                cases.TakeWhile(
                        c => c.Expectation != E2eExpectation.Called
                            || !string.Equals(c.Tool, Tool, StringComparison.Ordinal))
                    .Any(c => string.Equals(c.Purpose, Handing, StringComparison.Ordinal)),
                "渡す相手を作る段が、呼び出しより後に来ている。");
        }

        [Fact]
        public void TheCallBorrowsTheHandleThatWasMadeForTheArgument()
        {
            IList<E2eCase> cases = Built(Makers());
            E2eCase made = cases.Single(
                c => string.Equals(c.Purpose, Handing, StringComparison.Ordinal));

            Assert.Equal(made.Produces + "/0", Called(cases).Borrowed[Handed]);
        }

        /// <summary>
        /// 借りる元の名前は、借りる側が斜線で中を辿るのに使う。名前に斜線が混ざると、名前の
        /// 途中までを名前と読んで引けなくなる。
        /// </summary>
        [Fact]
        public void TheNameOfWhatIsBorrowedCarriesNoPathSeparator()
        {
            E2eCase made = Built(Makers()).Single(
                c => string.Equals(c.Purpose, Handing, StringComparison.Ordinal));

            Assert.DoesNotContain("/", made.Produces, StringComparison.Ordinal);
        }

        /// <summary>
        /// ハンドルで指す相手は番号で書くが、どの番号を書いても台帳が預かる相手を指さない。
        /// 最小の値で埋めると、呼び先まで届かないまま届いたことにしてしまう。
        /// </summary>
        [Fact]
        public void TheArgumentIsLeftEmptyForTheHandleToBePutIn()
        {
            IDictionary<string, object> arguments = Called(Built(Makers())).Arguments;

            Assert.Null(((IDictionary<string, object>)arguments["args"])["other"]);
        }

        [Fact]
        public void ACallIsNotBuiltWhenTheArgumentHasNoMaker()
        {
            Assert.DoesNotContain(
                Built(new Dictionary<string, IList<string>>(StringComparer.Ordinal)),
                c => c.Expectation == E2eExpectation.Called
                    && string.Equals(c.Tool, Tool, StringComparison.Ordinal));
        }

        private static E2eCase Called(IList<E2eCase> cases)
        {
            return cases.Single(
                c => c.Expectation == E2eExpectation.Called
                    && string.Equals(c.Tool, Tool, StringComparison.Ordinal));
        }

        private static IDictionary<string, IList<string>> Makers()
        {
            return new Dictionary<string, IList<string>>(StringComparer.Ordinal)
            {
                { OtherType, new[] { Other } },
            };
        }

        private static IList<E2eCase> Built(IDictionary<string, IList<string>> typeMakers)
        {
            SchemaItem other = Item(null, "other", true);

            return E2eCaseBuilder.Build(
                new ToolMap(new[]
                {
                    new ToolMapRow(
                        RowKey, ToolMapEditKind.Read, null, "相手と比べて並び順を返す。",
                        null, null, null),
                }),
                new ToolSchemaTable(new[] { Taking(other), Free(Receiver), Free(Other) }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Tool } },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                new Dictionary<SchemaItem, string> { { other, OtherType } },
                null,
                null,
                null,
                null,
                null,
                null,
                new HashSet<string>(new[] { OtherType }, StringComparer.Ordinal),
                null,
                new Dictionary<string, IList<string>>(StringComparer.Ordinal)
                {
                    { Tool, new[] { Receiver } },
                },
                null,
                null,
                null,
                typeMakers);
        }

        /// <summary>受け手をハンドルで指し、引数でも相手をハンドルで取るツール。</summary>
        private static ToolSchema Taking(SchemaItem other)
        {
            return new ToolSchema(
                Tool,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[]
                        {
                            Item("number", "handles", true),
                            new SchemaItem(
                                null, new[] { other }, null, "args", ItemOrigin.HostInput, true,
                                null, false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>受け手を渡さずに呼べるツール。出たハンドルは応答の並びの中へ入る。</summary>
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

        private static SchemaItem Item(string shape, string name, bool required)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, required, null, false, null, null,
                null, false, null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }
    }
}
