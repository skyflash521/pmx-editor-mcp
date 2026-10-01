using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class PositionInputTests
    {
        private const string Name = "target";

        private const string Shape = "target は位置でなければならない。";

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void APositionInsideTheCountIsTaken(int at)
        {
            int taken;
            string code;
            string message;

            Assert.True(PositionInput.TryOne(at, Name, Shape, 3, out taken, out code, out message));
            Assert.Equal(at, taken);
            Assert.Null(code);
            Assert.Null(message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public void APositionOutsideTheCountIsOutOfRange(int at)
        {
            int taken;
            string code;
            string message;

            Assert.False(PositionInput.TryOne(at, Name, Shape, 3, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
            Assert.Contains(Name + " が並びの外を指している: " + at, message);
        }

        [Fact]
        public void AnyPositionIsOutOfRangeWhenThereIsNothingToPointAt()
        {
            string code;
            string message;

            Assert.False(PositionInput.TryWithin(0, 0, Name, out code, out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("1")]
        [InlineData(1.5)]
        [InlineData(true)]
        [InlineData(4294967296L)]
        public void AValueThatIsNotAPositionIsInvalid(object given)
        {
            int taken;
            string code;
            string message;

            Assert.False(PositionInput.TryOne(given, Name, Shape, 3, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Equal(Shape, message);
        }

        [Fact]
        public void TheCountIsNamedWhenTheCallerGivesItsName()
        {
            string code;
            string message;

            Assert.False(PositionInput.TryWithin(5, 3, Name, out code, out message, "参照先の件数"));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
            Assert.Equal(Name + " の位置が範囲の外にある: 5(参照先の件数は 3)", message);
        }

        [Fact]
        public void TheLastPositionTheCallerAllowsCanBeBeyondTheCount()
        {
            string code;
            string message;

            Assert.True(PositionInput.TryWithin(3, 4, Name, out code, out message));
            Assert.False(PositionInput.TryWithin(4, 4, Name, out code, out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
        }

        [Fact]
        public void AListOfPositionsInsideTheCountIsTakenInOrder()
        {
            List<int> taken;
            string code;
            string message;

            Assert.True(PositionInput.TryMany(
                new object[] { 2, 0, 2 }, Name, Shape, 3, true, out taken, out code, out message));
            Assert.Equal(new[] { 2, 0, 2 }, taken);
        }

        [Fact]
        public void AnEmptyListIsTakenOnlyWhenTheCallerAllowsIt()
        {
            List<int> taken;
            string code;
            string message;

            Assert.True(PositionInput.TryMany(
                new object[0], Name, Shape, 3, true, out taken, out code, out message));
            Assert.Empty(taken);
            Assert.False(PositionInput.TryMany(
                new object[0], Name, Shape, 3, false, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Equal(Shape, message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData("1")]
        public void ALackOfAListIsInvalid(object given)
        {
            List<int> taken;
            string code;
            string message;

            Assert.False(PositionInput.TryMany(given, Name, Shape, 3, true, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Equal(Shape, message);
            Assert.Empty(taken);
        }

        [Fact]
        public void AListWithAMemberThatIsNotAPositionIsInvalid()
        {
            List<int> taken;
            string code;
            string message;

            Assert.False(PositionInput.TryMany(
                new object[] { 0, "a" }, Name, Shape, 3, true, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Equal(Shape, message);
            Assert.Empty(taken);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void AListWithAMemberOutsideTheCountIsOutOfRange(int at)
        {
            List<int> taken;
            string code;
            string message;

            Assert.False(PositionInput.TryMany(
                new object[] { 0, at }, Name, Shape, 3, true, out taken, out code, out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
            Assert.Contains(Name + " が並びの外を指している: " + at, message);
            Assert.Empty(taken);
        }

        [Fact]
        public void AListIsOnlyCheckedAgainstZeroWhenTheCountIsUnbounded()
        {
            List<int> taken;
            string code;
            string message;

            Assert.True(PositionInput.TryMany(
                new object[] { 0, 2000000000 },
                Name,
                Shape,
                PositionInput.Unbounded,
                true,
                out taken,
                out code,
                out message));
            Assert.False(PositionInput.TryMany(
                new object[] { -1 },
                Name,
                Shape,
                PositionInput.Unbounded,
                true,
                out taken,
                out code,
                out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
        }

        [Theory]
        [InlineData(0, 1, 3, true)]
        [InlineData(0, 3, 3, true)]
        [InlineData(2, 1, 3, true)]
        [InlineData(2, 2, 3, false)]
        [InlineData(3, 1, 3, false)]
        [InlineData(0, 4, 3, false)]
        [InlineData(0, 1, 0, false)]
        [InlineData(int.MaxValue, 2, int.MaxValue, false)]
        public void ARangeThatReachesBeyondTheCountIsOutOfRange(int start, int count, int ceiling, bool inside)
        {
            string code;
            string message;

            Assert.Equal(inside, PositionInput.TryRange(start, count, ceiling, "range", out code, out message));
            if (!inside)
            {
                Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
                Assert.Contains("range", message);
            }
        }

        [Fact]
        public void ARangeThatStartsBelowZeroIsOutOfRange()
        {
            string code;
            string message;

            Assert.False(PositionInput.TryRange(-1, 1, 3, "range", out code, out message));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, code);
            Assert.Contains("start", message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ARangeThatTakesNoMemberIsInvalid(int count)
        {
            string code;
            string message;

            Assert.False(PositionInput.TryRange(0, count, 3, "range", out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("count", message);
        }
    }
}
