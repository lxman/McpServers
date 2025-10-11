namespace AzureServer.Services.Storage.Models;

public class FilePropertiesDto
{
    public required string Name { get; set; }
    public required string ShareName { get; set; }
    public string? Path { get; set; }
    public long ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentHash { get; set; }
    public string? ETag { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsServerEncrypted { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
