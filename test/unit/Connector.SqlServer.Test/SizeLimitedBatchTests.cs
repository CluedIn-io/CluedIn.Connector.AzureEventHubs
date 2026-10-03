using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using CluedIn.Connector.AzureEventHub.Connector;
using Xunit;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    /// <summary>
    /// A flush must never hand the hub a message over its size limit: upstream v4.5.1 sent up to 50 events
    /// in one SendAsync and the hub rejected all of them once they passed 1 MB (1,137,914 bytes for 49
    /// events, UAT 2026-10-03).
    /// </summary>
    public class SizeLimitedBatchTests
    {
        private static EventData Event(int bytes) => new EventData(new byte[bytes]);

        // Fake EventDataBatches that accept events until their bodies would pass `limit` bytes.
        private static Func<Task<EventDataBatch>> Batches(long limit, List<List<EventData>> created)
        {
            return () =>
            {
                var store = new List<EventData>();
                created.Add(store);
                long used = 0;
                var batch = EventHubsModelFactory.EventDataBatch(limit, store, new CreateBatchOptions(), e =>
                {
                    if (used + e.Body.Length > limit)
                    {
                        return false;
                    }

                    used += e.Body.Length;
                    return true;
                });
                return Task.FromResult(batch);
            };
        }

        [Fact]
        public async Task Events_are_split_into_batches_that_each_fit_the_limit()
        {
            var created = new List<List<EventData>>();
            var sent = new List<int>();
            var events = Enumerable.Range(0, 49).Select(_ => Event(23_000)).ToArray();   // 1,127,000 bytes in all

            await AzureEventHubConnector.SendInSizeLimitedBatches(events, Batches(1_000_000, created), b => { sent.Add(b.Count); return Task.CompletedTask; });

            Assert.Equal(new[] { 43, 6 }, sent);   // 43 x 23,000 = 989,000 <= 1,000,000
            Assert.All(created.Where(s => s.Count > 0), s => Assert.True(s.Sum(e => e.Body.Length) <= 1_000_000));
        }

        [Fact]
        public async Task Events_that_fit_go_in_a_single_send()
        {
            var sent = new List<int>();

            await AzureEventHubConnector.SendInSizeLimitedBatches(
                Enumerable.Range(0, 50).Select(_ => Event(1_000)).ToArray(),
                Batches(1_000_000, new List<List<EventData>>()),
                b => { sent.Add(b.Count); return Task.CompletedTask; });

            Assert.Equal(new[] { 50 }, sent);
        }

        [Fact]
        public async Task An_event_too_big_for_any_batch_throws_after_the_events_before_it_are_sent()
        {
            var sent = new List<int>();
            var events = new[] { Event(100), Event(100), Event(2_000), Event(100) };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AzureEventHubConnector.SendInSizeLimitedBatches(events, Batches(1_000, new List<List<EventData>>()), b => { sent.Add(b.Count); return Task.CompletedTask; }));

            Assert.Equal(new[] { 2 }, sent);
            Assert.Contains("2000 bytes", ex.Message);
        }

        [Fact]
        public async Task Nothing_is_sent_for_no_events()
        {
            var sent = 0;

            await AzureEventHubConnector.SendInSizeLimitedBatches(Array.Empty<EventData>(), Batches(1_000, new List<List<EventData>>()), _ => { sent++; return Task.CompletedTask; });

            Assert.Equal(0, sent);
        }
    }
}
