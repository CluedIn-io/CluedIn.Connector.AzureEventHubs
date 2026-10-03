using System.Collections.Generic;
using System.Linq;
using System.Text;
using CluedIn.Connector.AzureEventHub.Connector;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    /// <summary>
    /// The hub rejects any single event over its limit, every time, so an entity carrying thousands of edges
    /// inline (2.9 MB and 11.6 MB in UAT, 2026-10-03) can never be sent as it is - and, buffered, it failed
    /// each flush it was in along with the events batched with it.
    /// </summary>
    public class OversizedEventTests
    {
        private static List<string> Edges(int count) => Enumerable.Range(0, count).Select(i => $"/Edge/{i:D8}").ToList();

        private static (Dictionary<string, object> envelope, Dictionary<string, object> entity) Message(int outgoing, int incoming)
        {
            var entity = new Dictionary<string, object> { { "Id", "e-1" }, { "Name", "x" } };
            if (outgoing > 0) entity["OutgoingEdges"] = Edges(outgoing);
            if (incoming > 0) entity["IncomingEdges"] = Edges(incoming);
            var envelope = new Dictionary<string, object> { { "VersionChangeType", "Changed" }, { "RoutingKey", "policy" }, { "Data", entity } };
            return (envelope, entity);
        }

        private static JObject Parse(byte[] body) => JObject.Parse(Encoding.UTF8.GetString(body));

        [Fact]
        public void An_event_under_the_limit_is_sent_whole()
        {
            var (envelope, entity) = Message(10, 10);

            var body = AzureEventHubConnector.SerializeWithinLimit(envelope, entity, 100_000, out var omitted, out var fullSize);

            Assert.False(omitted);
            Assert.Equal(body.Length, fullSize);
            var data = (JObject)Parse(body)["Data"];
            Assert.Equal(10, data["OutgoingEdges"].Count());
            Assert.Null(data["EdgesOmitted"]);
        }

        [Fact]
        public void An_event_over_the_limit_is_sent_without_its_edges_and_says_so()
        {
            var (envelope, entity) = Message(5_000, 5_000);

            var body = AzureEventHubConnector.SerializeWithinLimit(envelope, entity, 10_000, out var omitted, out var fullSize);

            Assert.True(omitted);
            Assert.True(fullSize > 10_000);
            Assert.True(body.Length <= 10_000);
            var message = Parse(body);
            var data = (JObject)message["Data"];
            Assert.Null(data["OutgoingEdges"]);
            Assert.Null(data["IncomingEdges"]);
            Assert.True((bool)data["EdgesOmitted"]);
            Assert.Equal("e-1", (string)data["Id"]);
            Assert.Equal("policy", (string)message["RoutingKey"]);
        }

        [Fact]
        public void Only_one_kind_of_edge_is_enough_to_trim()
        {
            var (envelope, entity) = Message(0, 5_000);

            var body = AzureEventHubConnector.SerializeWithinLimit(envelope, entity, 10_000, out var omitted, out _);

            Assert.True(omitted);
            Assert.NotNull(body);
        }

        [Fact]
        public void An_event_still_over_the_limit_without_edges_is_not_sent()
        {
            var (envelope, entity) = Message(5_000, 0);
            entity["Name"] = new string('x', 20_000);

            var body = AzureEventHubConnector.SerializeWithinLimit(envelope, entity, 10_000, out _, out var fullSize);

            Assert.Null(body);
            Assert.True(fullSize > 20_000);
        }

        [Fact]
        public void An_event_over_the_limit_with_no_edges_is_not_sent()
        {
            var (envelope, entity) = Message(0, 0);
            entity["Name"] = new string('x', 20_000);

            Assert.Null(AzureEventHubConnector.SerializeWithinLimit(envelope, entity, 10_000, out var omitted, out _));
            Assert.False(omitted);
            Assert.False(entity.ContainsKey("EdgesOmitted"));
        }

        [Fact]
        public void The_limit_leaves_headroom_under_the_standard_tier_maximum()
        {
            Assert.True(AzureEventHubConnector.MaxEventBytes < 1_048_576);
        }
    }
}
