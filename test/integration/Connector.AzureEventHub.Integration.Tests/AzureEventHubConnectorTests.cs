using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers;
using Castle.Windsor;
using CluedIn.Connector.AzureEventHub.Connector;
using CluedIn.Connector.AzureServiceBus.Integration.Tests;
using CluedIn.Core.Caching;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Streams.Models;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Integration.Tests
{
    public class AzureEventHubConnectorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AzureEventHubConnectorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private string RootConnectionString => Environment.GetEnvironmentVariable("EVENTHUB_ROOTMANAGESHAREDACCESSKEY_CONNECTIONSTRING");

        private string TestEventHubConnectionString => Environment.GetEnvironmentVariable("EVENTHUB_TESTQUEUE_CONNECTIONSTRING");

        private string TestEventHubName => new EventHubConnection(TestEventHubConnectionString).EventHubName;

        [Fact]
        public async Task VerifyConnectionReturnsTrueForValidQueueAccessKey()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            container.Register(Component.For<IAzureEventHubClient>().ImplementedBy<AzureEventHubClient>().OnlyNewServices());
            container.Register(Component.For<AzureEventHubConnector>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connector = container.Resolve<AzureEventHubConnector>();

            // act
            var valid = await connector.VerifyConnection(executionContext, new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, TestEventHubConnectionString },
                { AzureEventHubConstants.KeyName.Name, TestEventHubName }
            });

            // assert
            Assert.True(valid);
        }

        [Theory]
        [InlineData(StreamMode.Sync)]
        [InlineData(StreamMode.EventStream)]
        public async Task VerifyCanStoreData(StreamMode mode)
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            container.Register(Component.For<IAzureEventHubClient>().ImplementedBy<AzureEventHubClient>().OnlyNewServices());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureEventHubConnector>(MockBehavior.Default,
                typeof(AzureEventHubConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, TestEventHubConnectionString },
                { AzureEventHubConstants.KeyName.Name, TestEventHubName }
            });

            var providerDefinitionId = Guid.NewGuid();

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, providerDefinitionId))
                .ReturnsAsync(connectionMock.Object);

            var entityId = Guid.NewGuid().ToString();

            var data = new Dictionary<string, object>
            {
                { "user.lastName", "Picard" },
                { "Name", "Jean Luc Picard" },
                { "Id", entityId },
                { "PersistHash", "1lzghdhhgqlnucj078/77q==" },
                { "OriginEntityCode", "/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0" },
                { "EntityType", "/Person" },
                { "Codes", new[] { "/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0" } },
            };

            var connector = connectorMock.Object;

            var client = new EventHubConsumerClient("$Default", RootConnectionString, TestEventHubName);

            var hubEmptyEvent = new AutoResetEvent(false);
            var messageReceivedEvent = new AutoResetEvent(false);
            EventData eventData = null;

            var _ = Task.Run(async () =>
            {
                var options = new ReadEventOptions
                {
                    MaximumWaitTime = TimeSpan.FromSeconds(1),
                };

                while (true)
                {
                    await foreach (var partitionEvent in client.ReadEventsAsync(options))
                    {
                        if (partitionEvent.Data == null)
                        {
                            hubEmptyEvent.Set();
                        }
                        else
                        {
                            var s = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());

                            if (s.Contains(entityId))
                            {
                                eventData = partitionEvent.Data;
                                messageReceivedEvent.Set();
                                return;
                            }
                        }
                    }
                }
            });

            if (!hubEmptyEvent.WaitOne(60000))
            {
                throw new TimeoutException();
            }

            connector.SetMode(mode);

            // act
            if (mode == StreamMode.EventStream)
            {
                await connector.StoreData(executionContext, providerDefinitionId, "test_container", "correlationId",
                    new DateTimeOffset(2023, 5, 15, 9, 17, 0, TimeSpan.Zero), VersionChangeType.Changed, data);
            }
            else
            {
                await connector.StoreData(executionContext, providerDefinitionId, "test_container", data);
            }

            // assert

            if (!messageReceivedEvent.WaitOne(60000))
            {
                throw new TimeoutException();
            }

            var receivedBody = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(Encoding.UTF8.GetString(eventData.Body.ToArray())), Formatting.Indented);

            if (mode == StreamMode.Sync)
            {
                receivedBody.Should().Be($@"{{
  ""user.lastName"": ""Picard"",
  ""Name"": ""Jean Luc Picard"",
  ""Id"": ""{entityId}"",
  ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
  ""OriginEntityCode"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
  ""EntityType"": ""/Person"",
  ""Codes"": [
    ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0""
  ]
}}");
            }
            else
            {
                receivedBody.Should().Be($@"{{
  ""TimeStamp"": ""2023-05-15T19:17:00+10:00"",
  ""VersionChangeType"": ""Changed"",
  ""CorrelationId"": ""correlationId"",
  ""Data"": {{
    ""user.lastName"": ""Picard"",
    ""Name"": ""Jean Luc Picard"",
    ""Id"": ""{entityId}"",
    ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
    ""OriginEntityCode"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
    ""EntityType"": ""/Person"",
    ""Codes"": [
      ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0""
    ]
  }}
}}");
            }
        }
    }
}
