namespace AzureServer.Core.Services.Storage.Models;

public class SasTokenDto
{
    public string Uri { get; set; } = string.Empty;
    public string SasToken { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string StorageAccountName { get; set; } = string.Empty;
}