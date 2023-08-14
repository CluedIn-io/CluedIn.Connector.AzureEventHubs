using System;
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

        public string ConnectionString { get; set; }

        public string Name { get; set; }

        protected bool Equals(AzureEventHubConnectorJobData other)
        {
            return ConnectionString == other.ConnectionString && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((AzureEventHubConnectorJobData)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConnectionString, Name);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
                { AzureEventHubConstants.KeyName.Name, Name }
            };
        }
    }
}
