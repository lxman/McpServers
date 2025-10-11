namespace AzureServer.Services.Storage.Models;

public class BlobContainerDto
{
    public string Name { get; set; } = string.Empty;
    public string StorageAccountName { get; set; } = string.Empty;
    public string? PublicAccess { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LeaseStatus { get; set; }
    public string? LeaseState { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public bool? HasImmutabilityPolicy { get; set; }
    public bool? HasLegalHold { get; set; }
    public string? DefaultEncryptionScope { get; set; }
}