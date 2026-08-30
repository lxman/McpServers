namespace McpGateway.Supervision;

/// <summary>
/// Identifies one backend process. PoolKey is empty for a shared server and the calling client's
/// id for a per-client one — that difference is the whole isolation model.
/// </summary>
public readonly record struct BackendKey(string Server, string PoolKey)
{
    public override string ToString() =>
        PoolKey.Length == 0 ? Server : $"{Server}[{PoolKey}]";
}
