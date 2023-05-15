using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.DataStore;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubConnector : ConnectorBase, IConnectorStreamModeSupport
    {
        private readonly ILogger<AzureEventHubConnector> _logger;
        private readonly IAzureEventHubClient _client;
        private StreamMode StreamMode { get; set; } = StreamMode.Sync;

        public AzureEventHubConnector(IConfigurationRepository repo, ILogger<AzureEventHubConnector> logger, IAzureEventHubClient client) : base(repo)
        {
            ProviderId = AzureEventHubConstants.ProviderId;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));

            _logger.LogInformation("[AzureEventHub] AzureEventHubConnector Initialized");
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid providerDefinitionId, CreateContainerModel model)
        {
            await Task.FromResult(0);
        }

        public override async Task EmptyContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override async Task ArchiveContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override async Task RenameContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id, string newName)
        {
            await Task.FromResult(0);
        }

        public override async Task RemoveContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override Task<string> GetValidDataTypeName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
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

        public override async Task<IEnumerable<IConnectionDataType>> GetDataTypes(ExecutionContext executionContext, Guid providerDefinitionId, string containerId)
        {
            return await Task.FromResult(new List<IConnectionDataType>());
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            var _config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

            return await VerifyConnection(_config);
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, IDictionary<string, object> config)
        {
            var _config = new ConnectorConnectionBase
            {
                Authentication = config
            };

            return await VerifyConnection(_config);
        }

        private async Task<bool> VerifyConnection(IConnectorConnection config)
        {
            await using (var client = _client.GetEventHubClient(config))
            {
                //this is to validate if it has a valid 'Event Hub Name'
                var prop = await client.GetEventHubPropertiesAsync();
                await client.CloseAsync();
            }
            return await Task.FromResult(true);
        }

        public override async Task StoreData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, IDictionary<string, object> data)
        {
            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

            await _client.QueueData(config, data);
        }

        public async Task StoreData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, string correlationId,
            DateTimeOffset timestamp, VersionChangeType changeType, IDictionary<string, object> data)
        {
            if (StreamMode == StreamMode.EventStream)
            {
                var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

                var dataWrapper = new Dictionary<string, object>
                {
                    { "TimeStamp", timestamp },
                    { "VersionChangeType", changeType.ToString() },
                    { "CorrelationId", correlationId },
                    { "Data", data }
                };

                await _client.QueueData(config, dataWrapper);
            }
            else
            {
                await StoreData(executionContext, providerDefinitionId, containerName, data);
            }
        }

        public override async Task StoreEdgeData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, string originEntityCode, IEnumerable<string> edges)
        {
            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

            var data = new Dictionary<string, object>
            {
                { "OriginEntityCode", originEntityCode },
                { "Edges", edges }
            };

            await _client.QueueData(config, data);
        }

        public async Task StoreEdgeData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName,
            string originEntityCode, string correlationId, DateTimeOffset timestamp, VersionChangeType changeType,
            IEnumerable<string> edges)
        {
            if (StreamMode == StreamMode.EventStream)
            {
                var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

                var dataWrapper = new Dictionary<string, object>
                {
                    { "TimeStamp", timestamp },
                    { "VersionChangeType", changeType.ToString() },
                    { "CorrelationId", correlationId },
                    { "Edges", edges },
                };

                await _client.QueueData(config, dataWrapper);
            }
            else
            {
                await StoreEdgeData(executionContext, providerDefinitionId, containerName, originEntityCode, edges);
            }
        }

        public IList<StreamMode> GetSupportedModes()
        {
            return new List<StreamMode> { StreamMode.Sync, StreamMode.EventStream };
        }

        public void SetMode(StreamMode mode)
        {
            StreamMode = mode;
        }

        public Task<string> GetCorrelationId()
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }
    }
}
