using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs.Producer;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using Microsoft.Extensions.Logging;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public interface IAzureEventHubClient
    {
        Task QueueData(IConnectorConnection config, IDictionary<string, object> data);
        EventHubProducerClient GetEventHubClient(IConnectorConnection config);
    }
}
