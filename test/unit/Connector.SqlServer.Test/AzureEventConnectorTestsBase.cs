using CluedIn.Connector.AzureEventHub.Connector;
using CluedIn.Core.DataStore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    public class AzureEventConnectorTestsBase
    {
        protected readonly AzureEventHubConnector Sut;
        protected readonly Mock<IConfigurationRepository> Repo = new Mock<IConfigurationRepository>();
        protected readonly Mock<ILogger<AzureEventHubConnector>> Logger = new Mock<ILogger<AzureEventHubConnector>>();
        protected readonly Mock<IAzureEventHubClient> Client = new Mock<IAzureEventHubClient>();
        protected readonly TestContext Context = new TestContext();

        public AzureEventConnectorTestsBase()
        {
            Sut = new AzureEventHubConnector(Repo.Object, Logger.Object, Client.Object);
        }
    }
}
