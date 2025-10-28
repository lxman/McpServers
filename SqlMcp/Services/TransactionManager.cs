using System.Data;
using Microsoft.Extensions.Logging;
using SqlMcp.Models;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Services;

public class TransactionManager : ITransactionManager
{
    private readonly Dictionary<string, TransactionContext> _transactions = new();
    private readonly IConnectionManager _connectionManager;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<TransactionManager> _logger;

    public TransactionManager(
        IConnectionManager connectionManager,
        IAuditLogger auditLogger,
        ILogger<TransactionManager> logger)
    {
        _connectionManager = connectionManager;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<string> BeginTransactionAsync(string connectionName, string? isolationLevel = null)
    {
        try
        {
            IDbConnection connection = await _connectionManager.GetConnectionAsync(connectionName);
            IDbTransaction transaction = isolationLevel switch
            {
                "ReadUncommitted" => connection.BeginTransaction(IsolationLevel.ReadUncommitted),
                "ReadCommitted" => connection.BeginTransaction(IsolationLevel.ReadCommitted),
                "RepeatableRead" => connection.BeginTransaction(IsolationLevel.RepeatableRead),
                "Serializable" => connection.BeginTransaction(IsolationLevel.Serializable),
                _ => connection.BeginTransaction()
            };

            var transactionId = Guid.NewGuid().ToString();
            _transactions[transactionId] = new TransactionContext
            {
                TransactionId = transactionId,
                ConnectionName = connectionName,
                Transaction = transaction,
                Connection = connection,
                StartTime = DateTime.UtcNow
            };

            await _auditLogger.LogTransactionAsync(transactionId, "Begin");
            _logger.LogInformation("Transaction started: {TransactionId}", transactionId);
            return transactionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to begin transaction for connection: {ConnectionName}", connectionName);
            throw;
        }
    }

    public async Task CommitTransactionAsync(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out TransactionContext? context))
            throw new ArgumentException($"Transaction '{transactionId}' not found");

        try
        {
            context.Transaction.Commit();
            await _auditLogger.LogTransactionAsync(transactionId, "Commit");
            _logger.LogInformation("Transaction committed: {TransactionId}", transactionId);
        }
        finally
        {
            context.Transaction.Dispose();
            _transactions.Remove(transactionId);
        }
    }

    public async Task RollbackTransactionAsync(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out TransactionContext? context))
            throw new ArgumentException($"Transaction '{transactionId}' not found");

        try
        {
            context.Transaction.Rollback();
            await _auditLogger.LogTransactionAsync(transactionId, "Rollback");
            _logger.LogInformation("Transaction rolled back: {TransactionId}", transactionId);
        }
        finally
        {
            context.Transaction.Dispose();
            _transactions.Remove(transactionId);
        }
    }

    public IEnumerable<string> GetActiveTransactions()
    {
        return _transactions.Keys;
    }
}
