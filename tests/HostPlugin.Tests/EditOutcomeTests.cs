using System;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class EditOutcomeTests
    {
        [Fact]
        public void FailingBeforeTheCommitLeavesTheStateUnchanged()
        {
            Assert.Equal(EditState.Unchanged, EditOutcome.Resolve(EditStage.BeforeCommit));
        }

        [Fact]
        public void FailingAtTheCommitLeavesTheStateUnknown()
        {
            Assert.Equal(EditState.Unknown, EditOutcome.Resolve(EditStage.AtCommit));
        }

        [Fact]
        public void FailingAfterTheCommitLeavesTheStateChanged()
        {
            Assert.Equal(EditState.Changed, EditOutcome.AfterDuplicateEditCommit());
        }

        [Fact]
        public void AStageThatIsNotKnownStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EditOutcome.Resolve((EditStage)99));
        }

        [Theory]
        [InlineData(EditState.Unchanged, true)]
        [InlineData(EditState.Unknown, true)]
        [InlineData(EditState.Changed, false)]
        public void OnlyTheChangedStateIsAnsweredAsSuccess(EditState state, bool expected)
        {
            Assert.Equal(expected, EditOutcome.IsFailure(state));
        }
    }
}
