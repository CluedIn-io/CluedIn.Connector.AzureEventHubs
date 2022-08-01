using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public interface IAzureEventHubClient
    {
        Task QueueData(IDictionary<string, object> data);
    }
}
