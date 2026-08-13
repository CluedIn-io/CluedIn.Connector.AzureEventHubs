using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using CluedIn.Connector.AzureEventHub.Services;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using CluedIn.Core.Processing;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubConnector : ConnectorBaseV2
    {
        private readonly ILogger<AzureEventHubConnector> _logger;

        private readonly IClockService _clockService;

        private readonly PartitionedBuffer<AzureEventHubConnectorJobData, EventData> _buffer;

        public AzureEventHubConnector(ILogger<AzureEventHubConnector> logger, IClockService clockService)
            : base(AzureEventHubConstants.ProviderId)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clockService = clockService;

            // Buffer<T>.Add blocks the caller (a RabbitMQ message handler) until its item is flushed, so the
            // number of concurrently in-flight adds is capped by RabbitMQ's prefetch count (default 50) - not by
            // this buffer. This value is the buffer's *ceiling*: it sizes Buffer<T>'s internal semaphores/arrays
            // once at construction and never grows past it, though Buffer<T>.AutoAdjustMaxSize can shrink the
            // effective per-flush trigger below it (and back up) at runtime under sustained low throughput - see
            // that method. The ceiling itself must stay at/below the prefetch count: if it could exceed it, the
            // buffer could never fill by count (no 51st concurrent caller could ever arrive) and every flush would
            // fall back to the 10s idle timeout, collapsing throughput. BatchSize (see Flush) only subdivides
            // what this buffer already collected, so it can be configured independently without this risk.
            _buffer = new PartitionedBuffer<AzureEventHubConnectorJobData, EventData>(
                AzureEventHubConstants.DefaultFlushSize,
                AzureEventHubConstants.FlushTimeoutMilliseconds,
                Flush);

            _logger.LogInformation("[AzureEventHub] AzureEventHubConnector Initialized");
        }

        ~AzureEventHubConnector()
        {
            _buffer.Dispose();
        }

        private readonly Dictionary<AzureEventHubConnectorJobData, EventHubProducerClient> _cache = new Dictionary<AzureEventHubConnectorJobData, EventHubProducerClient>();

        private async Task Flush(AzureEventHubConnectorJobData configuration, EventData[] eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.Length == 0)
            {
                return;
            }

            if (!_cache.TryGetValue(configuration, out var client))
            {
                _cache.Add(configuration, client = new EventHubProducerClient(configuration.ConnectionString, configuration.Name));
            }

            try
            {
                if (configuration.CombineMessages)
                {
                    // Each chunk must be its own Event Hub batch. Collecting every chunk into one array and
                    // handing it to a single SendAsync call would pack multiple already-near-the-cap combined
                    // messages into one physical batch, which can exceed Event Hub's real per-batch size limit
                    // even though every individual chunk stayed under MaxCombinedMessageBytes.
                    foreach (var chunk in Chunk(eventData, configuration.BatchSize))
                    {
                        await client.SendAsync(new[] { CombineEventData(chunk) });
                    }
                }
                else
                {
                    await client.SendAsync(eventData);
                }
            }
            catch
            {
                _cache.Remove(configuration);
                throw;
            }
        }

        // Subdivides one flush's worth of records - at most AzureEventHubConstants.DefaultFlushSize, though it
        // may be fewer if Buffer<T>.AutoAdjustMaxSize has shrunk the flush trigger under low throughput - into
        // groups of at most batchSize, further split if a group's combined byte size would exceed the Event
        // Hub's max message size. batchSize is clamped <= DefaultFlushSize by JobData, so it can only ever make
        // combined messages smaller than a full flush would allow, never larger; fewer items than batchSize
        // just yields one smaller group, which is a no-op for correctness.
        internal static IEnumerable<EventData[]> Chunk(EventData[] items, int batchSize)
        {
            var chunk = new List<EventData>(Math.Min(batchSize, items.Length));
            var chunkBytes = 0;

            foreach (var item in items)
            {
                var itemBytes = item.Body.ToArray().Length;

                if (chunk.Count > 0 && (chunk.Count >= batchSize || chunkBytes + itemBytes > AzureEventHubConstants.MaxCombinedMessageBytes))
                {
                    yield return chunk.ToArray();
                    chunk = new List<EventData>(Math.Min(batchSize, items.Length));
                    chunkBytes = 0;
                }

                chunk.Add(item);
                chunkBytes += itemBytes;
            }

            if (chunk.Count > 0)
            {
                yield return chunk.ToArray();
            }
        }

        // Combines the raw JSON bodies of multiple already-serialized EventData items into a single
        // message body, wrapped as { "count": N, "messages": [ ... ] }, without re-parsing each body.
        internal static EventData CombineEventData(EventData[] items)
        {
            var sb = new StringBuilder();
            sb.Append("{\"count\":").Append(items.Length).Append(",\"messages\":[");

            for (var i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(Encoding.UTF8.GetString(items[i].Body.ToArray()));
            }

            sb.Append("]}");

            return new EventData(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid connectorProviderDefinitionId, IReadOnlyCreateContainerModelV2 model)
        {
            await Task.FromResult(0);
        }

        public override async Task EmptyContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            await Task.FromResult(0);
        }

        public override async Task ArchiveContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            await Task.FromResult(0);
        }

        public override async Task RenameContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, string oldContainerName)
        {
            await Task.FromResult(0);
        }

        public override async Task RemoveContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            await Task.FromResult(0);
        }

        public override Task<string> GetValidMappingDestinationPropertyName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
        {
            // Strip non-alpha numeric characters
            var result = Regex.Replace(name, @"[^A-Za-z0-9]+", "");

            return Task.FromResult(result);
        }

        public override async Task<string> GetValidContainerName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
        {
            // Strip non-alpha numeric characters
            Uri uri;
            if (Uri.TryCreate(name, UriKind.Absolute, out uri))
            {
                return await Task.FromResult(uri.AbsolutePath);
            }
            else
            {
                return await Task.FromResult(name);
            }
        }

        public override async Task<IEnumerable<IConnectorContainer>> GetContainers(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            return await Task.FromResult(new List<IConnectorContainer>());
        }

        public override async Task<ConnectionVerificationResult> VerifyConnection(ExecutionContext executionContext, IReadOnlyDictionary<string, object> config)
        {
            try
            {
                var configuration = new AzureEventHubConnectorJobData(config.ToDictionary(x => x.Key, x => x.Value));

                await using (var client = new EventHubProducerClient(configuration.ConnectionString, configuration.Name))
                {
                    //this is to validate if it has a valid 'Event Hub Name'
                    await client.GetEventHubPropertiesAsync();
                    await client.CloseAsync();
                }

                return new ConnectionVerificationResult(true);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The connection string could not be parsed") || ex.Message.Contains("No such host is known") || ex.Message.Contains("InvalidSignature") || ex.Message.Contains("The connection string used for an Event Hub client must specify") || ex.Message.Contains("Index was outside the bounds of the array"))
                {
                    return new ConnectionVerificationResult(false, $"Invalid connection string: {ex.Message}");
                }

                return ex.Message.Contains("The path to an Event Hub may be specified as part of the connection string or as a separate value, but not both") ? new ConnectionVerificationResult(false, $"Invalid event hub name: {ex.Message}") : new ConnectionVerificationResult(false, ex.Message);
            }
        }

        public override Task VerifyExistingContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            return Task.FromResult(0);
        }

        public override async Task<SaveResult> StoreData(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, IReadOnlyConnectorEntityData connectorEntityData)
        {
            var providerDefinitionId = streamModel.ConnectorProviderDefinitionId!.Value;

            // matching output format of previous version of the connector
            var data = connectorEntityData.Properties.ToDictionary(x => x.Name, x => x.Value);
            data.Add("Id", connectorEntityData.EntityId);

            if (connectorEntityData.PersistInfo != null)
            {
                data.Add("PersistHash", connectorEntityData.PersistInfo.PersistHash);
            }

            if (connectorEntityData.OriginEntityCode != null)
            {
                data.Add("OriginEntityCode", connectorEntityData.OriginEntityCode.ToString());
            }

            if (connectorEntityData.EntityType != null)
            {
                data.Add("EntityType", connectorEntityData.EntityType.ToString());
            }
            data.Add("Codes", connectorEntityData.EntityCodes.Select(c => c.ToString()));
            // end match previous version of the connector

            if (connectorEntityData.OutgoingEdges.SafeEnumerate().Any())
            {
                data.Add("OutgoingEdges", connectorEntityData.OutgoingEdges);
            }

            if (connectorEntityData.IncomingEdges.SafeEnumerate().Any())
            {
                data.Add("IncomingEdges", connectorEntityData.IncomingEdges);
            }

            if (connectorEntityData.StreamMode == StreamMode.EventStream)
            {
                var dataWrapper = new Dictionary<string, object>
                {
                    { "TimeStamp", _clockService.Now },
                    { "Epoch", _clockService.Now.ToUnixTimeSeconds() },
                    { "VersionChangeType", connectorEntityData.ChangeType.ToString() },
                    { "Data", data }
                };

                data = dataWrapper;
            }
            else
            {
                data.Add("ChangeType", connectorEntityData.ChangeType.ToString());
            }

            var eventData = new EventData(Encoding.UTF8.GetBytes(JsonUtility.Serialize(data,
                new JsonSerializer
                {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.None, // don't want to expose our internal class names
                }))
            );

            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var configurations = new AzureEventHubConnectorJobData(config.Authentication.ToDictionary(x => x.Key, x => x.Value));

            try
            {
                await _buffer.Add(configurations, eventData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AzureEventHub] {message}", ex.Message);

                return SaveResult.ReQueue;
            }

            return new SaveResult(SaveResultState.Success);
        }

        public override Task<ConnectorLatestEntityPersistInfo> GetLatestEntityPersistInfo(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, Guid entityId)
        {
            throw new NotImplementedException();
        }

        public override Task<IAsyncEnumerable<ConnectorLatestEntityPersistInfo>> GetLatestEntityPersistInfos(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            throw new NotImplementedException();
        }

        public override IReadOnlyCollection<StreamMode> GetSupportedModes()
        {
            return new[]
            {
                StreamMode.EventStream,
            };
        }

        public virtual async Task<IConnectorConnectionV2> GetAuthenticationDetails(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            return await AuthenticationDetailsHelper.GetAuthenticationDetails(executionContext, providerDefinitionId);
        }
    }
}
