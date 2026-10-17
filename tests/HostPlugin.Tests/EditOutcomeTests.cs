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
        public void AStateThatIsNotKnownHasNoWording()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EditOutcome.Describe((EditState)99));
        }

        [Fact]
        public void OnlyTheUnknownResultAsksForAReadBack()
        {
            Assert.DoesNotContain("読み戻", EditOutcome.Describe(EditState.Unchanged));
            Assert.Contains("読み戻", EditOutcome.Describe(EditState.Unknown));
            Assert.DoesNotContain("読み戻", EditOutcome.Describe(EditState.Changed));
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
