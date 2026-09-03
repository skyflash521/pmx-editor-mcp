using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ResponseSizeTests
    {
        [Fact]
        public void TheValueTakesTheBudgetLessTheWarningRoom()
        {
            Assert.Equal(98000, ResponseSize.ValueChars(100000));
            Assert.Equal(8000, ResponseSize.ValueChars(10000));
        }

        [Fact]
        public void ABudgetUnderTheLowerBoundStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ResponseSize.ValueChars(ResponseBudget.MinimumChars - 1));
        }

        [Fact]
        public void TheWarningRoomIsTheContract()
        {
            Assert.Equal(2000, ResponseSize.WarningChars);
        }

        [Fact]
        public void WarningsThatFitAreKeptAsTheyAre()
        {
            string[] warnings = { "一つ目。", "二つ目。" };

            Assert.Equal(warnings, ResponseSize.Fit(warnings));
        }

        [Fact]
        public void TheLengthCountsWhatEachWarningTakesInTheBody()
        {
            Assert.Equal(0, ResponseSize.Length(new string[0]));
            Assert.Equal(3 + ResponseSize.LineOverheadChars, ResponseSize.Length(new[] { "abc" }));
            Assert.Equal(
                6 + (2 * ResponseSize.LineOverheadChars),
                ResponseSize.Length(new[] { "abc", "def" }));
        }

        [Fact]
        public void TheOverheadIsTheLineBreakAndThePrefix()
        {
            Assert.Equal(5, ResponseSize.LineOverheadChars);
        }

        [Fact]
        public void TheNoticeFitsInTheWarningRoom()
        {
            Assert.True(
                ResponseSize.Length(new[] { ResponseSize.TruncatedNotice })
                    <= ResponseSize.WarningChars,
                "注記だけで警告の枠を超えている。");
        }

        [Fact]
        public void WarningsOverTheRoomAreCutWithANotice()
        {
            IList<string> fitted = ResponseSize.Fit(Many(3, ResponseSize.WarningChars / 2));

            Assert.Equal(ResponseSize.TruncatedNotice, fitted.Last());
            Assert.True(
                ResponseSize.Length(fitted) <= ResponseSize.WarningChars,
                "切り詰めたのに枠へ収まっていない。");
        }

        [Fact]
        public void TheCutKeepsAsManyWarningsAsFitWithTheNotice()
        {
            IList<string> fitted = ResponseSize.Fit(Many(3, ResponseSize.WarningChars / 2));

            Assert.Equal(2, fitted.Count);
            Assert.Equal(new string('0', ResponseSize.WarningChars / 2), fitted[0]);
        }

        [Fact]
        public void OnlyTheNoticeIsLeftWhenNoWarningFitsWithIt()
        {
            IList<string> fitted = ResponseSize.Fit(
                new[] { new string('a', ResponseSize.WarningChars) });

            Assert.Equal(new[] { ResponseSize.TruncatedNotice }, fitted);
        }

        [Fact]
        public void TheValueAndTheFittedWarningsStayInTheBudget()
        {
            int budget = 10000;
            IList<string> fitted = ResponseSize.Fit(Many(5, ResponseSize.WarningChars));

            Assert.True(
                ResponseSize.ValueChars(budget) + ResponseSize.Length(fitted) <= budget,
                "値の枠と警告の枠の和が予算を超えている。");
        }

        [Fact]
        public void WarningsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ResponseSize.Fit(null));
            Assert.Throws<ArgumentNullException>(() => ResponseSize.Length(null));
            Assert.Throws<ArgumentException>(() => ResponseSize.Length(new string[] { null }));
            Assert.Throws<ArgumentException>(() => ResponseSize.Fit(new string[] { null }));
        }

        /// <summary>同じ長さの警告を、先頭の文字を変えて並べたもの。</summary>
        private static IList<string> Many(int count, int length)
        {
            return Enumerable.Range(0, count)
                .Select(i => new string((char)('0' + i), length))
                .ToList();
        }
    }
}
