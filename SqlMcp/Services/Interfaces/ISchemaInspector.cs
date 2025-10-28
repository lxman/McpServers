using SqlMcp.Models;

namespace SqlMcp.Services.Interfaces;

public interface ISchemaInspector
{
    Task<IEnumerable<TableInfo>> GetTablesAsync(string connectionName);
    Task<TableSchema> GetTableSchemaAsync(string connectionName, string tableName);
    Task<IEnumerable<IndexInfo>> GetIndexesAsync(string connectionName, string tableName);
    Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync(string connectionName, string tableName);
}
