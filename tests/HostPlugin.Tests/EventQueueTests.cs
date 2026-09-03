using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class EventQueueTests
    {
        private const string Type = "pmx_view.mouse_down";

        private readonly EventQueue _queue = new EventQueue();

        [Fact]
        public void TheSeqStartsAtOneAndGrowsByOne()
        {
            Assert.Equal(1, _queue.Enqueue(Type, 1, null).Seq);
            Assert.Equal(2, _queue.Enqueue(Type, 1, null).Seq);
        }

        [Fact]
        public void AnEventKeepsItsTypeSourceAndPayload()
        {
            object payload = new object();

            QueuedEvent queued = _queue.Enqueue(Type, 7, payload);

            Assert.Equal(Type, queued.Type);
            Assert.Equal(7, queued.SourceHandle);
            Assert.Same(payload, queued.Payload);
        }

        [Fact]
        public void EventsComeOutOldestFirst()
        {
            _queue.Enqueue(Type, 1, "a");
            _queue.Enqueue(Type, 1, "b");

            EventDrainResult result = _queue.Drain(EventQueue.DefaultLimit);

            Assert.Equal(new object[] { "a", "b" }, result.Events.Select(e => e.Payload));
        }

        [Fact]
        public void OnlyTheAskedCountComesOutAndTheRestRemains()
        {
            for (int i = 0; i < 3; i++)
            {
                _queue.Enqueue(Type, 1, i);
            }

            EventDrainResult result = _queue.Drain(2);

            Assert.Equal(new object[] { 0, 1 }, result.Events.Select(e => e.Payload));
            Assert.Equal(1, result.Remaining);
            Assert.Equal(1, _queue.Count);
        }

        [Fact]
        public void DrainingAnEmptyQueueGivesNothing()
        {
            EventDrainResult result = _queue.Drain(EventQueue.DefaultLimit);

            Assert.Empty(result.Events);
            Assert.Equal(0, result.Dropped);
            Assert.Equal(0, result.Remaining);
        }

        [Fact]
        public void TheOldestIsDroppedWhenTheQueueIsFull()
        {
            for (int i = 0; i < EventQueue.Capacity + 2; i++)
            {
                _queue.Enqueue(Type, 1, i);
            }

            EventDrainResult result = _queue.Drain(EventQueue.MaxLimit);

            Assert.Equal(EventQueue.Capacity, result.Events.Count);
            Assert.Equal(2, result.Events[0].Payload);
            Assert.Equal(2, result.Dropped);
        }

        [Fact]
        public void ADroppedEventLeavesAGapInTheSeq()
        {
            for (int i = 0; i < EventQueue.Capacity + 1; i++)
            {
                _queue.Enqueue(Type, 1, i);
            }

            EventDrainResult result = _queue.Drain(EventQueue.MaxLimit);

            Assert.Equal(2, result.Events[0].Seq);
            Assert.Equal(1, result.Dropped);
        }

        [Fact]
        public void TheDroppedCountIsResetWhenItIsTold()
        {
            for (int i = 0; i < EventQueue.Capacity + 1; i++)
            {
                _queue.Enqueue(Type, 1, i);
            }

            Assert.Equal(1, _queue.Drain(EventQueue.MaxLimit).Dropped);
            Assert.Equal(0, _queue.Drain(EventQueue.MaxLimit).Dropped);
        }

        [Fact]
        public void TheDroppedCountCoversOnlyWhatWasDroppedSinceTheLastDrain()
        {
            for (int i = 0; i < EventQueue.Capacity + 1; i++)
            {
                _queue.Enqueue(Type, 1, i);
            }

            Assert.Equal(1, _queue.Drain(1).Dropped);
            Assert.Equal(EventQueue.Capacity - 1, _queue.Count);

            _queue.Enqueue(Type, 1, "a");
            _queue.Enqueue(Type, 1, "b");

            Assert.Equal(1, _queue.Drain(EventQueue.MaxLimit).Dropped);
        }

        [Fact]
        public void ALimitOutsideTheRangeStops()
        {
            foreach (int limit in new[] { 0, -1, EventQueue.MaxLimit + 1 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => _queue.Drain(limit));
            }
        }

        [Fact]
        public void TheLimitsAreTheContract()
        {
            Assert.Equal(1000, EventQueue.Capacity);
            Assert.Equal(100, EventQueue.DefaultLimit);
            Assert.Equal(1000, EventQueue.MaxLimit);
        }

        [Fact]
        public void AnEventWithoutATypeStops()
        {
            Assert.Throws<ArgumentNullException>(() => _queue.Enqueue(null, 1, null));
            Assert.Throws<ArgumentException>(() => _queue.Enqueue("  ", 1, null));
        }

        [Fact]
        public void AnEventWhoseSourceIsNotAHandleStops()
        {
            foreach (int sourceHandle in new[] { 0, -1 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _queue.Enqueue(Type, sourceHandle, null));
            }

            Assert.Equal(0, _queue.Count);
        }
    }
}
