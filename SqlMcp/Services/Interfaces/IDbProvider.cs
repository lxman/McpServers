using System.Data;

namespace SqlMcp.Services.Interfaces;

public interface IDbProvider
{
    string ProviderName { get; }
    IDbConnection CreateConnection(string connectionString);
    string GetTablesQuery();
    string GetColumnsQuery(string tableName);
    string GetIndexesQuery(string tableName);
    string GetForeignKeysQuery(string tableName);
    bool SupportsTransactions { get; }
    bool SupportsSchemas { get; }
}
