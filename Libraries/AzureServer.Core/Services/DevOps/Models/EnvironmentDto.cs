namespace AzureServer.Core.Services.DevOps.Models;

public class EnvironmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Rank { get; set; }
}