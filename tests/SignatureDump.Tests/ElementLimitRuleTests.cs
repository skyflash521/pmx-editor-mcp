using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ElementLimitRuleTests
    {
        /// <summary>題材の表。実物の値は共通契約の正本が持つ。</summary>
        private static readonly AssumedLength Lengths = new AssumedLength(
            new Dictionary<string, int>(StringComparer.Ordinal) { { "number", 11 } });

        /// <summary>要求サイズ予算。題材では構造トークンの側が小さくなる大きさを採る。</summary>
        private const int Budget = 8000000;

        private static SchemaBranch Branch(SchemaItem[] inputs, params SchemaChoice[] choices)
        {
            return new SchemaBranch("only", null, null, inputs, choices);
        }

        /// <summary>並びの項目。一次資料が要素数を定めていなければ要素数は null。</summary>
        private static SchemaItem Sequence(string name, SchemaItem element, int? maxItems)
        {
            return new SchemaItem(
                null, null, element, name, ItemOrigin.HostInput, true, null, false,
                null, null, maxItems.HasValue ? "一次資料" : null, false, maxItems);
        }

        private static SchemaItem Group(string name, params SchemaItem[] members)
        {
            return new SchemaItem(
                null, members, null, name, ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Value(string name, string shape)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, true, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Number()
        {
            return Value(null, "number");
        }

        /// <summary>ホストが自分で入れる引数。呼び出す側は送らない。</summary>
        private static SchemaItem Injected(string name)
        {
            return new SchemaItem(
                null, null, null, name, ItemOrigin.HostInput, true, null, false,
                null, null, null, true, null);
        }

        [Fact]
        public void TheRequestTakesTheSmallerOfTheBudgetAndTheStructureTokens()
        {
            SchemaItem targets = Sequence("targets", Number(), null);
            const int envelope = 5;

            IDictionary<SchemaItem, int> byTokens = ElementLimitRule.Request(
                Branch(new[] { targets }), Lengths, Budget, envelope + 1000);
            IDictionary<SchemaItem, int> byBudget = ElementLimitRule.Request(
                Branch(new[] { targets }), Lengths, Budget, envelope + 200000);

            Assert.Equal(1000, byTokens[targets]);
            Assert.Equal(Budget / (11 * 4), byBudget[targets]);
        }

        [Fact]
        public void ABudgetSmallerThanOneElementStaysAtOne()
        {
            SchemaItem targets = Sequence("targets", Number(), null);
            const int envelope = 5;

            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { targets }), Lengths, 10, envelope + 1000);

            Assert.Equal(1, limits[targets]);
        }

        [Fact]
        public void TheOuterSequenceIsDerivedBeforeTheInnerOne()
        {
            SchemaItem cells = Sequence("cells", Number(), null);
            SchemaItem rows = Sequence("rows", Group(null, cells), null);
            const int envelope = 5;

            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { rows }), Lengths, Budget, envelope + 20000);

            Assert.Equal(5000, limits[rows]);
            Assert.Equal(2, limits[cells]);
        }

        [Fact]
        public void ASequenceInsideACountedSequenceCarriesItsCount()
        {
            SchemaItem inner = Sequence(null, Number(), null);
            SchemaItem frames = Sequence("frames", inner, 4);
            const int envelope = 9;

            IDictionary<SchemaItem, int> byTokens = ElementLimitRule.Request(
                Branch(new[] { frames }), Lengths, Budget, envelope + 40000);
            IDictionary<SchemaItem, int> byBudget = ElementLimitRule.Request(
                Branch(new[] { frames }), Lengths, 3520, envelope + 40000);

            Assert.Equal(new[] { inner }, byTokens.Keys);
            Assert.Equal(40000 / 4, byTokens[inner]);
            Assert.Equal(3520 / (4 * 11 * 4), byBudget[inner]);
        }

        [Fact]
        public void TheSequencesInTheDistributedGroupHaveNoLimit()
        {
            SchemaItem values = Sequence("values", Number(), null);
            SchemaItem targets = Sequence("targets", Number(), null);
            const int envelope = 7;

            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { Group(ElementLimitRule.DistributedName, values), targets }),
                Lengths,
                Budget,
                envelope + 1000);

            Assert.Equal(new[] { targets }, limits.Keys);
            Assert.Equal(1000, limits[targets]);
        }

        [Fact]
        public void ASequenceInAChoiceIsCountedOnce()
        {
            SchemaItem handles = Sequence("handles", Number(), null);
            SchemaItem indices = Sequence("indices", Number(), null);
            SchemaItem[] inputs = new[] { handles, indices };
            SchemaChoice choice = new SchemaChoice(new[] { "handles", "indices" }, true);
            const int envelope = 6;

            IDictionary<SchemaItem, int> chosen = ElementLimitRule.Request(
                Branch(inputs, choice), Lengths, Budget, envelope + 1000);
            IDictionary<SchemaItem, int> both = ElementLimitRule.Request(
                Branch(inputs), Lengths, Budget, envelope + 1000);

            Assert.Equal(1000, chosen[handles]);
            Assert.Equal(500, both[handles]);
        }

        [Fact]
        public void ASequenceWithACountFromTheSourceIsCountedInTheEnvelope()
        {
            SchemaItem color = Sequence("color", Number(), 4);
            SchemaItem targets = Sequence("targets", Number(), null);
            const int envelope = 10;

            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { color, targets }), Lengths, Budget, envelope + 1000);

            Assert.Equal(new[] { targets }, limits.Keys);
            Assert.Equal(1000, limits[targets]);
        }

        [Fact]
        public void TheFixedMembersAreCountedInTheEnvelope()
        {
            SchemaItem targets = Sequence("targets", Number(), null);
            SchemaItem range = Group("range", Value("from", "number"), Value("to", "number"));
            const int envelope = 10;

            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { range, Group("mark"), targets }),
                Lengths,
                Budget,
                envelope + 1000);

            Assert.Equal(1000, limits[targets]);
        }

        [Fact]
        public void TheInputsTheHostFillsInAreNotCountedInTheEnvelope()
        {
            SchemaItem targets = Sequence("targets", Number(), null);
            SchemaItem connector = Injected("connector");
            const int envelope = 10;

            IDictionary<SchemaItem, int> withInjected = ElementLimitRule.Request(
                Branch(new[] { connector, targets }), Lengths, Budget, envelope + 1000);
            IDictionary<SchemaItem, int> withoutInjected = ElementLimitRule.Request(
                Branch(new[] { targets }), Lengths, Budget, envelope + 1000);

            Assert.Equal(withoutInjected[targets], withInjected[targets]);
        }

        [Fact]
        public void ABranchWithoutASequenceHasNoLimit()
        {
            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                Branch(new[] { Value("name", "number") }), Lengths, Budget, 1005);

            Assert.Empty(limits);
        }

        [Fact]
        public void StructureTokensThatDoNotCoverTheEnvelopeStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ElementLimitRule.Request(
                    Branch(new[] { Sequence("targets", Number(), null) }), Lengths, Budget, 5));

            Assert.Contains("構造トークンの残りが並びに足りない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnElementWithoutAnAssumedLengthStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ElementLimitRule.Request(
                    Branch(new[] { Sequence("targets", Group(null), null) }),
                    Lengths,
                    Budget,
                    1005));

            Assert.Contains("想定文字数が0の並び", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheResponseTakesTheValueRoomAndStaysAtOne()
        {
            Assert.Equal(98000 / 12, ElementLimitRule.Response(Number(), Lengths, 98000));
            Assert.Equal(1, ElementLimitRule.Response(Number(), Lengths, 5));
        }

        [Fact]
        public void TheBoundedCountTakesTheSmallerOfTheTwoSides()
        {
            Assert.Equal(7, ElementLimitRule.Bounded(9, 7));
            Assert.Equal(5, ElementLimitRule.Bounded(5, 7));
        }

        [Fact]
        public void TheIssuedCountTakesTheSmallestOfTheDerivedLimits()
        {
            Assert.Equal(5, ElementLimitRule.Issued(new[] { 5, 9 }, 7));
            Assert.Equal(7, ElementLimitRule.Issued(new int[0], 7));
        }

        [Fact]
        public void TheBranchAndTheTableAreRequired()
        {
            SchemaBranch branch = Branch(new[] { Sequence("targets", Number(), null) });

            Assert.Throws<ArgumentNullException>(
                () => ElementLimitRule.Request(null, Lengths, Budget, 1005));
            Assert.Throws<ArgumentNullException>(
                () => ElementLimitRule.Request(branch, null, Budget, 1005));
            Assert.Throws<ArgumentNullException>(
                () => ElementLimitRule.Response(null, Lengths, 98000));
            Assert.Throws<ArgumentNullException>(
                () => ElementLimitRule.Response(Number(), null, 98000));
            Assert.Throws<ArgumentNullException>(() => ElementLimitRule.Issued(null, 7));
        }
    }
}
