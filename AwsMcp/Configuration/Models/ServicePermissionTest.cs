namespace AwsMcp.Configuration.Models;

/// <summary>
/// Service permission test result
/// </summary>
public class ServicePermissionTest
{
    public string ServiceName { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
    public string TestedOperation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? SuggestedAction { get; set; }
}