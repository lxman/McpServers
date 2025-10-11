namespace AzureServer.Services.Storage.Models;

public class BlobItemDto
{
    public string Name { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string StorageAccountName { get; set; } = string.Empty;
    public string? BlobType { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentMd5 { get; set; }
    public string? CacheControl { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ETag { get; set; }
    public string? LeaseStatus { get; set; }
    public string? LeaseState { get; set; }
    public string? AccessTier { get; set; }
    public bool? IsServerEncrypted { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}