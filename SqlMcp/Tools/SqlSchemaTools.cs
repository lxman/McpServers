using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlMcp.Common;
using SqlMcp.Models;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlSchemaTools(
    ISchemaInspector schemaInspector,
    ILogger<SqlSchemaTools> logger)
{
    [McpServerTool, DisplayName("list_tables")]
    [Description("List all tables in database. See schema-inspection/list_tables.md")]
    public async Task<string> ListTables(
        [Description("Connection name")] string connectionName)
    {
        try
        {
            IEnumerable<TableInfo> tables = await schemaInspector.GetTablesAsync(connectionName);
            return JsonSerializer.Serialize(new { success = true, tables }, SerializerOptions.JsonOptionsIndented);
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
        [Description("Connection name")] string connectionName,
        [Description("Table name")] string tableName)
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
        [Description("Connection name")] string connectionName,
        [Description("Table name")] string tableName)
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
        [Description("Connection name")] string connectionName,
        [Description("Table name")] string tableName)
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
