using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlServer.Core.Common;
using SqlServer.Core.Models;
using SqlServer.Core.Services;
using SqlServer.Core.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlSchemaTools(
    ISchemaInspector schemaInspector,
    ResponseSizeGuard responseSizeGuard,
    ILogger<SqlSchemaTools> logger)
{
    [McpServerTool, DisplayName("list_tables")]
    [Description("List all tables in database. See schema-inspection/list_tables.md")]
    public async Task<string> ListTables(
        string connectionName)
    {
        try
        {
            IEnumerable<TableInfo> tables = await schemaInspector.GetTablesAsync(connectionName);
            var responseObject = new { success = true, tables };

            // Check response size before returning
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckResponseSize(responseObject, "list_tables");

            if (!sizeCheck.IsWithinLimit)
            {
                int tableCount = tables.Count();
                return responseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Database contains {tableCount} tables, resulting in {sizeCheck.EstimatedTokens:N0} estimated tokens.",
                    "Try these workarounds:\n" +
                    "  1. Query specific tables by name using get_table_schema\n" +
                    "  2. Use SQL to filter tables: SELECT name FROM sys.tables WHERE name LIKE 'prefix%'\n" +
                    "  3. Break down by schema if database supports it\n" +
                    "  4. Get table count first: SELECT COUNT(*) FROM information_schema.tables",
                    new {
                        totalTables = tableCount
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list tables");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_table_schema")]
    [Description("Get table schema details. See schema-inspection/get_table_schema.md")]
    public async Task<string> GetTableSchema(
        string connectionName,
        string tableName)
    {
        try
        {
            TableSchema schema = await schemaInspector.GetTableSchemaAsync(connectionName, tableName);
            return JsonSerializer.Serialize(new { success = true, schema }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get table schema");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_table_indexes")]
    [Description("Get table indexes. See schema-inspection/get_table_indexes.md")]
    public async Task<string> GetTableIndexes(
        string connectionName,
        string tableName)
    {
        try
        {
            IEnumerable<IndexInfo> indexes = await schemaInspector.GetIndexesAsync(connectionName, tableName);
            return JsonSerializer.Serialize(new { success = true, indexes }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get table indexes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_foreign_keys")]
    [Description("Get table foreign keys. See schema-inspection/get_foreign_keys.md")]
    public async Task<string> GetForeignKeys(
        string connectionName,
        string tableName)
    {
        try
        {
            IEnumerable<ForeignKeyInfo> foreignKeys = await schemaInspector.GetForeignKeysAsync(connectionName, tableName);
            return JsonSerializer.Serialize(new { success = true, foreignKeys }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get foreign keys");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
