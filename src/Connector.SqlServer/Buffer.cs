using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CluedIn.Connector.AzureEventHub
{
    /// <summary>
    /// Collects items and hands them to a bulk action in batches: when <c>maxSize</c> items have been
    /// added, or when <c>timeout</c> ms pass with no new item. <see cref="Add"/> completes once its item's
    /// batch has been flushed, and faults with that batch's exception if the flush fails - so a RabbitMQ
    /// message is only acknowledged once its event was actually sent.
    /// </summary>
    /// <remarks>
    /// Replaces upstream v4.5.1's buffer, which tracked a shared item count, array slots and four
    /// semaphores separately. Its flush read the count, sent, and then reset the count to 0, so an
    /// <c>Add</c> that took a slot in between was wiped: its caller waited forever (holding a RabbitMQ
    /// message unacknowledged) and a later flush released that slot's semaphore again, throwing
    /// "Adding the specified count to the semaphore would cause it to exceed its maximum count" and
    /// stalling the stream (UAT, 2026-10-03, during a 15M-event initial export). Here every item is
    /// paired with its own completion and a batch is swapped out whole under the same lock that adds
    /// to it, so an item is always in exactly one batch.
    /// </remarks>
    internal class Buffer<T> : IDisposable
    {
        private readonly int _initialMaxSize;
        private readonly int _timeout;
        private readonly Func<T[], Task> _bulkAction;

        private readonly object _lock = new object();
        private readonly SemaphoreSlim _flushSemaphore = new SemaphoreSlim(1, 1);

        private List<Pending> _pending = new List<Pending>();
        private int _maxSize;
        private DateTime _lastAdded;
        private Task _idleTask;

        private readonly int _autoMaxSizeDetectionSampleSize = 3;   // twice is a coincidence, three times is a pattern
        private readonly List<(int itemCount, DateTime flushedAt, TimeSpan flushDuration)> _idleFlushHistory = new List<(int, DateTime, TimeSpan)>();
        private DateTime _autoMaxSizeSetAt;

        private readonly struct Pending
        {
            public Pending(T item)
            {
                Item = item;
                Done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public T Item { get; }

            public TaskCompletionSource<bool> Done { get; }
        }

        public Buffer(int maxSize, int timeout, Func<T[], Task> bulkAction)
        {
            if (maxSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "must be at least 1");
            }

            _initialMaxSize = maxSize;
            _maxSize = maxSize;
            _timeout = timeout;
            _bulkAction = bulkAction ?? throw new ArgumentNullException(nameof(bulkAction));
        }

        public void Dispose()
        {
            Flush().Wait();
        }

        public Task Add(T item)
        {
            var pending = new Pending(item);
            List<Pending> full = null;

            lock (_lock)
            {
                _pending.Add(pending);
                _lastAdded = DateTime.UtcNow;

                if (_pending.Count >= _maxSize)
                {
                    full = _pending;
                    _pending = new List<Pending>();
                }
                else
                {
                    _idleTask ??= Idle();
                }
            }

            if (full != null)
            {
                // Not awaited here: the caller awaits its own completion, which FlushBatch sets.
                _ = FlushBatch(full, idle: false);
            }

            return pending.Done.Task;
        }

        /// <summary>Flushes whatever is buffered now, and waits for it.</summary>
        public Task Flush()
        {
            List<Pending> batch;

            lock (_lock)
            {
                batch = _pending;
                _pending = new List<Pending>();
            }

            return batch.Count == 0 ? Task.CompletedTask : FlushBatch(batch, idle: true);
        }

        private async Task Idle()
        {
            while (true)
            {
                await Task.Delay(100).ConfigureAwait(false);

                List<Pending> batch;

                lock (_lock)
                {
                    if (_pending.Count == 0)
                    {
                        _idleTask = null;
                        return;
                    }

                    if (DateTime.UtcNow.Subtract(_lastAdded).TotalMilliseconds < _timeout)
                    {
                        continue;
                    }

                    batch = _pending;
                    _pending = new List<Pending>();
                    _idleTask = null;   // an Add from here on starts a new idle timer
                }

                await FlushBatch(batch, idle: true).ConfigureAwait(false);
                return;
            }
        }

        private async Task FlushBatch(List<Pending> batch, bool idle)
        {
            await _flushSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                var flushStartedAt = DateTime.UtcNow;

                try
                {
                    await _bulkAction(batch.Select(p => p.Item).ToArray()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Every item in the batch failed with it - including the one whose Add filled it,
                    // which upstream v4.5.1 reported as success.
                    var error = new AggregateException(ex);
                    foreach (var pending in batch)
                    {
                        pending.Done.TrySetException(error);
                    }

                    return;
                }

                lock (_lock)
                {
                    AutoAdjustMaxSize(idle, batch.Count, flushStartedAt);
                }

                foreach (var pending in batch)
                {
                    pending.Done.TrySetResult(true);
                }
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        /// <summary>
        /// If the stream prefetch is less than <c>maxSize</c>, a batch never fills by count and every flush
        /// waits for the idle timeout, collapsing throughput. Detect that - three idle flushes of the same
        /// size in quick succession - and lower the size-trigger to it. Reset to the initial size after 10
        /// minutes in case the detection was wrong. (Unchanged from upstream v4.5.1, apart from taking the
        /// flushed item count as a parameter.) Called under <c>_lock</c>.
        /// </summary>
        private void AutoAdjustMaxSize(bool idle, int itemCount, DateTime flushStartedAt)
        {
            if (idle)
            {
                _idleFlushHistory.Add((itemCount, flushStartedAt, DateTime.UtcNow.Subtract(flushStartedAt)));

                if (_idleFlushHistory.Count > _autoMaxSizeDetectionSampleSize)
                {
                    _idleFlushHistory.RemoveAt(0);
                }

                if (_idleFlushHistory.Count == _autoMaxSizeDetectionSampleSize &&
                    _idleFlushHistory.All(h => h.itemCount == _idleFlushHistory[0].itemCount))
                {
                    var allIdleFlushesExecutedInMinimumTime =
                        _idleFlushHistory.Last().flushedAt.Subtract(_idleFlushHistory.First().flushedAt)
                            .TotalMilliseconds <
                        (_idleFlushHistory.Count - 1) * _timeout +
                        _idleFlushHistory.Take(_idleFlushHistory.Count - 1)
                            .Sum(x => x.flushDuration.TotalMilliseconds) +
                        itemCount *
                        20; // time is needed to populate the items between flushes so lets pick an arbitrary 20ms per item

                    if (allIdleFlushesExecutedInMinimumTime)
                    {
                        _maxSize = Math.Max(1, _idleFlushHistory[0].itemCount);
                        _autoMaxSizeSetAt = DateTime.UtcNow;
                    }
                }
            }

            // periodically reset maxSize back to initialMaxSize just in case the auto detection incorrectly reduced it
            if (_maxSize != _initialMaxSize && DateTime.UtcNow.Subtract(_autoMaxSizeSetAt).TotalMinutes > 10)
            {
                _maxSize = _initialMaxSize;
            }
        }
    }
}
