namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to register multiple passwords at once
/// </summary>
public class BulkRegisterPasswordsRequest
{
    /// <summary>
    /// Dictionary mapping file paths to their passwords
    /// </summary>
    public Dictionary<string, string> PasswordMap { get; set; } = new();
}
