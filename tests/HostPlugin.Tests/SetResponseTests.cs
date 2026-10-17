using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class SetResponseTests
    {
        [Fact]
        public void TheUpdateAnswerCarriesOnlyTheCount()
        {
            IDictionary<string, object> answer = SetResponse.Updated(3);

            Assert.Equal(new[] { "updated" }, answer.Keys);
            Assert.Equal(3, answer["updated"]);
        }

        [Fact]
        public void TheRemoveAnswerCarriesOnlyTheCount()
        {
            IDictionary<string, object> answer = SetResponse.Removed(2);

            Assert.Equal(new[] { "removed" }, answer.Keys);
            Assert.Equal(2, answer["removed"]);
        }

        [Fact]
        public void TheAddAnswerCarriesTheCountAndThePositionsInTheOrderTheyWereAdded()
        {
            IDictionary<string, object> answer = SetResponse.Added(new[] { 5, 0, 3 });

            Assert.Equal(new[] { "added", "indices" }, answer.Keys);
            Assert.Equal(3, answer["added"]);
            Assert.Equal(new[] { 5, 0, 3 }, (int[])answer["indices"]);
        }

        [Fact]
        public void TheAnswerOfAMethodWithNoResultCarriesTheNumberOfCalls()
        {
            IDictionary<string, object> answer = SetResponse.Invoked(5);

            Assert.Equal(new[] { "invoked" }, answer.Keys);
            Assert.Equal(5, answer["invoked"]);
        }

        [Fact]
        public void TheTemplateAnswerCarriesTheNumberOfTargetsItWasAppliedTo()
        {
            IDictionary<string, object> answer = SetResponse.Applied(4);

            Assert.Equal(new[] { "applied" }, answer.Keys);
            Assert.Equal(4, answer["applied"]);
        }

        [Fact]
        public void EveryCountIsZeroWhenThereIsNoTarget()
        {
            Assert.Equal(0, SetResponse.Updated(0)["updated"]);
            Assert.Equal(0, SetResponse.Removed(0)["removed"]);
            Assert.Equal(0, SetResponse.Invoked(0)["invoked"]);
            Assert.Equal(0, SetResponse.Applied(0)["applied"]);
            Assert.Equal(0, SetResponse.Added(new int[0])["added"]);
            Assert.Empty((int[])SetResponse.Added(new int[0])["indices"]);
        }

        [Fact]
        public void TheAnswerOfAMethodWithAResultIsAsLongAsTheTargets()
        {
            Assert.Equal(new object[] { 1, 2 }, SetResponse.PerTarget(new object[] { 1, 2 }, 2));
            Assert.Empty(SetResponse.PerTarget(new object[0], 0));
        }

        [Fact]
        public void AnAnswerOfADifferentLengthFromTheTargetsIsAProgrammingError()
        {
            Assert.Throws<ArgumentException>(() => SetResponse.PerTarget(new object[] { 1 }, 2));
        }

        [Fact]
        public void ACountBelowZeroIsAProgrammingError()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SetResponse.Updated(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SetResponse.Removed(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SetResponse.Invoked(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SetResponse.Applied(-1));
        }

        [Fact]
        public void TheAnswersAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => SetResponse.Added(null));
            Assert.Throws<ArgumentNullException>(() => SetResponse.PerTarget(null, 0));
        }

        [Fact]
        public void TheNamesAreTheOnesTheContractDefines()
        {
            Assert.Equal("updated", SetResponse.UpdatedName);
            Assert.Equal("removed", SetResponse.RemovedName);
            Assert.Equal("added", SetResponse.AddedName);
            Assert.Equal("indices", SetResponse.IndicesName);
            Assert.Equal("invoked", SetResponse.InvokedName);
            Assert.Equal("applied", SetResponse.AppliedName);
        }
    }
}
