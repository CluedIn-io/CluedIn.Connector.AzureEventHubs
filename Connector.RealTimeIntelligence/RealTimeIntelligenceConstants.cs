using System;
using System.Collections.Generic;
using CluedIn.Core.Net.Mail;
using CluedIn.Core.Providers;

namespace CluedIn.Connector.RealTimeIntelligence
{
    public class RealTimeIntelligenceConstants
    {
        public struct KeyName
        {
            public const string ConnectionString = "connectinString";
            public const string Name = "name";
        }

        public const string ConnectorName = "RealTimeIntelligenceConnector";
        public const string ConnectorComponentName = "RealTimeIntelligenceConnector";
        public const string ConnectorDescription = "Supports publishing of data to Real Time Intelligence in Microsoft Fabric.";
        public const string Uri = "https://learn.microsoft.com/en-us/fabric/data-activator/data-activator-introduction";

        public static readonly Guid ProviderId = Guid.Parse("{4AFB651C-36DA-47F3-B461-021999A9D634}");
        public const string ProviderName = "Real Time Intelligence Connector";
        public const bool SupportsConfiguration = false;
        public const bool SupportsWebHooks = false;
        public const bool SupportsAutomaticWebhookCreation = false;
        public const bool RequiresAppInstall = false;
        public const string AppInstallUrl = null;
        public const string ReAuthEndpoint = null;

        public static IList<string> ServiceType = new List<string> { "Connector" };
        public static IList<string> Aliases = new List<string> { "RealTimeIntelligenceConnector" };
        public const string IconResourceName = "Resources.synapse-real-time-intelligence-icon.png";
        public const string Instructions = "Provide authentication instructions here, if applicable";
        public const IntegrationType Type = IntegrationType.Connector;
        public const string Category = "Connectivity";
        public const string Details = "Supports publishing of data to Real Time Intelligence.";

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
