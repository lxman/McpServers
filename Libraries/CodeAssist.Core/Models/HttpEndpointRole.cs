namespace CodeAssist.Core.Models;

/// <summary>
/// Whether an HTTP endpoint is served (backend) or consumed (client).
/// </summary>
public enum HttpEndpointRole
{
    Server,
    Client
}
