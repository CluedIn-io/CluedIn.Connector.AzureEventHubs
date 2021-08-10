using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.Connector.AzureEventHub
{
    public class AzureEventHubConnectorJobData : CrawlJobData
    {
        public AzureEventHubConnectorJobData(IDictionary<string, object> configuration)
        {
            if (configuration == null)
            {
                return;
            }

            ConnectionString = GetValue<string>(configuration, AzureEventHubConstants.KeyName.ConnectionString);
            Name = GetValue<string>(configuration, AzureEventHubConstants.KeyName.Name);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
                { AzureEventHubConstants.KeyName.Name, Name }
            };
        }

        public string ConnectionString { get; set; }

        public string Name { get; set; }
    }
}
