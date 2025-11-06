namespace AzureServer.Core.Services.Storage.Models;

public class BlobPropertiesDto
{
    public string Name { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentHash { get; set; }
    public string? ETag { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime? LastAccessedOn { get; set; }
    public string? LeaseStatus { get; set; }
    public string? LeaseState { get; set; }
    public string? LeaseDuration { get; set; }
    public string? AccessTier { get; set; }
    public bool IsServerEncrypted { get; set; }
    public string? EncryptionKeySha256 { get; set; }
    public string? EncryptionScope { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public Dictionary<string, string>? Tags { get; set; }
}