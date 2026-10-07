using System;
using System.Linq;
using System.Text;
using Azure.Messaging.EventHubs;
using CluedIn.Connector.AzureEventHub;
using CluedIn.Connector.AzureEventHub.Connector;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests.Connector
{
    // Covers AzureEventHubConnector.Chunk and .CombineEventData - the two helpers behind the opt-in
    // message-combining feature (AzureEventHubConstants.KeyName.CombineMessages / BatchSize). Both are
    // internal static and exposed to this assembly via [InternalsVisibleTo] in src/Connector.SqlServer/AssemblyInfo.cs.
    public class AzureEventHubConnectorMessageCombiningTests
    {
        private static EventData CreateEventData(string body) => new EventData(Encoding.UTF8.GetBytes(body));

        private static string GetBody(EventData eventData) => Encoding.UTF8.GetString(eventData.Body.ToArray());

        public class ChunkTests
        {
            [Fact]
            public void EmptyInput_YieldsNoChunks()
            {
                var chunks = AzureEventHubConnector.Chunk(Array.Empty<EventData>(), 10).ToArray();

                Assert.Empty(chunks);
            }

            [Fact]
            public void FewerItemsThanBatchSize_YieldsSingleChunkWithAllItems()
            {
                var items = new[] { CreateEventData("{}"), CreateEventData("{}") };

                var chunks = AzureEventHubConnector.Chunk(items, 10).ToArray();

                Assert.Single(chunks);
                Assert.Equal(2, chunks[0].Length);
            }

            [Fact]
            public void SplitsIntoGroupsOfAtMostBatchSize()
            {
                var items = Enumerable.Range(0, 7).Select(i => CreateEventData($"{{\"i\":{i}}}")).ToArray();

                var chunks = AzureEventHubConnector.Chunk(items, 3).ToArray();

                Assert.Equal(3, chunks.Length);
                Assert.Equal(3, chunks[0].Length);
                Assert.Equal(3, chunks[1].Length);
                Assert.Single(chunks[2]);
            }

            [Fact]
            public void BatchSizeOne_PutsEachItemInItsOwnChunk()
            {
                var items = Enumerable.Range(0, 3).Select(i => CreateEventData($"{{\"i\":{i}}}")).ToArray();

                var chunks = AzureEventHubConnector.Chunk(items, 1).ToArray();

                Assert.Equal(3, chunks.Length);
                Assert.All(chunks, chunk => Assert.Single(chunk));
            }

            [Fact]
            public void PreservesItemOrderAcrossAndWithinChunks()
            {
                var items = Enumerable.Range(0, 5).Select(i => CreateEventData($"{{\"i\":{i}}}")).ToArray();

                var chunks = AzureEventHubConnector.Chunk(items, 2).ToArray();

                var flattenedBodies = chunks.SelectMany(chunk => chunk).Select(GetBody).ToArray();
                var originalBodies = items.Select(GetBody).ToArray();
                Assert.Equal(originalBodies, flattenedBodies);
            }

            [Fact]
            public void SplitsEarlyWhenCombinedByteSizeWouldExceedMax()
            {
                // Two items exactly filling MaxCombinedMessageBytes between them stay in one chunk (boundary is
                // strictly-greater-than); a third item then forces a split before it's added, even though the
                // batchSize limit (10) was never reached by count.
                var halfMax = AzureEventHubConstants.MaxCombinedMessageBytes / 2;
                var item1 = CreateEventData(new string('a', halfMax));
                var item2 = CreateEventData(new string('b', halfMax));
                var item3 = CreateEventData(new string('c', halfMax));

                var chunks = AzureEventHubConnector.Chunk(new[] { item1, item2, item3 }, 10).ToArray();

                Assert.Equal(2, chunks.Length);
                Assert.Equal(2, chunks[0].Length);
                Assert.Single(chunks[1]);
            }

            [Fact]
            public void SingleItemLargerThanMaxIsKeptAloneRatherThanDroppedOrSplit()
            {
                var oversized = CreateEventData(new string('x', AzureEventHubConstants.MaxCombinedMessageBytes + 1000));
                var normal = CreateEventData("{}");

                var chunks = AzureEventHubConnector.Chunk(new[] { oversized, normal }, 10).ToArray();

                Assert.Equal(2, chunks.Length);
                Assert.Single(chunks[0]);
                Assert.Single(chunks[1]);
                Assert.Equal(GetBody(oversized), GetBody(chunks[0][0]));
            }
        }

        public class CombineEventDataTests
        {
            [Fact]
            public void WrapsBodiesInCountAndMessagesEnvelope()
            {
                var items = new[] { CreateEventData("{\"a\":1}"), CreateEventData("{\"b\":2}") };

                var combined = AzureEventHubConnector.CombineEventData(items);

                Assert.Equal("{\"count\":2,\"messages\":[{\"a\":1},{\"b\":2}]}", GetBody(combined));
            }

            [Fact]
            public void SingleItem_WrapsWithoutTrailingComma()
            {
                var items = new[] { CreateEventData("{\"a\":1}") };

                var combined = AzureEventHubConnector.CombineEventData(items);

                Assert.Equal("{\"count\":1,\"messages\":[{\"a\":1}]}", GetBody(combined));
            }

            [Fact]
            public void ConcatenatesRawBodiesVerbatimWithoutReparsing()
            {
                // Whitespace/formatting inside each body must survive untouched - proves the bodies are
                // string-concatenated as-is rather than deserialized and re-serialized.
                var items = new[] { CreateEventData("{\n  \"a\": 1\n}"), CreateEventData("{\"b\":2}") };

                var combined = AzureEventHubConnector.CombineEventData(items);

                Assert.Equal("{\"count\":2,\"messages\":[{\n  \"a\": 1\n},{\"b\":2}]}", GetBody(combined));
            }

            [Fact]
            public void ResultIsWellFormedJsonWithMatchingCountAndMessageArrayLength()
            {
                var items = new[]
                {
                    CreateEventData("{\"a\":1}"),
                    CreateEventData("{\"b\":2}"),
                    CreateEventData("{\"c\":3}"),
                };

                var combined = AzureEventHubConnector.CombineEventData(items);

                var parsed = JObject.Parse(GetBody(combined));
                Assert.Equal(3, (int)parsed["count"]);
                Assert.Equal(3, ((JArray)parsed["messages"]).Count);
            }
        }
    }
}
