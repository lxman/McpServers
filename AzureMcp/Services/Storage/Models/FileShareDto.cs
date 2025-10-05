namespace AzureMcp.Services.Storage.Models;

public class FileShareDto
{
    public required string Name { get; set; }
    public required string StorageAccountName { get; set; }
    public long? QuotaInGB { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ETag { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public string? AccessTier { get; set; }
    public bool? EnabledProtocols { get; set; }
}
