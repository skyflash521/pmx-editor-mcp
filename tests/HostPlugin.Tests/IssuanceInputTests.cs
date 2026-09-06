using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class IssuanceInputTests
    {
        private static readonly object Group = new Dictionary<string, object> { { "name", "a" } };

        [Fact]
        public void AConstructorWithNoArgumentTakesOnlyTheCount()
        {
            ResolvedIssuance taken = Resolve(
                IssuanceKind.Constructor, count: 3, takesArgs: false);

            Assert.Equal(3, taken.Count);
            Assert.Equal(PerTargetForm.None, taken.Args.Form);
        }

        [Fact]
        public void AConstructorWithNoArgumentRefusesAGroupOfArguments()
        {
            Refused(IssuanceKind.Constructor, count: 3, args: Group, takesArgs: false, expected: "受け取らない");
        }

        [Fact]
        public void TheSharedArgumentsGoWithACount()
        {
            ResolvedIssuance taken = Resolve(IssuanceKind.Factory, count: 2, args: Group);

            Assert.Equal(2, taken.Count);
            Assert.Equal(PerTargetForm.Shared, taken.Args.Form);
        }

        /// <summary>並びの長さが発行する数になるので、発行する数を別に受け取らない。</summary>
        [Fact]
        public void TheLengthOfTheListIsTheNumberToIssue()
        {
            ResolvedIssuance taken = Resolve(
                IssuanceKind.Factory, argsList: new[] { Group, Group });

            Assert.Equal(2, taken.Count);
            Assert.Equal(PerTargetForm.PerTarget, taken.Args.Form);
        }

        [Fact]
        public void TheCountAndTheListCannotBeHeldTogether()
        {
            Refused(
                IssuanceKind.Factory,
                count: 2,
                argsList: new[] { Group, Group },
                expected: "同時に持てない");
        }

        [Fact]
        public void HavingNeitherWayOfGivingArgumentsIsRefused()
        {
            Refused(IssuanceKind.Factory, count: 2, expected: "どちらも無い");
        }

        [Fact]
        public void HavingNoCountAtAllIsRefused()
        {
            Refused(IssuanceKind.Constructor, takesArgs: false, expected: "count が無い");
        }

        [Fact]
        public void ACountBelowOneIsRefused()
        {
            Refused(IssuanceKind.Constructor, count: 0, takesArgs: false, expected: "1を下回っている");
        }

        [Fact]
        public void AnEmptyListOfArgumentsIsRefused()
        {
            Refused(IssuanceKind.Factory, argsList: new object[0], expected: "空である");
        }

        /// <summary>受け手に紐づくメソッドは、受け手1件につき1個を発行する。</summary>
        [Fact]
        public void TheReceiverBoundMethodIssuesOnePerReceiver()
        {
            ResolvedIssuance taken = Resolve(
                IssuanceKind.ReceiverBound, receiverCount: 3, args: Group);

            Assert.Equal(3, taken.Count);
            Assert.Equal(PerTargetForm.Shared, taken.Args.Form);
        }

        [Fact]
        public void TheReceiverBoundMethodRefusesACount()
        {
            Refused(
                IssuanceKind.ReceiverBound,
                count: 2,
                receiverCount: 2,
                args: Group,
                expected: "受け手に紐づくメソッドでは持てない");
        }

        [Fact]
        public void TheReceiverBoundMethodTakesAListAsLongAsTheReceivers()
        {
            Assert.Equal(
                2,
                Resolve(
                    IssuanceKind.ReceiverBound,
                    receiverCount: 2,
                    argsList: new[] { Group, Group }).Count);
            Refused(
                IssuanceKind.ReceiverBound,
                receiverCount: 2,
                argsList: new[] { Group },
                expected: "長さが対象の件数と違う");
        }

        [Fact]
        public void TheReceiverBoundMethodWithNoArgumentTakesNeither()
        {
            ResolvedIssuance taken = Resolve(
                IssuanceKind.ReceiverBound, receiverCount: 2, takesArgs: false);

            Assert.Equal(2, taken.Count);
            Assert.Equal(PerTargetForm.None, taken.Args.Form);
        }

        [Fact]
        public void MoreThanTheLimitIsRefused()
        {
            Assert.Equal(4, Resolve(IssuanceKind.Constructor, count: 4, takesArgs: false, countLimit: 4).Count);
            Refused(
                IssuanceKind.Constructor,
                count: 5,
                takesArgs: false,
                countLimit: 4,
                expected: "上限を超えている");
        }

        [Fact]
        public void TheLimitIsAlsoOnTheLengthOfTheList()
        {
            Refused(
                IssuanceKind.Factory,
                argsList: new[] { Group, Group },
                countLimit: 1,
                expected: "上限を超えている");
        }

        [Fact]
        public void TheNameOfTheCountIsTheOneTheContractDefines()
        {
            Assert.Equal("count", IssuanceInput.CountName);
        }

        [Fact]
        public void TheCountsAreRequired()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IssuanceInput.TryResolve(
                IssuanceKind.ReceiverBound, null, null, null, -1, false, 8, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => IssuanceInput.TryResolve(
                IssuanceKind.Constructor, 1, null, null, 0, false, 0, out _, out _, out _));
        }

        private static ResolvedIssuance Resolve(
            IssuanceKind kind,
            int? count = null,
            object args = null,
            IList<object> argsList = null,
            int receiverCount = 0,
            bool takesArgs = true,
            int countLimit = 8)
        {
            Assert.True(IssuanceInput.TryResolve(
                kind,
                count,
                args,
                argsList,
                receiverCount,
                takesArgs,
                countLimit,
                out ResolvedIssuance taken,
                out string code,
                out string message));
            Assert.Null(code);
            Assert.Null(message);

            return taken;
        }

        private static void Refused(
            IssuanceKind kind,
            int? count = null,
            object args = null,
            IList<object> argsList = null,
            int receiverCount = 0,
            bool takesArgs = true,
            int countLimit = 8,
            string expected = null)
        {
            Assert.False(IssuanceInput.TryResolve(
                kind,
                count,
                args,
                argsList,
                receiverCount,
                takesArgs,
                countLimit,
                out ResolvedIssuance taken,
                out string code,
                out string message));
            Assert.Null(taken);
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains(expected, message, StringComparison.Ordinal);
        }
    }
}
