namespace AzureServer.Core.Services.Container.Models;

public class ContainerGroupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? State { get; set; }
    public string? IpAddress { get; set; }
    public string? OsType { get; set; }
    public string? RestartPolicy { get; set; }
    public List<ContainerInstanceDto> Containers { get; set; } = [];
    public Dictionary<string, string>? Tags { get; set; }
    public string? Fqdn { get; set; }
    public int? Port { get; set; }
    public DateTime? CreatedOn { get; set; }
}

public class ContainerInstanceDto
{
    public string? Name { get; set; }
    public string? Image { get; set; }
    public string? State { get; set; }
    public double? CpuCores { get; set; }
    public double? MemoryInGb { get; set; }
    public List<int>? Ports { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public List<string>? Command { get; set; }
    public string? RestartCount { get; set; }
}
