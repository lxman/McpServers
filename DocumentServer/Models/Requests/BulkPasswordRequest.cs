namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to register multiple passwords from a JSON map
/// </summary>
/// <param name="PasswordMapJson">JSON map of file paths to passwords</param>
public record BulkPasswordRequest(string PasswordMapJson);
