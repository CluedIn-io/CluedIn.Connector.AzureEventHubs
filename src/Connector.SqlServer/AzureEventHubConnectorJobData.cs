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
            CombineMessages = GetBool(configuration, AzureEventHubConstants.KeyName.CombineMessages, false);

            // Clamped to DefaultFlushSize, the buffer's ceiling (a single flush can never exceed it, though it
            // may be less - see AzureEventHubConnector's constructor comment and Buffer.AutoAdjustMaxSize), so
            // batching can only make combined messages smaller than the buffer would otherwise send, never larger.
            BatchSize = Math.Clamp(
                GetPositiveInt(configuration, AzureEventHubConstants.KeyName.BatchSize, AzureEventHubConstants.DefaultBatchSize),
                AzureEventHubConstants.MinBatchSize,
                AzureEventHubConstants.DefaultFlushSize);
        }

        public string ConnectionString { get; set; }

        public string Name { get; set; }

        public bool CombineMessages { get; set; }

        public int BatchSize { get; set; }

        // Deliberately not the base class's GetValue<T>: that helper throws on a value it can't convert (e.g. an
        // empty or non-numeric string), whereas these two need to fall back to defaultValue on anything they
        // can't parse - the exact format the UI posts these two fields in wasn't confirmed when they were added.
        private static bool TryGetRawConfigValue(IDictionary<string, object> configuration, string key, out object raw)
        {
            return configuration.TryGetValue(key, out raw) && raw != null;
        }

        private static bool GetBool(IDictionary<string, object> configuration, string key, bool defaultValue)
        {
            if (!TryGetRawConfigValue(configuration, key, out var raw))
            {
                return defaultValue;
            }

            if (raw is bool b)
            {
                return b;
            }

            return bool.TryParse(raw.ToString(), out var parsed) ? parsed : defaultValue;
        }

        private static int GetPositiveInt(IDictionary<string, object> configuration, string key, int defaultValue)
        {
            if (!TryGetRawConfigValue(configuration, key, out var raw))
            {
                return defaultValue;
            }

            if (raw is int i && i > 0)
            {
                return i;
            }

            return int.TryParse(raw.ToString(), out var parsed) && parsed > 0 ? parsed : defaultValue;
        }

        protected bool Equals(AzureEventHubConnectorJobData other)
        {
            return ConnectionString == other.ConnectionString
                && Name == other.Name
                && CombineMessages == other.CombineMessages
                && BatchSize == other.BatchSize;
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
            return HashCode.Combine(ConnectionString, Name, CombineMessages, BatchSize);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { AzureEventHubConstants.KeyName.ConnectionString, ConnectionString },
                { AzureEventHubConstants.KeyName.Name, Name },
                { AzureEventHubConstants.KeyName.CombineMessages, CombineMessages },
                { AzureEventHubConstants.KeyName.BatchSize, BatchSize }
            };
        }
    }
}
