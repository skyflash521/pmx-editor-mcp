using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class PagingTests
    {
        private const int PerItem = 10;

        private const int NextOffsetChars = 15;

        [Fact]
        public void EverythingThatFitsIsReturned()
        {
            Page<int> page = Take(All(5), 0, 5, 1000);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, page.Items);
            Assert.Equal(5, page.Total);
            Assert.Null(page.NextOffset);
            Assert.Empty(page.Warnings);
        }

        [Fact]
        public void TheRestIsPointedToByTheNextOffset()
        {
            Page<int> page = Take(All(5), 1, 2, 1000);

            Assert.Equal(new[] { 1, 2 }, page.Items);
            Assert.Equal(5, page.Total);
            Assert.Equal(3, page.NextOffset);
        }

        [Fact]
        public void AnOffsetPastTheEndReturnsNothing()
        {
            Page<int> page = Take(All(3), 3, 10, 1000);

            Assert.Empty(page.Items);
            Assert.Equal(3, page.Total);
            Assert.Null(page.NextOffset);
            Assert.Empty(page.Warnings);
        }

        [Fact]
        public void AnOffsetFarPastTheEndReturnsNothing()
        {
            Page<int> page = Take(All(3), 99, 10, 1000);

            Assert.Empty(page.Items);
            Assert.Null(page.NextOffset);
        }

        [Fact]
        public void TheCountIsReducedUntilItFits()
        {
            Page<int> page = Take(All(10), 0, 10, (4 * PerItem) + 2);

            Assert.Equal(new[] { 0, 1, 2, 3 }, page.Items);
            Assert.Equal(10, page.Total);
            Assert.Equal(4, page.NextOffset);
        }

        [Fact]
        public void AReducedCountSaysBothNumbers()
        {
            Page<int> page = Take(All(10), 0, 10, (4 * PerItem) + 2);

            string warning = Assert.Single(page.Warnings);
            Assert.Contains("10 件", warning, StringComparison.Ordinal);
            Assert.Contains("4 件", warning, StringComparison.Ordinal);
        }

        [Fact]
        public void ReducingCountsFromTheAskedNotFromTheTotal()
        {
            Page<int> page = Take(All(100), 10, 6, (2 * PerItem) + 2);

            Assert.Equal(new[] { 10, 11 }, page.Items);
            Assert.Contains("6 件", Assert.Single(page.Warnings), StringComparison.Ordinal);
            Assert.Equal(12, page.NextOffset);
        }

        [Fact]
        public void NotEvenOneItemFittingIsRefused()
        {
            Page<int> page;

            Assert.False(Paging.TryTake(All(5), 0, 5, PerItem, Measure, out page));
            Assert.Null(page);
        }

        [Fact]
        public void NothingToReturnIsNotRefusedEvenWhenTheRoomIsTiny()
        {
            Page<int> page = Take(All(3), 3, 5, 0);

            Assert.Empty(page.Items);
            Assert.Empty(page.Warnings);
        }

        [Fact]
        public void TheWholeRestIsJudgedAtItsOwnSize()
        {
            IList<int> all = All(5);

            Page<int> page;
            Assert.True(
                Paging.TryTake(all, 0, 5, Measure(all.Count), WithNextOffset(all), out page));

            Assert.Equal(5, page.Items.Count);
            Assert.Empty(page.Warnings);
            Assert.Null(page.NextOffset);
        }

        [Fact]
        public void TheSearchStillFindsTheMostThatFits()
        {
            IList<int> all = All(5);

            Page<int> page;
            Assert.True(
                Paging.TryTake(
                    all, 0, 5, Measure(3) + NextOffsetChars, WithNextOffset(all), out page));

            Assert.Equal(3, page.Items.Count);
            Assert.Equal(3, page.NextOffset);
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            Page<int> page;

            Assert.Throws<ArgumentNullException>(
                () => Paging.TryTake(null, 0, 1, 100, Measure, out page));
            Assert.Throws<ArgumentNullException>(
                () => Paging.TryTake(All(1), 0, 1, 100, null, out page));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Paging.TryTake(All(1), -1, 1, 100, Measure, out page));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Paging.TryTake(All(1), 0, 0, 100, Measure, out page));
        }

        private static IList<int> All(int count)
        {
            return Enumerable.Range(0, count).ToList();
        }

        /// <summary>1件あたり同じ大きさを使い、並び全体で少しの枠を使うものとして量る。</summary>
        private static int Measure(IList<int> items)
        {
            return Measure(items.Count);
        }

        private static int Measure(int count)
        {
            return (count * PerItem) + 2;
        }

        /// <summary>続きが残るときだけ、続きの位置のぶんを足して量るもの。</summary>
        private static Func<IList<int>, int> WithNextOffset(IList<int> all)
        {
            return items => Measure(items.Count)
                + (items.Count == all.Count ? 0 : NextOffsetChars);
        }

        private static Page<int> Take(IList<int> all, int offset, int limit, int valueChars)
        {
            Page<int> page;
            Assert.True(Paging.TryTake(all, offset, limit, valueChars, Measure, out page));

            return page;
        }
    }
}
