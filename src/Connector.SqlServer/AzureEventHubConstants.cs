using System;
using System.Collections.Generic;
using CluedIn.Core.Net.Mail;
using CluedIn.Core.Providers;

namespace CluedIn.Connector.AzureEventHub
{
    public class AzureEventHubConstants
    {
        public struct KeyName
        {
            public const string ConnectionString = "connectinString";
            public const string Name = "name";
        }

        public const string ConnectorName = "AzureEventConnector";
        public const string ConnectorComponentName = "AzureEventConnector";
        public const string ConnectorDescription = "Supports publishing of data to Azure Event Hubs.";
        public const string Uri = "https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-about";

        public static readonly Guid ProviderId = Guid.Parse("{F6178E19-6168-449C-B4B6-F9810E86C1C2}");
        public const string ProviderName = "Azure Event Hub Connector";
        public const bool SupportsConfiguration = false;
        public const bool SupportsWebHooks = false;
        public const bool SupportsAutomaticWebhookCreation = false;
        public const bool RequiresAppInstall = false;
        public const string AppInstallUrl = null;
        public const string ReAuthEndpoint = null;

        public static IList<string> ServiceType = new List<string> { "Connector" };
        public static IList<string> Aliases = new List<string> { "AzureEventConnector" };
        public const string IconResourceName = "Resources.Event-Hubs.svg";
        public const string Instructions = "Provide authentication instructions here, if applicable";
        public const IntegrationType Type = IntegrationType.Connector;
        public const string Category = "Connectivity";
        public const string Details = "Supports publishing of data to Azure Event Hubs.";

        public static AuthMethods AuthMethods = new AuthMethods
        {
            Token = new Control[]
            {
                new Control
                {
                    Name = KeyName.ConnectionString,
                    DisplayName = "Connection String",
                    Type = "input",
                    IsRequired = true,
                    ValidationRules = new List<Dictionary<string, string>>()
                    {
                        new() {
                            { "regex", "\\s" },
                            { "message", "Spaces are not allowed" }
                        }
                    },
                },
                new Control
                {
                    Name = KeyName.Name,
                    DisplayName = "Event Hub Name",
                    Type = "input",
                    IsRequired = true,
                    ValidationRules = new List<Dictionary<string, string>>()
                    {
                        new() {
                            { "regex", "\\s" },
                            { "message", "Spaces are not allowed" }
                        }
                    },
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
