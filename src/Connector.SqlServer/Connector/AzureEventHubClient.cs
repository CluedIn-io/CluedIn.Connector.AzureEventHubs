using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using Microsoft.Extensions.Logging;

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
                var eventHubClient = new EventHubBufferedProducerClient(
                    config.Authentication[AzureEventHubConstants.KeyName.ConnectionString].ToString(),
                    config.Authentication[AzureEventHubConstants.KeyName.Name].ToString());

                try
                {
                    var retried = 0;
                    var resetEvent = new System.Threading.AutoResetEvent(false);
                    eventHubClient.SendEventBatchFailedAsync += args =>
                    {
                        _logger.LogError($"[AzureEventHub] Publishing failed for { args.EventBatch.Count } events.  Error: '{ args.Exception.Message }'");

                        retried++;
                        if (retried < 3)
                        {
                            foreach (var eventData in args.EventBatch)
                            {
                                _logger.LogWarning($"[AzureEventHub] Requeue: {eventData.EventBody}");

                                eventHubClient.EnqueueEventAsync(eventData);
                            }
                        }
                        else
                        {
                            resetEvent.Set();
                            _logger.LogError($"[AzureEventHub] Retried count exceed. { args.EventBatch.Count } events will be disposed!");
                        }
                        return Task.CompletedTask;
                    };
                    eventHubClient.SendEventBatchSucceededAsync += args =>
                    {
                        _logger.LogDebug($"[AzureEventHub] { args.EventBatch.Count } events were published to partition: '{ args.PartitionId }.");

                        resetEvent.Set();
                        return Task.CompletedTask;
                    };

                    var eventData = new EventData(Encoding.UTF8.GetBytes(JsonUtility.Serialize(data)));
                    _logger.LogDebug($"[AzureEventHub] Enqueue: {eventData.EventBody}");

                    await eventHubClient.EnqueueEventAsync(eventData);

                    resetEvent.WaitOne(60000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AzureEventHub] Error occured in queuing data!]");
                }
                finally
                {
                    // Closing the producer will flush any
                    // enqueued events that have not been published.
                    await eventHubClient.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[AzureEventHub] Unable to connect! {config}]");
            }
        }
    }
}
