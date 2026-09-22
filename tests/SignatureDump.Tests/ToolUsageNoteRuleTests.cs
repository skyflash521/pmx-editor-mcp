using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolUsageNoteRuleTests
    {
        [Fact]
        public void AListingCarriesTheWayToReadToTheEnd()
        {
            string note = Note(true, "indices", "range", "all", "offset", "limit");

            Assert.Contains("all", note, StringComparison.Ordinal);
            Assert.Contains("nextOffset", note, StringComparison.Ordinal);
            Assert.Contains("offset", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingUnderAParentAlsoCarriesTheParent()
        {
            string note = Note(
                true, "parentIndices", "parentRange", "parentAll", "indices", "range", "all",
                "offset", "limit");

            Assert.Contains("parentAll", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingUnderAParentSaysThatPointingOnlyTheParentListsEverythingUnderIt()
        {
            string note = Note(
                true, "parentIndices", "parentRange", "parentAll", "indices", "range", "all",
                "offset", "limit");

            Assert.Contains("親だけを指せば、その下の要素をすべて並べる", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingWithNoParentDoesNotCarryTheParent()
        {
            Assert.DoesNotContain(
                "parentAll", Note(true, "indices", "range", "all", "offset", "limit"),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AToolThatTakesRangeCarriesThatRangeIsNotClamped()
        {
            string note = Note(false, "indices", "range", "all");

            Assert.Contains("range", note, StringComparison.Ordinal);
            Assert.DoesNotContain("nextOffset", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingThatTakesRangeCarriesBoth()
        {
            string note = Note(true, "indices", "range", "all", "offset", "limit");

            Assert.Contains("nextOffset", note, StringComparison.Ordinal);
            Assert.Contains("range", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingThatTakesNameContainsCarriesWhatItNarrows()
        {
            string note = Note(true, "indices", "range", "all", "offset", "limit", "nameContains");

            Assert.Contains("nameContains", note, StringComparison.Ordinal);
            Assert.Contains("name", note, StringComparison.Ordinal);
            Assert.Contains("total", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingWithoutNameContainsDoesNotCarryIt()
        {
            Assert.DoesNotContain(
                "nameContains", Note(true, "indices", "range", "all", "offset", "limit"),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AToolThatTakesNeitherCarriesNoNote()
        {
            Assert.Null(Note(false, "pmxHandle"));
        }

        [Fact]
        public void ATargetingToolThatTakesNoRangeAndListsNothingCarriesNoNote()
        {
            Assert.Null(Note(false, "handles"));
        }

        [Fact]
        public void AToolThatHandsBackHandlesSaysThatItMakesSomethingNew()
        {
            string note = ToolUsageNoteRule.Compose(Issuing());

            Assert.Contains("新しく作る", note, StringComparison.Ordinal);
            Assert.Contains("session_release_handle", note, StringComparison.Ordinal);
        }

        [Fact]
        public void AListingDoesNotSayThatItMakesSomethingNew()
        {
            Assert.DoesNotContain(
                "session_release_handle",
                Note(true, "indices", "range", "all", "offset", "limit"),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AnUpdateThatTakesValuesSaysHowTheyPairWithTheTargets()
        {
            string note = Note(false, "indices", "value", "values");

            Assert.Contains("values は指した対象の並びの順に1件ずつ当て", note, StringComparison.Ordinal);
            Assert.Contains("value は指した対象の全部へ同じ値を当てる", note, StringComparison.Ordinal);
        }

        [Fact]
        public void ACallThatTakesArgsListSaysHowItPairsWithTheTargets()
        {
            string note = Note(false, "handles", "args", "argsList");

            Assert.Contains("argsList は指した対象の並びの順に1件ずつ当て", note, StringComparison.Ordinal);
            Assert.Contains("args は指した対象の全部へ同じ引数を当てる", note, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolUsageNoteRule.Compose(null));
        }

        private static string Note(bool listing, params string[] inputs)
        {
            return ToolUsageNoteRule.Compose(Schema(listing, inputs));
        }

        private static ToolSchema Schema(bool listing, string[] inputs)
        {
            List<SchemaItem> taken = new List<SchemaItem>();
            foreach (string name in inputs)
            {
                taken.Add(Item(name, "number", null, null));
            }

            return new ToolSchema(
                "model_list_vertices",
                new[] { new SchemaBranch("only", null, null, taken, new SchemaChoice[0]) },
                listing ? Listed() : Item(null, "boolean", null, null),
                null);
        }

        private static ToolSchema Issuing()
        {
            return new ToolSchema(
                "model_material",
                new[]
                {
                    new SchemaBranch(
                        "plain",
                        null,
                        null,
                        new List<SchemaItem> { Item("count", "number", null, null) },
                        new SchemaChoice[0]),
                },
                Item(null, null, null, Item(null, "number", null, null)),
                null);
        }

        private static SchemaItem Listed()
        {
            return Item(
                null,
                null,
                new List<SchemaItem>
                {
                    Item("total", "number", null, null),
                    Item("items", null, null, Item(null, "number", null, null)),
                },
                null);
        }

        private static SchemaItem Item(
            string name, string shape, IList<SchemaItem> members, SchemaItem element)
        {
            return new SchemaItem(
                shape,
                members,
                element,
                name,
                ItemOrigin.HostInput,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }
    }
}
