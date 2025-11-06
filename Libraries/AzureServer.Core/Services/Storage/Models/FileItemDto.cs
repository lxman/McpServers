namespace AzureServer.Core.Services.Storage.Models;

public class FileItemDto
{
    public required string Name { get; set; }
    public required string ShareName { get; set; }
    public required string StorageAccountName { get; set; }
    public string? Path { get; set; }
    public bool IsDirectory { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ETag { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
