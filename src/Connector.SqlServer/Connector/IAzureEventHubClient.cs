using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CluedIn.Core.Connectors;
using Microsoft.Data.SqlClient;

namespace CluedIn.Connector.AzureEventHub.Connector
{
    public interface IAzureEventHubClient
    {
        Task ExecuteCommandAsync(IConnectorConnection config, string commandText, IList<SqlParameter> param = null);
    }
}
