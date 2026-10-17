using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 作った要素を並びへ加える前に、位置で指す項目を埋める段の組み立て。埋めずに加えると、
    /// 指す先を持たない要素として書き戻しで捨てられる。
    /// </summary>
    public sealed class E2eCaseBuilderWiringTests
    {
        private const string Adder = "model_add_faces";

        private const string Factory = "model_face";

        private const string Writer = "model_update_faces";

        private const string Member = "vertex1";

        private const string Another = "vertex2";

        private const string Aimed = "Sdk.Vertex";

        private const string AimedAdder = "model_add_vertices";

        private const string AimedFactory = "model_vertex";

        private const string Offsets = "model_add_morph_offsets";

        private const string OffsetFactory = "model_vertex_morph_offset";

        private const string OffsetWriter = "model_update_morph_offsets";

        private const string Selector = "kind";

        private const string VertexOffset = "vertex_morph_offset";

        private const string BoneOffset = "bone_morph_offset";

        [Fact]
        public void TheOnesThePointedMembersNeedAreFilledBeforeTheElementIsAdded()
        {
            IList<E2eCase> cases = Built();

            Assert.Equal(
                new[] { Factory, Writer, Adder },
                cases
                    .Select(c => c.Tool)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
        }

        [Fact]
        public void ThePointedMembersAreSetOnTheOneThatWasJustMade()
        {
            E2eCase writing = Built().First(
                c => string.Equals(c.Tool, Writer, StringComparison.Ordinal));
            E2eCase made = Built().First(
                c => string.Equals(c.Tool, Factory, StringComparison.Ordinal));

            Assert.Equal(
                0, ((IDictionary<string, object>)writing.Arguments["value"])[Member]);
            Assert.Equal(made.Produces + "/0", writing.Borrowed["handles/0"]);
        }

        /// <summary>
        /// 名指しした項目が位置で数えられなくなったら落とす。段が黙って消えると、指す先を持たない
        /// まま加えた要素が書き戻しで捨てられる検査が、また合格として数えられる。
        /// </summary>
        [Fact]
        public void ANamedMemberThatIsNoLongerCountedByPositionIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Built(sdkTypes: new Dictionary<SchemaItem, string>()));

            Assert.Contains(
                "埋める段を1つも生んでいない", error.Message, StringComparison.Ordinal);
        }

        /// <summary>名指しした書き換えのツールへ写る要素が無くなったら落とす。</summary>
        [Fact]
        public void ANamedToolThatNoElementWritesThroughIsRefused()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Built(targeted: Targeted("model_update_bones", "parent")));

            Assert.Contains(
                "model_update_bones", error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 共通契約が挙げていない項目は埋めない。指す先が無くても捨てられない要素にまで段を出すと、
        /// 確かめるものが増えないまま実機の検査が伸びる。
        /// </summary>
        [Fact]
        public void NothingIsSetOnAMemberTheContractDoesNotName()
        {
            Assert.DoesNotContain(
                Built(targeted: new Dictionary<string, ISet<string>>(StringComparer.Ordinal)),
                c => string.Equals(c.Tool, Writer, StringComparison.Ordinal));
        }

        /// <summary>
        /// 同じ組の中でも、共通契約が挙げた項目だけを埋める。挙がっていない項目は、指す先が無くても
        /// 要素ごと捨てられはしない。
        /// </summary>
        [Fact]
        public void OnlyTheMembersTheContractNamesAreSet()
        {
            E2eCase writing = Built().First(
                c => string.Equals(c.Tool, Writer, StringComparison.Ordinal));

            Assert.Equal(
                new[] { Member },
                ((IDictionary<string, object>)writing.Arguments["value"]).Keys.ToArray());
        }

        /// <summary>親へ揃える値を挙げた並びでは、加える前に親を揃える段を出す。</summary>
        [Fact]
        public void TheParentIsAlignedBeforeTheElementIsAdded()
        {
            IList<E2eCase> cases = Built(
                parentValues: new Dictionary<string, ParentValues>(StringComparer.Ordinal)
                {
                    { Adder, new ParentValues(OffsetWriter, "kind", "Bone") },
                });
            E2eCase aligned = cases.Single(
                c => string.Equals(c.Tool, OffsetWriter, StringComparison.Ordinal));
            E2eCase added = cases.Single(
                c => string.Equals(c.Tool, Adder, StringComparison.Ordinal));

            Assert.Equal(
                "Bone", ((IDictionary<string, object>)aligned.Arguments["value"])["kind"]);
            Assert.True(
                cases.IndexOf(aligned) < cases.IndexOf(added),
                "親を揃える段が、加える段より後に来ている。");
        }

        /// <summary>指す先の並びが空だと位置で指せないので、指す先の要素を先に1つ作って加える。</summary>
        [Fact]
        public void ThePointedAtListIsFilledBeforeTheOneThatPointsAtIt()
        {
            IList<E2eCase> cases = Built(
                more: new[] { Held(AimedAdder), Free(AimedFactory) },
                factories: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Adder, Factory },
                    { AimedAdder, AimedFactory },
                },
                addersByType: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Aimed, AimedAdder },
                });

            Assert.Equal(
                new[] { AimedFactory, AimedAdder, Factory, Writer, Adder },
                cases
                    .Select(c => c.Tool)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
        }

        /// <summary>
        /// 種別で呼び分ける書き換えでは、選んだ呼び分けが受け取る項目だけを埋める。別の呼び分けの
        /// 項目まで渡すと、受け取らない項目を渡した呼び出しとして退けられる。
        /// </summary>
        [Fact]
        public void OnlyTheMembersOfTheChosenBranchAreSet()
        {
            E2eCase writing = Branched().First(
                c => string.Equals(c.Tool, OffsetWriter, StringComparison.Ordinal));
            IDictionary<string, object> value =
                (IDictionary<string, object>)writing.Arguments["value"];

            Assert.Equal(VertexOffset, writing.Arguments[Selector]);
            Assert.Equal(new[] { "vertex" }, value.Keys.ToArray());
        }

        private static IList<E2eCase> Built(
            IDictionary<SchemaItem, string> sdkTypes = null,
            IDictionary<string, ISet<string>> targeted = null,
            IEnumerable<ToolSchema> more = null,
            IDictionary<string, string> factories = null,
            IDictionary<string, string> addersByType = null,
            IDictionary<string, ParentValues> parentValues = null)
        {
            SchemaItem member = Item("number", Member);
            SchemaItem another = Item("number", Another);

            return Built(
                new[] { Held(Adder), Free(Factory), Writing(member, another) }
                    .Concat(more ?? new ToolSchema[0])
                    .ToArray(),
                sdkTypes ?? new Dictionary<SchemaItem, string>
                {
                    { member, Aimed },
                    { another, Aimed },
                },
                factories ?? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Adder, Factory },
                },
                addersByType,
                new Dictionary<string, string>(StringComparer.Ordinal) { { Adder, Writer } },
                targeted ?? Targeted(Writer, Member),
                Writer,
                parentValues);
        }

        /// <summary>種別で呼び分ける書き換えを持つ、モーフのオフセットの並び。</summary>
        private static IList<E2eCase> Branched()
        {
            SchemaItem vertex = Item("number", "vertex");
            SchemaItem bone = Item("number", "bone");

            return Built(
                new[] { Held(Offsets), Free(OffsetFactory), Choosing(vertex, bone) },
                new Dictionary<SchemaItem, string> { { vertex, Aimed }, { bone, Aimed } },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Offsets, OffsetFactory },
                },
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Offsets, OffsetWriter },
                },
                Targeted(OffsetWriter, "vertex", "bone"),
                OffsetWriter);
        }

        private static IList<E2eCase> Built(
            ToolSchema[] schemas,
            IDictionary<SchemaItem, string> sdkTypes,
            IDictionary<string, string> factories,
            IDictionary<string, string> addersByType,
            IDictionary<string, string> updaters,
            IDictionary<string, ISet<string>> targeted,
            string writer,
            IDictionary<string, ParentValues> parentValues = null)
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(schemas),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                sdkTypes,
                null,
                null,
                new HashSet<string>(new[] { Aimed }, StringComparer.Ordinal),
                factories,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                addersByType,
                updaters,
                targeted,
                parentValues)
                .Where(c => !string.Equals(c.Tool, writer, StringComparison.Ordinal)
                    || c.Borrowed != null)
                .ToList();
        }

        private static IDictionary<string, ISet<string>> Targeted(
            string tool, params string[] members)
        {
            return new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
            {
                { tool, new HashSet<string>(members, StringComparer.Ordinal) },
            };
        }

        /// <summary>位置で指す項目を2つ持つ、要素の書き換えのツール。</summary>
        private static ToolSchema Writing(SchemaItem member, SchemaItem another)
        {
            return new ToolSchema(
                Writer,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Item("number", "handles"), Group(member, another) },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>種別で呼び分ける、要素の書き換えのツール。呼び分けごとに指す項目が違う。</summary>
        private static ToolSchema Choosing(SchemaItem vertex, SchemaItem bone)
        {
            return new ToolSchema(
                OffsetWriter,
                new[]
                {
                    new SchemaBranch(
                        VertexOffset,
                        Selector,
                        VertexOffset,
                        new[] { Item("number", "handles"), Group(vertex) },
                        new SchemaChoice[0]),
                    new SchemaBranch(
                        BoneOffset,
                        Selector,
                        BoneOffset,
                        new[] { Item("number", "handles"), Group(bone) },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>書き換えが受け取る値の組。</summary>
        private static SchemaItem Group(params SchemaItem[] members)
        {
            return new SchemaItem(
                null, members, null, "value", ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
        }

        /// <summary>作った要素をハンドルで渡して並びへ加えるツール。</summary>
        private static ToolSchema Held(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new[] { Item("number", "handles") },
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

        private static SchemaItem Item(string shape, string name)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, true, null, false, null, null,
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
