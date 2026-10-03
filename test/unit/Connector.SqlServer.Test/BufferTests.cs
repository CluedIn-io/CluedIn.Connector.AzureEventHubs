using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    /// <summary>
    /// Buffer replaces upstream v4.5.1's buffer, whose flush reset a shared item count to 0 after sending,
    /// wiping any Add that took a slot in between: that caller hung, and a later flush threw "Adding the
    /// specified count to the semaphore would cause it to exceed its maximum count".
    /// </summary>
    public class BufferTests
    {
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

        [Fact]
        public async Task A_full_batch_is_flushed_and_every_add_completes()
        {
            var flushed = new ConcurrentQueue<int[]>();
            var buffer = new Buffer<int>(3, 60_000, batch => { flushed.Enqueue(batch); return Task.CompletedTask; });

            await Task.WhenAll(buffer.Add(1), buffer.Add(2), buffer.Add(3)).WaitAsync(Guard);

            Assert.Single(flushed);
            Assert.Equal(new[] { 1, 2, 3 }, flushed.Single().OrderBy(x => x));
        }

        [Fact]
        public async Task A_partial_batch_is_flushed_after_the_idle_timeout()
        {
            var flushed = new ConcurrentQueue<int[]>();
            var buffer = new Buffer<int>(50, 300, batch => { flushed.Enqueue(batch); return Task.CompletedTask; });

            await Task.WhenAll(buffer.Add(1), buffer.Add(2)).WaitAsync(Guard);

            Assert.Equal(new[] { 1, 2 }, flushed.SelectMany(b => b).OrderBy(x => x));
        }

        [Fact]
        public async Task Flush_sends_what_is_buffered_now()
        {
            var flushed = new ConcurrentQueue<int[]>();
            var buffer = new Buffer<int>(50, 60_000, batch => { flushed.Enqueue(batch); return Task.CompletedTask; });

            var add = buffer.Add(7);
            await buffer.Flush().WaitAsync(Guard);
            await add.WaitAsync(Guard);

            Assert.Equal(new[] { 7 }, flushed.SelectMany(b => b));
        }

        [Fact]
        public async Task A_failed_flush_faults_every_add_in_the_batch_including_the_one_that_filled_it()
        {
            var buffer = new Buffer<int>(3, 60_000, _ => throw new InvalidOperationException("hub said no"));

            var adds = new[] { buffer.Add(1), buffer.Add(2), buffer.Add(3) };

            foreach (var add in adds)
            {
                var ex = await Assert.ThrowsAsync<AggregateException>(() => add.WaitAsync(Guard));
                Assert.IsType<InvalidOperationException>(ex.InnerException);
            }
        }

        [Fact]
        public async Task A_failed_flush_does_not_stop_later_batches()
        {
            var calls = 0;
            var flushed = new ConcurrentQueue<int[]>();
            var buffer = new Buffer<int>(2, 60_000, batch =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("transient");
                }

                flushed.Enqueue(batch);
                return Task.CompletedTask;
            });

            await Assert.ThrowsAsync<AggregateException>(() => Task.WhenAll(buffer.Add(1), buffer.Add(2)).WaitAsync(Guard));
            await Task.WhenAll(buffer.Add(3), buffer.Add(4)).WaitAsync(Guard);

            Assert.Equal(new[] { 3, 4 }, flushed.SelectMany(b => b).OrderBy(x => x));
        }

        [Fact]
        public async Task Under_heavy_concurrency_every_item_is_flushed_exactly_once_and_no_add_hangs()
        {
            // The upstream race: adds arriving while a flush (size- or idle-triggered) is in progress.
            // A short idle timeout and a slow, jittery bulk action keep both kinds of flush overlapping adds.
            const int Callers = 60, PerCaller = 400;
            var seen = new ConcurrentDictionary<int, int>();
            var random = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
            var buffer = new Buffer<int>(50, 20, async batch =>
            {
                foreach (var item in batch)
                {
                    seen.AddOrUpdate(item, 1, (_, n) => n + 1);
                }

                await Task.Delay(random.Value.Next(0, 5));
            });

            var callers = Enumerable.Range(0, Callers).Select(c => Task.Run(async () =>
            {
                for (var i = 0; i < PerCaller; i++)
                {
                    await buffer.Add(c * PerCaller + i);

                    if (random.Value.Next(0, 10) == 0)
                    {
                        await Task.Delay(random.Value.Next(0, 30));   // let idle flushes fire mid-stream
                    }
                }
            }));

            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromMinutes(2));

            Assert.Equal(Callers * PerCaller, seen.Count);
            Assert.All(seen.Values, n => Assert.Equal(1, n));
        }

        [Fact]
        public void Max_size_must_be_at_least_one()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Buffer<int>(0, 100, _ => Task.CompletedTask));
        }
    }
}
