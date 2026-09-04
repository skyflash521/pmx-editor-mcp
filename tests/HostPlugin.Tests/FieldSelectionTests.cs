using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class FieldSelectionTests
    {
        private static readonly string[] Readable = { "name", "position", "weight" };

        private static readonly string[] Always = { "parentIndex", "indexInParent" };

        private static readonly string[] Nothing = new string[0];

        [Fact]
        public void AskingForNothingTakesEveryReadableField()
        {
            Assert.Equal(Readable, Resolve(null));
        }

        [Fact]
        public void OnlyTheFieldsThatWereAskedForAreTaken()
        {
            Assert.Equal(new[] { "name", "weight" }, Resolve(new[] { "name", "weight" }));
        }

        [Fact]
        public void TheOrderOfTheAskingDoesNotChangeTheOrderOfTheAnswer()
        {
            Assert.Equal(new[] { "name", "weight" }, Resolve(new[] { "weight", "name" }));
        }

        [Fact]
        public void AskingForNoFieldAtAllIsRefused()
        {
            Refused(new string[0], "fields が項目を1つも選んでいない");
        }

        [Fact]
        public void AskingForTheSameFieldTwiceIsRefused()
        {
            Refused(new[] { "name", "name" }, "fields が項目 name を二度選んでいる");
        }

        [Fact]
        public void AskingForAFieldThatCannotBeReadIsRefused()
        {
            Refused(new[] { "name", "colour" }, "fields が選んだ colour は読み取れる項目に無い");
        }

        [Fact]
        public void WhatIsAlwaysReturnedComesFirstWhetherOrNotFieldsWereAskedFor()
        {
            Assert.Equal(
                new[] { "parentIndex", "indexInParent", "name", "position", "weight" },
                Resolve(null, Always));
            Assert.Equal(
                new[] { "parentIndex", "indexInParent", "name" },
                Resolve(new[] { "name" }, Always));
        }

        [Fact]
        public void AskingForWhatIsAlwaysReturnedIsRefused()
        {
            IList<string> selected;
            string code;
            string message;
            Assert.False(FieldSelection.TryResolve(
                new[] { "parentIndex" }, Readable, Always, out selected, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("parentIndex", message);
            Assert.Null(selected);
        }

        [Fact]
        public void AFieldThatIsBothReadableAndAlwaysReturnedStops()
        {
            IList<string> selected;
            string code;
            string message;
            Assert.Throws<ArgumentException>(() => FieldSelection.TryResolve(
                null, Readable, new[] { "name" }, out selected, out code, out message));
        }

        [Fact]
        public void NoListOfReadableFieldsStops()
        {
            IList<string> selected;
            string code;
            string message;
            Assert.Throws<ArgumentNullException>(() => FieldSelection.TryResolve(
                null, null, Nothing, out selected, out code, out message));
        }

        [Fact]
        public void NoListOfWhatIsAlwaysReturnedStops()
        {
            IList<string> selected;
            string code;
            string message;
            Assert.Throws<ArgumentNullException>(() => FieldSelection.TryResolve(
                null, Readable, null, out selected, out code, out message));
        }

        [Fact]
        public void TheChosenFieldsAreTakenFromTheElementInTheChosenOrder()
        {
            IDictionary<string, object> taken = FieldSelection.Take(
                Element(), new[] { "weight", "name" });
            Assert.Equal(new[] { "weight", "name" }, new List<string>(taken.Keys));
            Assert.Equal("頂点", taken["name"]);
            Assert.Equal(0.5d, taken["weight"]);
        }

        [Fact]
        public void AFieldTheElementDoesNotCarryStops()
        {
            Assert.Throws<ArgumentException>(() => FieldSelection.Take(
                Element(), new[] { "colour" }));
        }

        [Fact]
        public void NoElementAndNoChoiceStop()
        {
            Assert.Throws<ArgumentNullException>(() => FieldSelection.Take(null, Readable));
            Assert.Throws<ArgumentNullException>(() => FieldSelection.Take(Element(), null));
        }

        private static IDictionary<string, object> Element()
        {
            Dictionary<string, object> element = new Dictionary<string, object>(StringComparer.Ordinal);
            element["name"] = "頂点";
            element["position"] = new object[] { 0d, 0d, 0d };
            element["weight"] = 0.5d;

            return element;
        }

        private static IList<string> Resolve(IList<string> requested)
        {
            return Resolve(requested, Nothing);
        }

        private static IList<string> Resolve(IList<string> requested, IList<string> always)
        {
            IList<string> selected;
            string code;
            string message;
            Assert.True(FieldSelection.TryResolve(
                requested, Readable, always, out selected, out code, out message));
            Assert.Null(code);
            Assert.Null(message);

            return selected;
        }

        private static void Refused(IList<string> requested, string expected)
        {
            IList<string> selected;
            string code;
            string message;
            Assert.False(FieldSelection.TryResolve(
                requested, Readable, Nothing, out selected, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains(expected, message);
            Assert.Null(selected);
        }
    }
}
