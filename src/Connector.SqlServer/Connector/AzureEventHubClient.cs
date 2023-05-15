using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubClient : IAzureEventHubClient
    {
        private readonly ILogger<AzureEventHubClient> _logger;

        public AzureEventHubClient(ILogger<AzureEventHubClient> logger)
        {
            _logger = logger;
        }

        public async Task QueueData(IConnectorConnection config, IDictionary<string, object> data)
        {
            try
            {
                var eventHubClient = GetEventHubClient(config);

                var eventData = new EventData(Encoding.UTF8.GetBytes(JsonUtility.Serialize(data,
                    new JsonSerializer
                    {
                        Formatting = Formatting.Indented,
                        TypeNameHandling = TypeNameHandling.None, // don't want to expose our internal class names
                    }))
                );

                await ActionExtensions.ExecuteWithRetryAsync(async () => await eventHubClient.SendAsync(new[] { eventData }));
                await eventHubClient.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AzureEventHub] Error occurred in queuing data!]");
            }
        }

        public EventHubProducerClient GetEventHubClient(IConnectorConnection config)
        {
            return new EventHubProducerClient(
                config.Authentication[AzureEventHubConstants.KeyName.ConnectionString].ToString(),
                config.Authentication[AzureEventHubConstants.KeyName.Name].ToString());
        }
    }
}
