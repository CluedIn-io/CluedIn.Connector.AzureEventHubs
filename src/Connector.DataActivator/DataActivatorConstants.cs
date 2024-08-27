using System;
using System.Collections.Generic;
using CluedIn.Core.Net.Mail;
using CluedIn.Core.Providers;

namespace CluedIn.Connector.DataActivator
{
    public class DataActivatorConstants
    {
        public struct KeyName
        {
            public const string ConnectionString = "connectinString";
            public const string Name = "name";
        }

        public const string ConnectorName = "DataActivatorConnector";
        public const string ConnectorComponentName = "DataActivatorConnector";
        public const string ConnectorDescription = "Supports publishing of data to Data Activator in Microsoft Fabric.";
        public const string Uri = "https://learn.microsoft.com/en-us/fabric/data-activator/data-activator-introduction";

        public static readonly Guid ProviderId = Guid.Parse("{9CFE477C-6896-4B4C-9D62-F3B07317BCB5}");
        public const string ProviderName = "Data Activator Connector";
        public const bool SupportsConfiguration = false;
        public const bool SupportsWebHooks = false;
        public const bool SupportsAutomaticWebhookCreation = false;
        public const bool RequiresAppInstall = false;
        public const string AppInstallUrl = null;
        public const string ReAuthEndpoint = null;

        public static IList<string> ServiceType = new List<string> { "Connector" };
        public static IList<string> Aliases = new List<string> { "DataActivatorConnector" };
        public const string IconResourceName = "Resources.dataactivator.png";
        public const string Instructions = "Provide authentication instructions here, if applicable";
        public const IntegrationType Type = IntegrationType.Connector;
        public const string Category = "Connectivity";
        public const string Details = "Supports publishing of data to Data Activator.";

        public static AuthMethods AuthMethods = new AuthMethods
        {
            token = new Control[]
            {
                new Control
                {
                    name = KeyName.ConnectionString,
                    displayName = "Connection String",
                    type = "input",
                    isRequired = true
                },
                new Control
                {
                    name = KeyName.Name,
                    displayName = "Name",
                    type = "input",
                    isRequired = true
                }
            }
        };

        public static IEnumerable<Control> Properties = new List<Control>
        {

        };

        public static readonly ComponentEmailDetails ComponentEmailDetails = new ComponentEmailDetails {
            Features = new Dictionary<string, string>
            {
                                       { "Connectivity",        "Expenses and Invoices against customers" }
                                   },
            Icon = ProviderIconFactory.CreateConnectorUri(ProviderId),
            ProviderName = ProviderName,
            ProviderId = ProviderId,
            Webhooks = SupportsWebHooks
        };

        public static IProviderMetadata CreateProviderMetadata()
        {
            return new ProviderMetadata {
                Id = ProviderId,
                ComponentName = ConnectorName,
                Name = ProviderName,
                Type = "Connector",
                SupportsConfiguration = SupportsConfiguration,
                SupportsWebHooks = SupportsWebHooks,
                SupportsAutomaticWebhookCreation = SupportsAutomaticWebhookCreation,
                RequiresAppInstall = RequiresAppInstall,
                AppInstallUrl = AppInstallUrl,
                ReAuthEndpoint = ReAuthEndpoint,
                ComponentEmailDetails = ComponentEmailDetails
            };
        }
    }
}
