using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcp.Models;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Services;

public class SchemaInspector : ISchemaInspector
{
    private readonly IConnectionManager _connectionManager;
    private readonly SqlConfiguration _config;
    private readonly ILogger<SchemaInspector> _logger;
    private readonly Dictionary<string, IDbProvider> _providers = new();

    public SchemaInspector(
        IConnectionManager connectionManager,
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

    public async Task<IEnumerable<TableInfo>> GetTablesAsync(string connectionName)
    {
        try
        {
            IDbConnection connection = await _connectionManager.GetConnectionAsync(connectionName);
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
            IDbConnection connection = await _connectionManager.GetConnectionAsync(connectionName);
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
            IDbConnection connection = await _connectionManager.GetConnectionAsync(connectionName);
            IDbProvider provider = GetProvider(connectionName);
            string query = provider.GetIndexesQuery(tableName);

            // For SQLite, we need to handle the comma-separated Columns string
            if (provider.ProviderName == "Sqlite")
            {
                var rawResults = await connection.QueryAsync<SqliteIndexDto>(query, new { tableName });
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
            IDbConnection connection = await _connectionManager.GetConnectionAsync(connectionName);
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

    private IDbProvider GetProvider(string connectionName)
    {
        if (!_config.Connections.TryGetValue(connectionName, out ConnectionConfig? connConfig))
            throw new ArgumentException($"Connection '{connectionName}' not found");

        if (!_providers.TryGetValue(connConfig.Provider, out IDbProvider? provider))
            throw new NotSupportedException($"Provider '{connConfig.Provider}' not supported");

        return provider;
    }
}
