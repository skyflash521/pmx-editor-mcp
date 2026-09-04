using System;
using System.Collections.Generic;
using System.Linq;
using PmxEditorMcp;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class TargetSelectionTests
    {
        private const TargetForm EveryForm =
            TargetForm.Indices | TargetForm.Range | TargetForm.All | TargetForm.Handles;

        [Fact]
        public void IndicesKeepTheOrderTheRequestGave()
        {
            ResolvedTargets resolved = Resolve(new TargetRequest(indices: new[] { 3, 0, 2 }), listCount: 5);

            Assert.Equal(TargetForm.Indices, resolved.Form);
            Assert.Equal(new[] { 3, 0, 2 }, resolved.Indices);
            Assert.Null(resolved.Handles);
        }

        [Fact]
        public void HandlesKeepTheOrderTheRequestGave()
        {
            ResolvedTargets resolved = Resolve(
                new TargetRequest(handles: new long[] { 7, 1, 4 }), listCount: 0);

            Assert.Equal(TargetForm.Handles, resolved.Form);
            Assert.Equal(new long[] { 7, 1, 4 }, resolved.Handles);
            Assert.Null(resolved.Indices);
        }

        [Fact]
        public void ARangeComesBackInAscendingPositions()
        {
            ResolvedTargets resolved = Resolve(
                new TargetRequest(rangeStart: 2, rangeCount: 3), listCount: 6);

            Assert.Equal(TargetForm.Range, resolved.Form);
            Assert.Equal(new[] { 2, 3, 4 }, resolved.Indices);
        }

        [Fact]
        public void AllComesBackInAscendingPositions()
        {
            ResolvedTargets resolved = Resolve(new TargetRequest(all: true), listCount: 4);

            Assert.Equal(TargetForm.All, resolved.Form);
            Assert.Equal(new[] { 0, 1, 2, 3 }, resolved.Indices);
        }

        [Fact]
        public void AllOverAnEmptyListResolvesToNoTargets()
        {
            ResolvedTargets resolved = Resolve(new TargetRequest(all: true), listCount: 0);

            Assert.Equal(0, resolved.Count);
        }

        [Fact]
        public void NoFormAtAllIsAnInvalidArgument()
        {
            Failure failure = Reject(new TargetRequest(), listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("対象の指定が無い", failure.Message);
        }

        [Fact]
        public void TwoFormsAtOnceIsAnInvalidArgument()
        {
            Failure failure = Reject(
                new TargetRequest(indices: new[] { 0 }, all: true), listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("indices", failure.Message);
            Assert.Contains("all", failure.Message);
        }

        [Fact]
        public void HalfARangeCountsAsTheRangeFormAndIsAnInvalidArgument()
        {
            Failure failure = Reject(new TargetRequest(rangeStart: 1), listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("start と count", failure.Message);
        }

        [Fact]
        public void AFormTheToolDoesNotAcceptIsAnInvalidArgument()
        {
            bool resolvedOk = TargetSelection.TryResolve(
                new TargetRequest(handles: new long[] { 1 }),
                TargetForm.Indices | TargetForm.Range | TargetForm.All,
                listCount: 3,
                isUsableHandle: h => true,
                resolved: out ResolvedTargets resolved,
                code: out string code,
                message: out string message,
                names: TargetNames.Element);

            Assert.False(resolvedOk);
            Assert.Null(resolved);
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("このツールでは指定できない", message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AnEmptyTargetArrayIsAnInvalidArgument(bool byHandles)
        {
            TargetRequest request = byHandles
                ? new TargetRequest(handles: new long[0])
                : new TargetRequest(indices: new int[0]);

            Failure failure = Reject(request, listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("空だと対象が決まらない", failure.Message);
        }

        [Fact]
        public void ARepeatedIndexIsAnInvalidArgument()
        {
            Failure failure = Reject(new TargetRequest(indices: new[] { 1, 2, 1 }), listCount: 5);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("二度指している", failure.Message);
        }

        [Fact]
        public void ARepeatedHandleIsAnInvalidArgument()
        {
            Failure failure = Reject(new TargetRequest(handles: new long[] { 4, 4 }), listCount: 0);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
            Assert.Contains("二度指している", failure.Message);
        }

        [Fact]
        public void ARepeatedIndexIsRejectedBeforeTheRangeIsChecked()
        {
            Failure failure = Reject(new TargetRequest(indices: new[] { 9, 9 }), listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void AnIndexOutsideTheListIsOutOfRange(int index)
        {
            Failure failure = Reject(new TargetRequest(indices: new[] { 0, index }), listCount: 3);

            Assert.Equal(ToolEnvelope.IndexOutOfRange, failure.Code);
        }

        [Fact]
        public void ARangeThatRunsPastTheListIsOutOfRange()
        {
            Failure failure = Reject(new TargetRequest(rangeStart: 2, rangeCount: 2), listCount: 3);

            Assert.Equal(ToolEnvelope.IndexOutOfRange, failure.Code);
        }

        [Fact]
        public void ARangeThatEndsExactlyAtTheListIsAccepted()
        {
            ResolvedTargets resolved = Resolve(
                new TargetRequest(rangeStart: 1, rangeCount: 2), listCount: 3);

            Assert.Equal(new[] { 1, 2 }, resolved.Indices);
        }

        [Fact]
        public void ARangeNearTheLargestIntegerDoesNotWrapIntoTheList()
        {
            Failure failure = Reject(
                new TargetRequest(rangeStart: int.MaxValue, rangeCount: 2), listCount: 3);

            Assert.Equal(ToolEnvelope.IndexOutOfRange, failure.Code);
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 0)]
        public void ARangeOutsideItsOwnBoundsIsAnInvalidArgument(int start, int count)
        {
            Failure failure = Reject(
                new TargetRequest(rangeStart: start, rangeCount: count), listCount: 5);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
        }

        [Fact]
        public void AllSetToFalseIsAnInvalidArgument()
        {
            Failure failure = Reject(new TargetRequest(all: false), listCount: 3);

            Assert.Equal(ToolEnvelope.InvalidArgument, failure.Code);
        }

        [Fact]
        public void AHandleTheLedgerCannotUseIsAnInvalidHandle()
        {
            bool resolvedOk = TargetSelection.TryResolve(
                new TargetRequest(handles: new long[] { 1, 2 }),
                EveryForm,
                listCount: 0,
                isUsableHandle: h => h != 2,
                resolved: out ResolvedTargets resolved,
                code: out string code,
                message: out string message,
                names: TargetNames.Element);

            Assert.False(resolvedOk);
            Assert.Null(resolved);
            Assert.Equal(ToolEnvelope.InvalidHandle, code);
            Assert.Contains("2", message);
        }

        [Fact]
        public void HandlesAreAskedAboutInTheOrderTheRequestGave()
        {
            List<long> asked = new List<long>();
            TargetSelection.TryResolve(
                new TargetRequest(handles: new long[] { 1, 2, 3 }),
                EveryForm,
                listCount: 0,
                isUsableHandle: h =>
                {
                    asked.Add(h);
                    return h != 3;
                },
                resolved: out ResolvedTargets _,
                code: out string _,
                message: out string _,
                names: TargetNames.Element);

            Assert.Equal(new long[] { 1, 2, 3 }, asked);
        }

        [Fact]
        public void PagingRunsOverTheResolvedSetRatherThanTheList()
        {
            ResolvedTargets resolved = Resolve(new TargetRequest(indices: new[] { 8, 6, 7 }), listCount: 10);

            Assert.True(Paging.TryTake(
                resolved.Indices.ToArray(),
                offset: 1,
                limit: 5,
                valueChars: int.MaxValue,
                measure: taken => 0,
                page: out Page<int> page));
            Assert.Equal(3, page.Total);
            Assert.Equal(new[] { 6, 7 }, page.Items);
        }

        [Fact]
        public void ANullRequestStops()
        {
            Assert.Throws<ArgumentNullException>(() => TargetSelection.TryResolve(
                null, EveryForm, 0, h => true,
                out ResolvedTargets _, out string _, out string _, TargetNames.Element));
        }

        [Fact]
        public void ANegativeListCountStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TargetSelection.TryResolve(
                new TargetRequest(all: true), EveryForm, -1, h => true,
                out ResolvedTargets _, out string _, out string _, TargetNames.Element));
        }

        [Fact]
        public void AnotherSetIsRefusedByItsOwnItemNames()
        {
            bool resolvedOk = TargetSelection.TryResolve(
                new TargetRequest(indices: new int[0]),
                EveryForm,
                listCount: 3,
                isUsableHandle: h => true,
                resolved: out ResolvedTargets resolved,
                code: out string code,
                message: out string message,
                names: TargetNames.Parent);

            Assert.False(resolvedOk);
            Assert.Null(resolved);
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.StartsWith("parentIndices ", message);
        }

        [Fact]
        public void AnotherSetNamesEveryFormItAcceptsByItsOwnNames()
        {
            TargetSelection.TryResolve(
                new TargetRequest(),
                EveryForm,
                listCount: 3,
                isUsableHandle: h => true,
                resolved: out ResolvedTargets _,
                code: out string _,
                message: out string message,
                names: TargetNames.Parent);

            Assert.Contains("parentRange", message);
            Assert.Contains("parentAll", message);
            Assert.Contains("parentHandles", message);
            Assert.DoesNotContain("・range", message);
        }

        [Fact]
        public void ResolvingWithoutItemNamesStops()
        {
            Assert.Throws<ArgumentNullException>(() => TargetSelection.TryResolve(
                new TargetRequest(all: true), EveryForm, 0, h => true,
                out ResolvedTargets _, out string _, out string _, null));
        }

        [Fact]
        public void ANamelessItemStops()
        {
            Assert.Throws<ArgumentException>(() => new TargetNames("indices", " ", "all", "handles"));
        }

        private static ResolvedTargets Resolve(TargetRequest request, int listCount)
        {
            bool resolvedOk = TargetSelection.TryResolve(
                request,
                EveryForm,
                listCount,
                isUsableHandle: h => true,
                resolved: out ResolvedTargets resolved,
                code: out string code,
                message: out string message,
                names: TargetNames.Element);

            Assert.True(resolvedOk, code + ": " + message);
            Assert.Null(code);
            Assert.Null(message);

            return resolved;
        }

        private static Failure Reject(TargetRequest request, int listCount)
        {
            bool resolvedOk = TargetSelection.TryResolve(
                request,
                EveryForm,
                listCount,
                isUsableHandle: h => true,
                resolved: out ResolvedTargets resolved,
                code: out string code,
                message: out string message,
                names: TargetNames.Element);

            Assert.False(resolvedOk);
            Assert.Null(resolved);
            Assert.Contains(code, ToolEnvelope.ErrorCodes);
            Assert.False(string.IsNullOrWhiteSpace(message));

            return new Failure(code, message);
        }

        private sealed class Failure
        {
            public Failure(string code, string message)
            {
                Code = code;
                Message = message;
            }

            public string Code { get; }

            public string Message { get; }
        }
    }
}
