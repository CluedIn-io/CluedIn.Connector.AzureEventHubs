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
using CluedIn.Connector.AzureEventHub.Services;
using CluedIn.Core.Caching;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Vocabularies;
using CluedIn.Core.Streams.Models;
using FluentAssertions;
using Moq;
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

            container.Register(Component.For<AzureEventHubConnector>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connector = container.Resolve<AzureEventHubConnector>();

            // act
            var verificationResult = await connector.VerifyConnection(executionContext, new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, TestEventHubConnectionString },
                { AzureEventHubConstants.KeyName.Name, TestEventHubName }
            });

            // assert
            Assert.True(verificationResult.Success);
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

            var connectionMock = new Mock<IConnectorConnectionV2>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureEventHubConstants.KeyName.ConnectionString, TestEventHubConnectionString },
                { AzureEventHubConstants.KeyName.Name, TestEventHubName }
            });

            var clockMock = new Mock<IClockService>();
            clockMock.Setup(x => x.Now)
                .Returns(new DateTimeOffset(new DateTime(2023, 5, 15, 9, 17, 0, DateTimeKind.Utc), TimeSpan.Zero)
                    .ToUniversalTime());
            container.Register(Component.For<IClockService>().Instance(clockMock.Object));

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureEventHubConnector>(MockBehavior.Default,
                typeof(AzureEventHubConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var providerDefinitionId = Guid.NewGuid();

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, providerDefinitionId))
                .ReturnsAsync(connectionMock.Object);

            var entityId = Guid.NewGuid().ToString();

            var data = new ConnectorEntityData(VersionChangeType.Changed, mode,
                Guid.Parse(entityId),
                new ConnectorEntityPersistInfo("1lzghdhhgqlnucj078/77q==", 1), null,
                EntityCode.FromKey($"/Person#Acceptance:{entityId}"),
                "/Person",
                new[]
                {
                    new ConnectorPropertyData("user.lastName", "Picard",
                        new VocabularyKeyConnectorPropertyDataType(new VocabularyKey("user.lastName"))),
                    new ConnectorPropertyData("Name", "Jean Luc Picard",
                        new EntityPropertyConnectorPropertyDataType(typeof(string))),
                },
                new IEntityCode[] { EntityCode.FromKey($"/Person#Acceptance:{entityId}") },
                null, null);

            var streamModel = new Mock<IReadOnlyStreamModel>();
            streamModel.Setup(x => x.ConnectorProviderDefinitionId).Returns(providerDefinitionId);
            streamModel.Setup(x => x.ContainerName).Returns("test_container");
            streamModel.Setup(x => x.Mode).Returns(mode);

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

            // act
            await connector.StoreData(executionContext, streamModel.Object, data);

            // assert

            if (!messageReceivedEvent.WaitOne(60000))
            {
                throw new TimeoutException();
            }

            var receivedBody = Encoding.UTF8.GetString(eventData.Body.ToArray());

            if (mode == StreamMode.Sync)
            {
                receivedBody.Should().Be($@"{{
  ""user.lastName"": ""Picard"",
  ""Name"": ""Jean Luc Picard"",
  ""Id"": ""{entityId}"",
  ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
  ""OriginEntityCode"": ""/Person#Acceptance:{entityId}"",
  ""EntityType"": ""/Person"",
  ""Codes"": [
    ""/Person#Acceptance:{entityId}""
  ],
  ""ChangeType"": ""Changed""
}}");
            }
            else
            {
                receivedBody.Should().Be($@"{{
  ""TimeStamp"": ""2023-05-15T09:17:00+00:00"",
  ""VersionChangeType"": ""Changed"",
  ""Data"": {{
    ""user.lastName"": ""Picard"",
    ""Name"": ""Jean Luc Picard"",
    ""Id"": ""{entityId}"",
    ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
    ""OriginEntityCode"": ""/Person#Acceptance:{entityId}"",
    ""EntityType"": ""/Person"",
    ""Codes"": [
      ""/Person#Acceptance:{entityId}""
    ]
  }}
}}");
            }
        }
    }
}
