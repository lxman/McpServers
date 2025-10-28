using System.Data;

namespace SqlMcp.Models;

public class TransactionContext
{
    public required string TransactionId { get; set; }
    public required string ConnectionName { get; set; }
    public required IDbTransaction Transaction { get; set; }
    public required IDbConnection Connection { get; set; }
    public DateTime StartTime { get; set; }
}
