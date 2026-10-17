using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class PerTargetInputTests
    {
        private static readonly object One = new Dictionary<string, object> { { "name", "a" } };

        private static readonly object Two = new Dictionary<string, object> { { "name", "b" } };

        [Fact]
        public void TheSharedGroupGoesToEveryTarget()
        {
            ResolvedPerTargetInput taken = Resolve(One, null, 3);

            Assert.Equal(PerTargetForm.Shared, taken.Form);
            Assert.Same(One, taken.For(0));
            Assert.Same(One, taken.For(2));
        }

        [Fact]
        public void TheListGoesToTheTargetsInTheSameOrder()
        {
            ResolvedPerTargetInput taken = Resolve(null, new[] { One, Two }, 2);

            Assert.Equal(PerTargetForm.PerTarget, taken.Form);
            Assert.Same(One, taken.For(0));
            Assert.Same(Two, taken.For(1));
        }

        [Fact]
        public void HavingBothIsRefused()
        {
            Refused(One, new[] { One }, 1, "同時に持てない");
        }

        [Fact]
        public void HavingNeitherIsRefused()
        {
            Refused(null, null, 1, "どちらも無い");
        }

        [Fact]
        public void AListOfADifferentLengthFromTheTargetsIsRefused()
        {
            Refused(null, new[] { One }, 2, "長さが対象の件数と違う");
        }

        [Fact]
        public void AnEmptyListIsRightWhenThereIsNoTarget()
        {
            ResolvedPerTargetInput taken = Resolve(null, new object[0], 0);

            Assert.Equal(PerTargetForm.PerTarget, taken.Form);
            Assert.Empty(taken.PerTarget);
        }

        [Fact]
        public void TheSharedGroupIsTakenEvenWhenThereIsNoTarget()
        {
            Assert.Equal(PerTargetForm.Shared, Resolve(One, null, 0).Form);
        }

        [Fact]
        public void AToolThatTakesNoArgumentTakesNeither()
        {
            ResolvedPerTargetInput taken = Resolve(null, null, 2, takesInput: false);

            Assert.Equal(PerTargetForm.None, taken.Form);
            Assert.Throws<InvalidOperationException>(() => taken.For(0));
        }

        [Fact]
        public void GivingAGroupToAToolThatTakesNoArgumentIsRefused()
        {
            Refused(One, null, 2, "も受け取らない", takesInput: false);
            Refused(null, new object[] { One, Two }, 2, "も受け取らない", takesInput: false);
        }

        [Fact]
        public void TheNamesOfTheTwoItemsAppearInTheRefusal()
        {
            Refused(One, new[] { One }, 1, "args");
            Refused(One, new[] { One }, 1, "argsList");
        }

        [Fact]
        public void AskingBeyondTheListIsAProgrammingError()
        {
            ResolvedPerTargetInput taken = Resolve(null, new[] { One }, 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => taken.For(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => taken.For(-1));
        }

        [Fact]
        public void TheNamesAndTheTargetCountAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => PerTargetInput.TryResolve(
                One, null, 1, null, true, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerTargetInput.TryResolve(
                One, null, -1, PerTargetInput.Args, true, out _, out _, out _));
        }

        [Fact]
        public void BothPairsOfNamesAreTheOnesTheContractDefines()
        {
            Assert.Equal("value", PerTargetInput.Values.Shared);
            Assert.Equal("values", PerTargetInput.Values.PerTarget);
            Assert.Equal("args", PerTargetInput.Args.Shared);
            Assert.Equal("argsList", PerTargetInput.Args.PerTarget);
        }

        private static ResolvedPerTargetInput Resolve(
            object shared, IList<object> perTarget, int targetCount, bool takesInput = true)
        {
            Assert.True(PerTargetInput.TryResolve(
                shared,
                perTarget,
                targetCount,
                PerTargetInput.Args,
                takesInput,
                out ResolvedPerTargetInput taken,
                out string code,
                out string message));
            Assert.Null(code);
            Assert.Null(message);

            return taken;
        }

        private static void Refused(
            object shared,
            IList<object> perTarget,
            int targetCount,
            string expected,
            bool takesInput = true)
        {
            Assert.False(PerTargetInput.TryResolve(
                shared,
                perTarget,
                targetCount,
                PerTargetInput.Args,
                takesInput,
                out ResolvedPerTargetInput taken,
                out string code,
                out string message));
            Assert.Null(taken);
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains(expected, message, StringComparison.Ordinal);
        }
    }
}
