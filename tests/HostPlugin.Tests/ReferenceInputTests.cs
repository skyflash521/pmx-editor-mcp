using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ReferenceInputTests
    {
        [Fact]
        public void ThePositionsAreTakenInTheOrderTheyWereWritten()
        {
            Assert.Equal(new[] { 2, 0, 1 }, Resolve(new[] { 2, 0, 1 }, 3));
        }

        /// <summary>
        /// 参照は対象ではなく値なので、同じ位置を二度並べても同じ要素を二度指す指定にはならない。
        /// </summary>
        [Fact]
        public void TheSamePositionCanBeWrittenTwice()
        {
            Assert.Equal(new[] { 1, 1 }, Resolve(new[] { 1, 1 }, 3));
        }

        [Fact]
        public void AnEmptyListIsRefused()
        {
            Refused(new int[0], 3, ToolEnvelope.InvalidArgument, "空である");
        }

        [Fact]
        public void HavingNoListAtAllIsRefused()
        {
            Refused(null, 3, ToolEnvelope.InvalidArgument, "が無い");
        }

        [Fact]
        public void APositionOutsideTheReferencedListIsRefused()
        {
            Refused(new[] { 0, 3 }, 3, ToolEnvelope.IndexOutOfRange, "範囲の外");
            Refused(new[] { -1 }, 3, ToolEnvelope.IndexOutOfRange, "範囲の外");
        }

        [Fact]
        public void ThePositionIsCheckedAgainstTheReferencedListNotTheOwningList()
        {
            Refused(new[] { 2 }, 2, ToolEnvelope.IndexOutOfRange, "参照先のリストの件数は 2");
        }

        [Fact]
        public void TheNameOfTheItemIsTheOneTheContractDefines()
        {
            Assert.Equal("refIndices", ReferenceInput.Name);
        }

        [Fact]
        public void ANegativeReferencedCountIsAProgrammingError()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ReferenceInput.TryResolve(new[] { 0 }, -1, out _, out _, out _));
        }

        private static IList<int> Resolve(IList<int> refIndices, int referencedCount)
        {
            Assert.True(ReferenceInput.TryResolve(
                refIndices, referencedCount, out IList<int> resolved, out string code, out string message));
            Assert.Null(code);
            Assert.Null(message);

            return resolved;
        }

        private static void Refused(
            IList<int> refIndices, int referencedCount, string expectedCode, string expected)
        {
            Assert.False(ReferenceInput.TryResolve(
                refIndices, referencedCount, out IList<int> resolved, out string code, out string message));
            Assert.Null(resolved);
            Assert.Equal(expectedCode, code);
            Assert.Contains(expected, message, StringComparison.Ordinal);
        }
    }
}
