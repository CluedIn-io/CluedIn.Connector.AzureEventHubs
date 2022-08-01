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
    public class AzureEventHubClient : EventHubBufferedProducerClient, IAzureEventHubClient, IDisposable
    {
        private readonly ExecutionContext _executionContext;
        private readonly Guid _providerDefinitionId;
        private readonly string _containerName;
        private readonly IConnectorConnection _config;

        #region Constructor & Destructor
        public AzureEventHubClient(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, IConnectorConnection config) :
            base(config.Authentication[AzureEventHubConstants.KeyName.ConnectionString].ToString(), config.Authentication[AzureEventHubConstants.KeyName.Name].ToString())
        {
            _executionContext = executionContext;
            _providerDefinitionId = providerDefinitionId;
            _containerName = containerName;
            _config = config;

            SendEventBatchFailedAsync += ProducerClient_SendEventBatchFailedAsync;
            SendEventBatchSucceededAsync += ProducerClient_SendEventBatchSucceededAsync;
        }
        ~AzureEventHubClient()
        {
            Dispose();
        }
        public async void Dispose()
        {
            await FlushAsync();

            SendEventBatchFailedAsync -= ProducerClient_SendEventBatchFailedAsync;
            SendEventBatchSucceededAsync -= ProducerClient_SendEventBatchSucceededAsync;

            await DisposeAsync();
        }
        #endregion

        public async Task QueueData(IDictionary<string, object> data)
        {
            var eventData = new EventData(Encoding.UTF8.GetBytes(JsonUtility.Serialize(data)));

            _executionContext.Log.LogInformation($"[AzureEventHub] Enqueue: {eventData.EventBody}");

            await EnqueueEventAsync(eventData);
        }

        #region Events
        private async Task ProducerClient_SendEventBatchFailedAsync(SendEventBatchFailedEventArgs arg)
        {
            _executionContext.Log.LogError($"[AzureEventHub] Publishing failed for { arg.EventBatch.Count } events.  Error: '{ arg.Exception.Message }'");

            foreach (var eventData in arg.EventBatch)
            {
                _executionContext.Log.LogWarning($"[AzureEventHub] Requeue: {eventData.EventBody}");

                await EnqueueEventAsync(eventData);
            }
        }
        private Task ProducerClient_SendEventBatchSucceededAsync(SendEventBatchSucceededEventArgs arg)
        {
            _executionContext.Log.LogDebug($"[AzureEventHub] { arg.EventBatch.Count } events were published to partition: '{ arg.PartitionId }.");

            return Task.CompletedTask;
        }
        #endregion
    }
}
