using System.Data;
using Dapper;
using Mcp.Database.Core.Sql;
using Mcp.Database.Core.Sql.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServer.Core.Models;
using SqlServer.Core.Services.Interfaces;

namespace SqlServer.Core.Services;

public class SchemaInspector : ISchemaInspector
{
    private readonly SqlConnectionManager _connectionManager;
    private readonly SqlConfiguration _config;
    private readonly ILogger<SchemaInspector> _logger;
    private readonly Dictionary<string, IDbProvider> _providers = new();

    public SchemaInspector(
        SqlConnectionManager connectionManager,
        IOptions<SqlConfiguration> config,
        ILogger<SchemaInspector> logger)
    {
        _connectionManager = connectionManager;
        _config = config.Value;
        _logger = logger;
        InitializeProviders();
    }

    private void InitializeProviders()
    {
        _providers["SqlServer"] = new SqlServerProvider();
        _providers["Sqlite"] = new SqliteProvider();
    }

    private IDbProvider GetProvider(string connectionName)
    {
        ISqlProvider? sharedProvider = _connectionManager.GetProvider(connectionName)
            ?? throw new InvalidOperationException($"Provider for connection '{connectionName}' not found.");

        return _providers.TryGetValue(sharedProvider.ProviderName, out IDbProvider? provider)
            ? provider
            : throw new InvalidOperationException($"Schema provider '{sharedProvider.ProviderName}' not supported.");
    }

    public async Task<IEnumerable<TableInfo>> GetTablesAsync(string connectionName)
    {
        try
        {
            IDbConnection connection = _connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            IDbProvider provider = GetProvider(connectionName);
            string query = provider.GetTablesQuery();
            IEnumerable<TableInfo> tables = await connection.QueryAsync<TableInfo>(query);
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tables for connection: {ConnectionName}", connectionName);
            throw;
        }
    }

    public async Task<TableSchema> GetTableSchemaAsync(string connectionName, string tableName)
    {
        try
        {
            IDbConnection connection = _connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            IDbProvider provider = GetProvider(connectionName);
            string query = provider.GetColumnsQuery(tableName);
            IEnumerable<ColumnInfo> columns = await connection.QueryAsync<ColumnInfo>(query, new { tableName });

            return new TableSchema
            {
                TableName = tableName,
                Columns = columns
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema for table: {TableName}", tableName);
            throw;
        }
    }

    public async Task<IEnumerable<IndexInfo>> GetIndexesAsync(string connectionName, string tableName)
    {
        try
        {
            IDbConnection connection = _connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            IDbProvider provider = GetProvider(connectionName);
            string query = provider.GetIndexesQuery(tableName);

            // For SQLite, we need to handle the comma-separated Columns string
            if (provider.ProviderName == "Sqlite")
            {
                IEnumerable<SqliteIndexDto> rawResults = await connection.QueryAsync<SqliteIndexDto>(query, new { tableName });
                return rawResults.Select(r => new IndexInfo
                {
                    IndexName = r.IndexName,
                    TableName = r.TableName,
                    IsUnique = r.IsUnique,
                    IsPrimaryKey = r.IsPrimaryKey,
                    Columns = string.IsNullOrWhiteSpace(r.Columns)
                        ? Array.Empty<string>()
                        : r.Columns.Split(',').Select(c => c.Trim()).ToArray()
                });
            }

            IEnumerable<IndexInfo> indexes = await connection.QueryAsync<IndexInfo>(query, new { tableName });
            return indexes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get indexes for table: {TableName}", tableName);
            throw;
        }
    }

    // DTO for SQLite index query results where Columns is a comma-separated string
    private class SqliteIndexDto
    {
        public required string IndexName { get; set; }
        public required string TableName { get; set; }
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public required string Columns { get; set; }
    }

    public async Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync(string connectionName, string tableName)
    {
        try
        {
            IDbConnection connection = _connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            IDbProvider provider = GetProvider(connectionName);
            string query = provider.GetForeignKeysQuery(tableName);
            IEnumerable<ForeignKeyInfo> foreignKeys = await connection.QueryAsync<ForeignKeyInfo>(query, new { tableName });
            return foreignKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get foreign keys for table: {TableName}", tableName);
            throw;
        }
    }
}
