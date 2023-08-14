using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CluedIn.Connector.AzureEventHub.Services;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using CluedIn.Core.Processing;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubConnector : ConnectorBaseV2
    {
        private readonly ILogger<AzureEventHubConnector> _logger;

        private readonly IAzureEventHubClient _client;

        private readonly IClockService _clockService;

        public AzureEventHubConnector(ILogger<AzureEventHubConnector> logger, IAzureEventHubClient client, IClockService clockService)
            : base(AzureEventHubConstants.ProviderId)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _clockService = clockService;

            _logger.LogInformation("[AzureEventHub] AzureEventHubConnector Initialized");
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
            var connectionBase = new ConnectorConnectionBase
            {
                Authentication = config.ToDictionary(x => x.Key, x => x.Value)
            };

            await using (var client = _client.GetEventHubClient(connectionBase))
            {
                //this is to validate if it has a valid 'Event Hub Name'
                await client.GetEventHubPropertiesAsync();
                await client.CloseAsync();
            }

            return new ConnectionVerificationResult(true);
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
                    { "VersionChangeType", connectorEntityData.ChangeType.ToString() },
                    { "Data", data }
                };

                data = dataWrapper;
            }
            else
            {
                data.Add("ChangeType", connectorEntityData.ChangeType.ToString());
            }

            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

            var connectionBase = new ConnectorConnectionBase
            {
                Authentication = config.Authentication.ToDictionary(x => x.Key, x => x.Value)
            };

            await _client.QueueData(connectionBase, data);

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
                StreamMode.Sync, // the old version had this even though it doesn't actually support a sync mode
            };
        }

        public virtual async Task<IConnectorConnectionV2> GetAuthenticationDetails(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            return await AuthenticationDetailsHelper.GetAuthenticationDetails(executionContext, providerDefinitionId);
        }
    }
}
