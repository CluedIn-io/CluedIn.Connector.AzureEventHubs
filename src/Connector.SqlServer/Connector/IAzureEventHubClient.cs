using System.Collections.Generic;
using System.Threading.Tasks;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using Microsoft.Extensions.Logging;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public interface IAzureEventHubClient
    {
        Task QueueData(IConnectorConnection config, IDictionary<string, object> data);
    }
}
