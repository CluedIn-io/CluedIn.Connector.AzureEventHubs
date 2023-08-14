using CluedIn.Connector.AzureEventHub.Connector;
using CluedIn.Connector.AzureEventHub.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    public class AzureEventConnectorTestsBase
    {
        protected readonly AzureEventHubConnector Sut;
        protected readonly Mock<ILogger<AzureEventHubConnector>> Logger = new Mock<ILogger<AzureEventHubConnector>>();
        protected readonly Mock<IAzureEventHubClient> Client = new Mock<IAzureEventHubClient>();
        protected readonly Mock<IClockService> Clock = new Mock<IClockService>();
        protected readonly TestContext Context = new TestContext();

        public AzureEventConnectorTestsBase()
        {
            Sut = new AzureEventHubConnector(Logger.Object, Client.Object, Clock.Object);
        }
    }
}
