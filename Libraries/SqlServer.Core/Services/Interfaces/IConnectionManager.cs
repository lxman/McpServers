using System.Data;

namespace SqlServer.Core.Services.Interfaces;

public interface IConnectionManager
{
    Task<IDbConnection> GetConnectionAsync(string connectionName);
    Task<bool> TestConnectionAsync(string connectionName);
    IEnumerable<string> GetAvailableConnections();
    Task CloseConnectionAsync(string connectionName);
    Task CloseAllConnectionsAsync();
}
