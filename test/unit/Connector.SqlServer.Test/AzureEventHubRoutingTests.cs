using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CluedIn.Connector.AzureEventHub.Connector;
using CluedIn.Connector.AzureEventHub.Services;
using CluedIn.Core.Connectors;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    /// <summary>
    /// An optional per-stream routing key, stamped on every message so a consumer reading an event
    /// hub shared by several streams can tell which stream a message came from. A Removed event's
    /// payload is only { Id, Codes: [] } - no EntityType - and the Event Hubs message carries no
    /// discriminator either, so without this a delete cannot be routed.
    /// </summary>
    public class AzureEventHubRoutingTests
    {
        private const string ConnectionString =
            "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=abc123";

        // Returns a fixed export-target configuration, so the tests exercise the resolution logic
        // rather than the platform's authentication lookup.
        private sealed class StubConnector : AzureEventHubConnector
        {
            private readonly IReadOnlyDictionary<string, object> _authentication;

            public StubConnector(IReadOnlyDictionary<string, object> authentication)
                : base(new Mock<ILogger<AzureEventHubConnector>>().Object, new Mock<IClockService>().Object)
            {
                _authentication = authentication;
            }

            public override Task<IConnectorConnectionV2> GetAuthenticationDetails(
                ExecutionContext executionContext, Guid providerDefinitionId)
            {
                var connection = new Mock<IConnectorConnectionV2>();
                connection.SetupGet(x => x.Authentication).Returns(_authentication);
                return Task.FromResult(connection.Object);
            }
        }

        private static StubConnector Connector() => new StubConnector(new Dictionary<string, object>
        {
            { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
            { AzureEventHubConstants.KeyName.Name, "cluedin-entity-changes" },
        });

        private static IReadOnlyStreamModel Stream(IReadOnlyDictionary<string, object> connectorProperties = null)
        {
            var stream = new Mock<IReadOnlyStreamModel>();
            stream.SetupGet(x => x.ConnectorProperties).Returns(connectorProperties);
            return stream.Object;
        }

        [Fact]
        public async Task RoutingKey_ComesFromTheStreamsConnectorProperties()
        {
            var stream = Stream(new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.RoutingKey, "policy" },
            });

            var configuration = await Connector().GetStreamConfiguration(null, Guid.NewGuid(), stream);

            Assert.Equal("policy", configuration.RoutingKey);
        }

        [Fact]
        public async Task RoutingKey_IsNullWhenTheStreamDoesNotSetOne()
        {
            var configuration = await Connector().GetStreamConfiguration(null, Guid.NewGuid(), Stream());

            Assert.Null(configuration.RoutingKey);
        }

        [Fact]
        public async Task TheHubStillComesFromTheExportTarget()
        {
            // The stream supplies only its own properties; it must not affect the destination.
            var stream = Stream(new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.RoutingKey, "policy" },
            });

            var configuration = await Connector().GetStreamConfiguration(null, Guid.NewGuid(), stream);

            Assert.Equal("cluedin-entity-changes", configuration.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankRoutingKey_IsTreatedAsUnset(string raw)
        {
            var jobData = new AzureEventHubConnectorJobData(new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
                { AzureEventHubConstants.KeyName.Name, "cluedin-entity-changes" },
                { AzureEventHubConstants.KeyName.RoutingKey, raw },
            });

            Assert.Null(jobData.RoutingKey);
        }

        [Fact]
        public void RoutingKey_IsNotPartOfEquality()
        {
            // JobData is both the producer-client cache key and the PartitionedBuffer partition key.
            // The routing key is already baked into each message body and does not change which hub
            // the message goes to, so including it would split buffers and leak a client per value.
            var config = new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
                { AzureEventHubConstants.KeyName.Name, "cluedin-entity-changes" },
            };

            var policy = new AzureEventHubConnectorJobData(
                new Dictionary<string, object>(config) { { AzureEventHubConstants.KeyName.RoutingKey, "policy" } });
            var party = new AzureEventHubConnectorJobData(
                new Dictionary<string, object>(config) { { AzureEventHubConstants.KeyName.RoutingKey, "party" } });

            Assert.Equal(policy, party);
            Assert.Equal(policy.GetHashCode(), party.GetHashCode());
        }

        [Fact]
        public void RoutingKeyControl_IsOfferedOnTheStreamAndIsOptional()
        {
            var control = AzureEventHubConstants.Properties
                .Single(x => x.Name == AzureEventHubConstants.KeyName.RoutingKey);

            Assert.False(control.IsRequired);
        }
    }
}
