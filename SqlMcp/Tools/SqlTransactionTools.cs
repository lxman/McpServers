using System.ComponentModel;
using System.Text.Json;
using Mcp.Common;
using Mcp.Common.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlServer.Core.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlTransactionTools(
    ITransactionManager transactionManager,
    ILogger<SqlTransactionTools> logger)
{
    [McpServerTool, DisplayName("begin_transaction")]
    [Description("Begin database transaction. See transaction-management/begin_transaction.md")]
    public async Task<string> BeginTransaction(
        string connectionName,
        string? isolationLevel = null)
    {
        try
        {
            string transactionId = await transactionManager.BeginTransactionAsync(connectionName, isolationLevel);
            return JsonSerializer.Serialize(new { success = true, transactionId }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to begin transaction");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("commit_transaction")]
    [Description("Commit transaction. See transaction-management/commit_transaction.md")]
    public async Task<string> CommitTransaction(
        string transactionId)
    {
        try
        {
            await transactionManager.CommitTransactionAsync(transactionId);
            return JsonSerializer.Serialize(new { success = true, transactionId, message = "Transaction committed" }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to commit transaction");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("rollback_transaction")]
    [Description("Rollback transaction. See transaction-management/rollback_transaction.md")]
    public async Task<string> RollbackTransaction(
        string transactionId)
    {
        try
        {
            await transactionManager.RollbackTransactionAsync(transactionId);
            return JsonSerializer.Serialize(new { success = true, transactionId, message = "Transaction rolled back" }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rollback transaction");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_active_transactions")]
    [Description("List active transactions. See transaction-management/list_active_transactions.md")]
    public string ListActiveTransactions()
    {
        try
        {
            IEnumerable<string> transactions = transactionManager.GetActiveTransactions();
            return JsonSerializer.Serialize(new { success = true, transactions }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list transactions");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
