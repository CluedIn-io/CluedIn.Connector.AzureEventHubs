using CluedIn.Core.Connectors;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubConnectorContainer : IConnectorContainer
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string FullyQualifiedName { get; set; }
    }
}
