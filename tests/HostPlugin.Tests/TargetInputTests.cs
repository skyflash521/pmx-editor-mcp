using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>要求の項目から、要素の集合の指定を読み取るところ。</summary>
    public class TargetInputTests
    {

        [Fact]
        public void IndicesComeThroughInTheOrderTheyWereGiven()
        {
            TargetRequest request = Taken(
                Arguments(new KeyValuePair<string, object>("indices", new object[] { 2, 0, 1 })));

            Assert.Equal(new[] { 2, 0, 1 }, request.Indices);
            Assert.Null(request.RangeStart);
            Assert.Null(request.All);
        }

        [Fact]
        public void RangeComesThroughAsTheStartAndTheCount()
        {
            TargetRequest request = Taken(Arguments(
                new KeyValuePair<string, object>(
                    "range",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "start", 3 },
                        { "count", 2 },
                    })));

            Assert.Equal(3, request.RangeStart);
            Assert.Equal(2, request.RangeCount);
        }

        [Fact]
        public void AllComesThroughAsTheBooleanThatWasGiven()
        {
            TargetRequest request = Taken(
                Arguments(new KeyValuePair<string, object>("all", true)));

            Assert.True(request.All);
        }

        [Fact]
        public void TheScreenSelectionComesThroughAsTheBooleanThatWasGiven()
        {
            TargetRequest request = Taken(
                Arguments(new KeyValuePair<string, object>("selected", true)));

            Assert.True(request.Selected);
            Assert.Null(request.Indices);
            Assert.Null(request.All);
        }

        [Fact]
        public void AScreenSelectionThatIsNotABooleanIsRefused()
        {
            string code = Refused(
                Arguments(new KeyValuePair<string, object>("selected", 1)));

            Assert.Equal(ToolEnvelope.InvalidArgument, code);
        }

        [Fact]
        public void TheParentNamesReadTheParentSetAndLeaveTheElementSetAlone()
        {
            TargetRequest request = Taken(
                Arguments(
                    new KeyValuePair<string, object>("parentIndices", new object[] { 1 }),
                    new KeyValuePair<string, object>("indices", new object[] { 5 })),
                TargetNames.Parent);

            Assert.Equal(new[] { 1 }, request.Indices);
        }

        [Fact]
        public void HandlesAreNotReadWhenTheToolDoesNotTakeThem()
        {
            TargetRequest request = Taken(
                Arguments(new KeyValuePair<string, object>("handles", new object[] { 7 })));

            Assert.Null(request.Handles);
        }

        [Fact]
        public void HandlesAreReadWhenTheToolTakesThem()
        {
            TargetRequest request = Taken(
                Arguments(new KeyValuePair<string, object>("handles", new object[] { 7 })),
                TargetNames.Element,
                handles: true);

            Assert.Equal(new long[] { 7 }, request.Handles);
        }

        [Fact]
        public void AnIndexThatIsNotAWholeNumberIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(
                    new KeyValuePair<string, object>("indices", new object[] { 1.5 }))));
        }

        [Fact]
        public void IndicesThatAreNotAnArrayAreRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(new KeyValuePair<string, object>("indices", 1))));
        }

        [Fact]
        public void ARangeThatCarriesOnlyOneOfTheTwoMembersIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(
                    new KeyValuePair<string, object>(
                        "range",
                        new Dictionary<string, object>(StringComparer.Ordinal) { { "start", 1 } }))));
        }

        [Fact]
        public void AllThatIsNotABooleanIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(new KeyValuePair<string, object>("all", "yes"))));
        }

        [Fact]
        public void OnlyTheNamesTheToolTakesGoThrough()
        {
            string code;
            string message;

            Assert.True(TargetInput.TryOnlyKnown(
                Arguments(new KeyValuePair<string, object>("kind", "vertex")),
                new[] { "kind", "all" },
                out code,
                out message));
            Assert.Null(code);
        }

        [Fact]
        public void ANameTheToolDoesNotTakeIsRefusedAndNamed()
        {
            string code;
            string message;

            Assert.False(TargetInput.TryOnlyKnown(
                Arguments(new KeyValuePair<string, object>("知らない項目", 1)),
                new[] { "kind" },
                out code,
                out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("知らない項目", message);
        }

        [Fact]
        public void TheParametersAreRequired()
        {
            string code;
            string message;

            Assert.Throws<ArgumentNullException>(() => TargetInput.TryTake(
                null, TargetNames.Element, false, out TargetRequest _, out code, out message));
        }

        private static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] given)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in given)
            {
                arguments.Add(pair.Key, pair.Value);
            }

            return arguments;
        }

        private static TargetRequest Taken(
            IDictionary<string, object> arguments,
            TargetNames names = null,
            bool handles = false)
        {
            TargetRequest request;
            string code;
            string message;
            Assert.True(
                TargetInput.TryTake(
                    arguments,
                    names ?? TargetNames.Element,
                    handles,
                    out request,
                    out code,
                    out message),
                "読み取れなかった: " + message);

            return request;
        }

        private static string Refused(IDictionary<string, object> arguments)
        {
            TargetRequest request;
            string code;
            string message;
            Assert.False(TargetInput.TryTake(
                arguments, TargetNames.Element, false, out request, out code, out message));
            Assert.NotNull(message);

            return code;
        }
    }
}
