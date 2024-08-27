using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CluedIn.Connector.AzureEventHub;
using CluedIn.Core;
using CluedIn.Core.Crawling;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;
using CluedIn.Core.Webhooks;
using CluedIn.Providers.Models;
using Newtonsoft.Json;

namespace CluedIn.Connector.DataActivator
{
    public class DataActivatorConnectorProvider : ProviderBase, IExtendedProviderMetadata
    {
        /**********************************************************************************************************
         * CONSTRUCTORS
         **********************************************************************************************************/

        public DataActivatorConnectorProvider([NotNull] ApplicationContext appContext)
            : base(appContext, DataActivatorConstants.CreateProviderMetadata())
        {
           
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        public override async Task<CrawlJobData> GetCrawlJobData(
            ProviderUpdateContext context,
            IDictionary<string, object> configuration,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = new AzureEventHubConnectorJobData(configuration);

            return await Task.FromResult(result);
        }

        public override Task<bool> TestAuthentication(
            ProviderUpdateContext context,
            IDictionary<string, object> configuration,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            throw new NotImplementedException();
        }

        public override Task<ExpectedStatistics> FetchUnSyncedEntityStatistics(Core.ExecutionContext context, IDictionary<string, object> configuration, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            throw new NotImplementedException();
        }

        public override Task<IDictionary<string, object>> GetHelperConfiguration(
            ProviderUpdateContext context,
            [NotNull] CrawlJobData jobData,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            if (jobData == null)
                throw new ArgumentNullException(nameof(jobData));

            if (jobData is AzureEventHubConnectorJobData result)
            {
                return Task.FromResult(result.ToDictionary());
            }

            throw new InvalidOperationException($"Unexpected data type for AzureEventConnectorJobData, {jobData.GetType()}");
        }

        public override Task<IDictionary<string, object>> GetHelperConfiguration(
            ProviderUpdateContext context,
            CrawlJobData jobData,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId,
            string folderId)
        {
            return GetHelperConfiguration(context, jobData, organizationId, userId, providerDefinitionId);
        }

        public override Task<AccountInformation> GetAccountInformation(Core.ExecutionContext context, [NotNull] CrawlJobData jobData, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            if (jobData == null)
            {
                throw new ArgumentNullException(nameof(jobData));
            }

            if (!(jobData is AzureEventHubConnectorJobData result))
            {
                throw new ArgumentException(
                    "Wrong CrawlJobData type", nameof(jobData));
            }

            var accountId = $"{result.Name}.{result.ConnectionString}";

            return Task.FromResult(new AccountInformation(accountId, $"{accountId}"));
        }

        public override string Schedule(DateTimeOffset relativeDateTime, bool webHooksEnabled)
        {
            return $"{relativeDateTime.Minute} 0/23 * * *";
        }

        public override Task<IEnumerable<WebHookSignature>> CreateWebHook(Core.ExecutionContext context, [NotNull] CrawlJobData jobData, [NotNull] IWebhookDefinition webhookDefinition, [NotNull] IDictionary<string, object> config)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));
            if (webhookDefinition == null) throw new ArgumentNullException(nameof(webhookDefinition));
            if (config == null) throw new ArgumentNullException(nameof(config));

            throw new NotImplementedException();
        }

        public override Task<IEnumerable<WebhookDefinition>> GetWebHooks(Core.ExecutionContext context)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteWebHook(Core.ExecutionContext context, [NotNull] CrawlJobData jobData, [NotNull] IWebhookDefinition webhookDefinition)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));
            if (webhookDefinition == null) throw new ArgumentNullException(nameof(webhookDefinition));

            throw new NotImplementedException();
        }

        public override IEnumerable<string> WebhookManagementEndpoints([NotNull] IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            // TODO should ids also be checked for being empty ?

            throw new NotImplementedException();
        }

        public override Task<CrawlLimit> GetRemainingApiAllowance(Core.ExecutionContext context, [NotNull] CrawlJobData jobData, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));

            //TODO what the hell is this?
            //There is no limit set, so you can pull as often and as much as you want.
            return Task.FromResult(new CrawlLimit(-1, TimeSpan.Zero));
        }

        public string Icon => DataActivatorConstants.IconResourceName;
        public string Domain { get; } = DataActivatorConstants.Uri;
        public string About { get; } = DataActivatorConstants.ConnectorDescription;
        public AuthMethods AuthMethods => DataActivatorConstants.AuthMethods;
        public IEnumerable<Control> Properties { get; } = DataActivatorConstants.Properties;
        public string ServiceType { get; } = JsonConvert.SerializeObject(DataActivatorConstants.ServiceType);
        public string Aliases { get; } = JsonConvert.SerializeObject(DataActivatorConstants.Aliases);
        public Guide Guide { get; set; } = new Guide {
            Instructions = DataActivatorConstants.Instructions,
            Value = new List<string> { DataActivatorConstants.ConnectorDescription },
            Details = DataActivatorConstants.Details

        };

        public string Details { get; set; } = DataActivatorConstants.Details;
        public string Category { get; set; } = DataActivatorConstants.Category;
        public new IntegrationType Type { get; set; } = DataActivatorConstants.Type;
    }
}
