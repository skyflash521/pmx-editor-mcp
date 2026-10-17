using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class PagingReadTests
    {
        private const int PerItem = 10;

        private const int Frame = 2;

        private const int Huge = 1000000;

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(37)]
        public void NoItemAfterTheFirstOneThatDoesNotFitIsRead(int fitting)
        {
            ReadCountingList all = new ReadCountingList(Huge, fitting + 1);

            Page<int> page;
            Assert.True(Paging.TryTake(all, 0, int.MaxValue, (fitting * PerItem) + Frame, Measure, out page));

            Assert.Equal(fitting, page.Items.Count);
            Assert.Equal(fitting, page.NextOffset);
            Assert.Equal(Huge, page.Total);
        }

        [Fact]
        public void NoItemAfterTheFirstOneThatDoesNotFitIsReadFromAnOffset()
        {
            ReadCountingList all = new ReadCountingList(Huge, 500 + 3 + 1);

            Page<int> page;
            Assert.True(Paging.TryTake(all, 500, int.MaxValue, (3 * PerItem) + Frame, Measure, out page));

            Assert.Equal(new[] { 500, 501, 502 }, page.Items);
            Assert.Equal(503, page.NextOffset);
        }

        private static int Measure(IList<int> items)
        {
            return (items.Count * PerItem) + Frame;
        }

        private sealed class ReadCountingList : IList<int>
        {
            private readonly int _count;

            private readonly int _lastReadable;

            public ReadCountingList(int count, int lastReadable)
            {
                _count = count;
                _lastReadable = lastReadable;
            }

            public int Count
            {
                get { return _count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public int this[int index]
            {
                get
                {
                    Assert.True(
                        index <= _lastReadable,
                        "枠に収まらなかった要素より後ろの位置 " + index + " を読んだ。");

                    return index;
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public IEnumerator<int> GetEnumerator()
            {
                for (int at = 0; at < _count; at++)
                {
                    yield return this[at];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int IndexOf(int item)
            {
                throw new NotSupportedException();
            }

            public void Insert(int index, int item)
            {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            public void Add(int item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(int item)
            {
                throw new NotSupportedException();
            }

            public void CopyTo(int[] array, int arrayIndex)
            {
                throw new NotSupportedException();
            }

            public bool Remove(int item)
            {
                throw new NotSupportedException();
            }
        }
    }
}
