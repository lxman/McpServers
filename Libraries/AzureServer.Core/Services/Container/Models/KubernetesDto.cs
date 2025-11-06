namespace AzureServer.Core.Services.Container.Models;

public class KubernetesClusterDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? KubernetesVersion { get; set; }
    public string? Fqdn { get; set; }
    public string? ProvisioningState { get; set; }
    public string? PowerState { get; set; }
    public string? NetworkProfile { get; set; }
    public string? DnsPrefix { get; set; }
    public int? NodeCount { get; set; }
    public List<NodePoolDto>? NodePools { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DateTime? CreatedOn { get; set; }
    public bool? EnableRBAC { get; set; }
    public string? SkuName { get; set; }
    public string? SkuTier { get; set; }
}

public class NodePoolDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? VmSize { get; set; }
    public int? Count { get; set; }
    public string? OsType { get; set; }
    public string? OsDiskSizeGB { get; set; }
    public int? MinCount { get; set; }
    public int? MaxCount { get; set; }
    public bool? EnableAutoScaling { get; set; }
    public string? Mode { get; set; }
    public string? OrchestratorVersion { get; set; }
    public string? NodeImageVersion { get; set; }
    public string? PowerState { get; set; }
    public string? ProvisioningState { get; set; }
}

public class KubernetesNamespaceDto
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedOn { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public Dictionary<string, string>? Annotations { get; set; }
}

public class KubernetesDeploymentDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public int? Replicas { get; set; }
    public int? AvailableReplicas { get; set; }
    public string? Image { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedOn { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
}

public class KubernetesServiceDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? Type { get; set; }
    public string? ClusterIP { get; set; }
    public string? ExternalIP { get; set; }
    public List<int>? Ports { get; set; }
    public Dictionary<string, string>? Selector { get; set; }
    public DateTime? CreatedOn { get; set; }
}

public class KubernetesPodDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? Status { get; set; }
    public string? NodeName { get; set; }
    public string? PodIP { get; set; }
    public List<ContainerStatusDto>? Containers { get; set; }
    public DateTime? CreatedOn { get; set; }
    public int? RestartCount { get; set; }
}

public class ContainerStatusDto
{
    public string? Name { get; set; }
    public string? Image { get; set; }
    public bool? Ready { get; set; }
    public int? RestartCount { get; set; }
    public string? State { get; set; }
}
