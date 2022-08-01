using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using CluedIn.Core.DataStore;
using Microsoft.Extensions.Logging;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public class AzureEventHubConnector : ConnectorBase
    {
        private readonly ILogger<AzureEventHubConnector> _logger;
        private readonly IDictionary<Guid, IAzureEventHubClient> clients;

        public AzureEventHubConnector(IConfigurationRepository repo, ILogger<AzureEventHubConnector> logger) : base(repo)
        {
            System.Diagnostics.Debugger.Launch();
            ProviderId = AzureEventHubConstants.ProviderId;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            clients = new Dictionary<Guid, IAzureEventHubClient>();
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
            return await Task.FromResult(true);
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, IDictionary<string, object> config)
        {
            return await Task.FromResult(true);
        }

        public override async Task StoreData(ExecutionContext executionContext, Guid providerDefinitionId,
            string containerName, IDictionary<string, object> data)
        {
            var client = await GetClient(executionContext, providerDefinitionId, containerName);

            await client.QueueData(data);
        }
        
        public override async Task StoreEdgeData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, string originEntityCode, IEnumerable<string> edges)
        {
            await Task.FromResult(0);
        }

        private async Task<IAzureEventHubClient> GetClient(ExecutionContext executionContext, Guid providerDefinitionId, string containerName)
        {
            if (clients.TryGetValue(providerDefinitionId, out var client))
                return client;

            client = new AzureEventHubClient(executionContext, providerDefinitionId, containerName, await base.GetAuthenticationDetails(executionContext, providerDefinitionId));

            clients.Add(providerDefinitionId, client);

            return client;
        }
    }
}
