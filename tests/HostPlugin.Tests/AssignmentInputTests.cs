using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class AssignmentInputTests
    {
        private static readonly Func<long, bool> Usable = handle => handle >= 100 && handle < 200;

        private const TargetForm BothWays = TargetForm.Indices | TargetForm.Handles;

        [Fact]
        public void TheGroupsAreTakenInTheOrderTheyWereWritten()
        {
            ResolvedAssignments taken = Resolve(
                new[]
                {
                    ByIndex(1, 100, 101),
                    ByIndex(0, 102),
                });

            Assert.Equal(TargetForm.Indices, taken.ParentForm);
            Assert.Equal(
                new[] { 1, 0 },
                new[] { taken.Groups[0].ParentIndex.Value, taken.Groups[1].ParentIndex.Value });
            Assert.Equal(3, taken.ChildCount);
        }

        [Fact]
        public void ParentsCanBePointedAtByHandle()
        {
            ResolvedAssignments taken = Resolve(new[] { ByHandle(100, 110) });

            Assert.Equal(TargetForm.Handles, taken.ParentForm);
            Assert.Equal(1, taken.ChildCount);
        }

        /// <summary>親の型がハンドルを発行しないリストは、位置でしか親を指せない。</summary>
        [Fact]
        public void PointingAtAParentByHandleIsRefusedWhereTheListDoesNotAllowIt()
        {
            Refused(
                new[] { ByHandle(100, 110) },
                ToolEnvelope.InvalidArgument,
                "parentHandle はこのツールでは指定できない",
                parentAllowed: TargetForm.Indices);
        }

        [Fact]
        public void MixingTheTwoWaysOfPointingAtParentsIsRefused()
        {
            Refused(
                new[] { ByIndex(0, 100), ByHandle(110, 101) },
                ToolEnvelope.InvalidArgument,
                "親の指し方を混ぜている");
        }

        [Fact]
        public void AGroupThatPointsAtNoParentIsRefused()
        {
            Refused(
                new[] { new AssignmentGroup(handles: new long[] { 100 }) },
                ToolEnvelope.InvalidArgument,
                "親を指していない");
        }

        [Fact]
        public void AGroupThatPointsAtTwoParentsIsRefused()
        {
            Refused(
                new[] { new AssignmentGroup(0, 110, new long[] { 100 }) },
                ToolEnvelope.InvalidArgument,
                "二重に指している");
        }

        [Fact]
        public void WritingTheSameParentTwiceIsRefused()
        {
            Refused(
                new[] { ByIndex(0, 100), ByIndex(0, 101) },
                ToolEnvelope.InvalidArgument,
                "同じ親を二度以上");
        }

        /// <summary>指し方そのものの誤りは、位置が範囲の中にあるかを見る前に断る。</summary>
        [Fact]
        public void ARepeatedParentIsRefusedBeforeTheRangeIsChecked()
        {
            Refused(
                new[] { ByIndex(9, 100), ByIndex(9, 101) },
                ToolEnvelope.InvalidArgument,
                "同じ親を二度以上");
        }

        [Fact]
        public void ARepeatedHandleIsRefusedBeforeTheHandleIsChecked()
        {
            Refused(
                new[] { ByIndex(0, 9), ByIndex(1, 9) },
                ToolEnvelope.InvalidArgument,
                "同じハンドルを二度以上");
        }

        [Fact]
        public void AParentOutsideTheListIsRefused()
        {
            Refused(new[] { ByIndex(9, 100) }, ToolEnvelope.IndexOutOfRange, "範囲の外");
        }

        [Fact]
        public void AParentHandleThatCannotBeUsedIsRefused()
        {
            Refused(new[] { ByHandle(9, 100) }, ToolEnvelope.InvalidHandle, "parentHandle");
        }

        /// <summary>
        /// 組ごとに見るだけでは、先の組で消費したハンドルを後の組が使う要求が通ってしまう。
        /// </summary>
        [Fact]
        public void AHandleThatAppearsInTwoGroupsIsRefused()
        {
            Refused(
                new[] { ByIndex(0, 100), ByIndex(1, 100) },
                ToolEnvelope.InvalidArgument,
                "同じハンドルを二度以上");
        }

        [Fact]
        public void AChildHandleThatCannotBeUsedIsRefused()
        {
            Refused(new[] { ByIndex(0, 9) }, ToolEnvelope.InvalidHandle, "使えないハンドル");
        }

        [Fact]
        public void AnEmptyRequestIsRefused()
        {
            Refused(new AssignmentGroup[0], ToolEnvelope.InvalidArgument, "空である");
            Refused(null, ToolEnvelope.InvalidArgument, "空である");
        }

        [Fact]
        public void AGroupWithNoChildIsRefused()
        {
            Refused(
                new[] { new AssignmentGroup(0, handles: new long[0]) },
                ToolEnvelope.InvalidArgument,
                "子を1つも持たない");
        }

        [Fact]
        public void AListThatOnlyPointsTakesReferencePositionsInstead()
        {
            ResolvedAssignments taken = Resolve(
                new[] { new AssignmentGroup(0, refIndices: new[] { 1, 2 }) },
                AssignmentChild.RefIndices);

            Assert.Equal(2, taken.ChildCount);
        }

        /// <summary>参照先の位置も、加える前に参照先のリストの中にあることを見る。</summary>
        [Fact]
        public void AReferencePositionOutsideTheReferencedListIsRefused()
        {
            Refused(
                new[] { new AssignmentGroup(0, refIndices: new[] { 0, 5 }) },
                ToolEnvelope.IndexOutOfRange,
                "参照先のリストの件数は 4",
                AssignmentChild.RefIndices);
        }

        /// <summary>参照は対象ではなく値なので、同じ位置を複数の組へ書いてよい。</summary>
        [Fact]
        public void TheSameReferencePositionCanAppearInTwoGroups()
        {
            Assert.Equal(
                2,
                Resolve(
                    new[]
                    {
                        new AssignmentGroup(0, refIndices: new[] { 1 }),
                        new AssignmentGroup(1, refIndices: new[] { 1 }),
                    },
                    AssignmentChild.RefIndices).ChildCount);
        }

        [Fact]
        public void GivingHandlesToAListThatOnlyPointsIsRefused()
        {
            Refused(
                new[] { ByIndex(0, 100) },
                ToolEnvelope.InvalidArgument,
                "区分と合わない",
                AssignmentChild.RefIndices);
        }

        [Fact]
        public void GivingReferencePositionsToAListThatOwnsIsRefused()
        {
            Refused(
                new[] { new AssignmentGroup(0, refIndices: new[] { 1 }) },
                ToolEnvelope.InvalidArgument,
                "区分と合わない");
        }

        /// <summary>上限は組ごとではなく、全部の組の子の合計に掛かる。</summary>
        [Fact]
        public void TheLimitIsOnTheTotalOfEveryGroup()
        {
            Assert.Equal(
                3, Resolve(new[] { ByIndex(0, 100, 101), ByIndex(1, 102) }, childLimit: 3).ChildCount);
            Refused(
                new[] { ByIndex(0, 100, 101), ByIndex(1, 102) },
                ToolEnvelope.InvalidArgument,
                "子の合計が上限を超えている",
                childLimit: 2);
        }

        [Fact]
        public void TheNameOfTheItemIsTheOneTheContractDefines()
        {
            Assert.Equal("assignments", AssignmentInput.Name);
        }

        [Fact]
        public void TheCountsAndTheHandleTestAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => AssignmentInput.TryResolve(
                new[] { ByIndex(0, 100) }, AssignmentChild.Handles, BothWays, 3, null, 4, 8,
                out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentInput.TryResolve(
                new[] { ByIndex(0, 100) }, AssignmentChild.Handles, BothWays, -1, Usable, 4, 8,
                out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentInput.TryResolve(
                new[] { ByIndex(0, 100) }, AssignmentChild.Handles, BothWays, 3, Usable, -1, 8,
                out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentInput.TryResolve(
                new[] { ByIndex(0, 100) }, AssignmentChild.Handles, BothWays, 3, Usable, 4, 0,
                out _, out _, out _));
        }

        private static AssignmentGroup ByIndex(int parentIndex, params long[] handles)
        {
            return new AssignmentGroup(parentIndex, handles: handles);
        }

        private static AssignmentGroup ByHandle(long parentHandle, params long[] handles)
        {
            return new AssignmentGroup(parentHandle: parentHandle, handles: handles);
        }

        private static ResolvedAssignments Resolve(
            IList<AssignmentGroup> groups,
            AssignmentChild child = AssignmentChild.Handles,
            int childLimit = 8,
            TargetForm parentAllowed = BothWays)
        {
            Assert.True(AssignmentInput.TryResolve(
                groups,
                child,
                parentAllowed,
                3,
                Usable,
                4,
                childLimit,
                out ResolvedAssignments taken,
                out string code,
                out string message));
            Assert.Null(code);
            Assert.Null(message);

            return taken;
        }

        private static void Refused(
            IList<AssignmentGroup> groups,
            string expectedCode,
            string expected,
            AssignmentChild child = AssignmentChild.Handles,
            int childLimit = 8,
            TargetForm parentAllowed = BothWays)
        {
            Assert.False(AssignmentInput.TryResolve(
                groups,
                child,
                parentAllowed,
                3,
                Usable,
                4,
                childLimit,
                out ResolvedAssignments taken,
                out string code,
                out string message));
            Assert.Null(taken);
            Assert.Equal(expectedCode, code);
            Assert.Contains(expected, message, StringComparison.Ordinal);
        }
    }
}
