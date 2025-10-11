using Azure.ResourceManager.ContainerRegistry.Models;
using AzureServer.Services.Container.Models;

// ReSharper disable InconsistentNaming

namespace AzureServer.Services.Container;

public interface IContainerService
{
    // Container Instance Operations
    Task<IEnumerable<ContainerGroupDto>> ListContainerGroupsAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<ContainerGroupDto?> GetContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName);
    Task<ContainerGroupDto> CreateContainerGroupAsync(string subscriptionId, string resourceGroupName, ContainerGroupCreateRequest request);
    Task<bool> DeleteContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName);
    Task<ContainerGroupDto> RestartContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName);
    Task<ContainerGroupDto> StopContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName);
    Task<ContainerGroupDto> StartContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName);
    Task<string> GetContainerLogsAsync(string subscriptionId, string resourceGroupName, string containerGroupName, string containerName, int? tail = null);
    Task<ContainerExecResult> ExecuteCommandAsync(string subscriptionId, string resourceGroupName, string containerGroupName, string containerName, string command);

    // Container Registry Operations
    Task<IEnumerable<ContainerRegistryDto>> ListRegistriesAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<ContainerRegistryDto?> GetRegistryAsync(string subscriptionId, string resourceGroupName, string registryName);
    Task<ContainerRegistryDto> CreateRegistryAsync(string subscriptionId, string resourceGroupName, ContainerRegistryCreateRequest request);
    Task<bool> DeleteRegistryAsync(string subscriptionId, string resourceGroupName, string registryName);
    Task<RegistryCredentialsDto> GetRegistryCredentialsAsync(string subscriptionId, string resourceGroupName, string registryName);
    Task<RegistryCredentialsDto> RegenerateRegistryCredentialAsync(string subscriptionId, string resourceGroupName, string registryName, string passwordName);
    
    // Registry Repository and Image Operations
    Task<IEnumerable<ContainerRepositoryDto>> ListRepositoriesAsync(string subscriptionId, string resourceGroupName, string registryName);
    Task<IEnumerable<ContainerImageDto>> ListImagesAsync(string subscriptionId, string resourceGroupName, string registryName, string? repositoryName = null);
    Task<ContainerImageDto?> GetImageAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName, string tag);
    Task<bool> DeleteImageAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName, string tag);
    Task<bool> DeleteRepositoryAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName);
    
    // Registry Build Operations
    Task<BuildTaskDto> CreateBuildTaskAsync(string subscriptionId, string resourceGroupName, string registryName, BuildTaskCreateRequest request);
    Task<BuildRunDto> RunBuildTaskAsync(string subscriptionId, string resourceGroupName, string registryName, string buildTaskName);
    Task<IEnumerable<BuildRunDto>> ListBuildRunsAsync(string subscriptionId, string resourceGroupName, string registryName);
    Task<string> GetBuildLogAsync(string subscriptionId, string resourceGroupName, string registryName, string runId);

    // Kubernetes (AKS) Operations
    Task<IEnumerable<KubernetesClusterDto>> ListKubernetesClustersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<KubernetesClusterDto?> GetKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName);
    Task<KubernetesClusterDto> CreateKubernetesClusterAsync(string subscriptionId, string resourceGroupName, KubernetesClusterCreateRequest request);
    Task<bool> DeleteKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName);
    Task<KubernetesClusterDto> ScaleKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName, int nodeCount);
    Task<KubernetesClusterDto> UpgradeKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName, string kubernetesVersion);
    Task<KubernetesCredentialsDto> GetKubernetesCredentialsAsync(string subscriptionId, string resourceGroupName, string clusterName);
    Task<KubernetesClusterDto> StartKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName);
    Task<KubernetesClusterDto> StopKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName);
    
    // Kubernetes Node Pool Operations
    Task<IEnumerable<NodePoolDto>> ListNodePoolsAsync(string subscriptionId, string resourceGroupName, string clusterName);
    Task<NodePoolDto?> GetNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName);
    Task<NodePoolDto> CreateNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, NodePoolCreateRequest request);
    Task<bool> DeleteNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName);
    Task<NodePoolDto> UpdateNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName, NodePoolUpdateRequest request);
}

// Request/Response Models
public class ContainerGroupCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OsType { get; set; } = "Linux";
    public string RestartPolicy { get; set; } = "Always";
    public List<ContainerCreateRequest> Containers { get; set; } = [];
    public string? IpAddressType { get; set; }
    public List<int>? Ports { get; set; }
    public string? DnsNameLabel { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public ContainerRegistryCredentials? ImageRegistryCredentials { get; set; }
}

public class ContainerCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public double CpuCores { get; set; } = 1.0;
    public double MemoryInGb { get; set; } = 1.5;
    public List<int>? Ports { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public List<string>? Command { get; set; }
    public List<VolumeMount>? VolumeMounts { get; set; }
}

public class VolumeMount
{
    public string Name { get; set; } = string.Empty;
    public string MountPath { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}

public class ContainerRegistryCredentials
{
    public string Server { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ContainerExecResult
{
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int ExitCode { get; set; }
}

public class ContainerRegistryCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Sku { get; set; } = "Basic";
    public bool AdminUserEnabled { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class BuildTaskCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string SourceLocation { get; set; } = string.Empty;
    public string DockerFilePath { get; set; } = "Dockerfile";
    public string ImageName { get; set; } = string.Empty;
    public List<string>? ImageTags { get; set; }
    public string? Branch { get; set; }
    public Dictionary<string, string>? BuildArguments { get; set; }
}

public class BuildTaskDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedOn { get; set; }
}

public class BuildRunDto
{
    public string? Id { get; set; }
    public string? BuildTaskName { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public string? OutputImage { get; set; }
    public ContainerRegistryRunType? RunType { get; set; }
}

public class KubernetesClusterCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string KubernetesVersion { get; set; } = string.Empty;
    public string DnsPrefix { get; set; } = string.Empty;
    public NodePoolCreateRequest AgentPoolProfile { get; set; } = new();
    public string? NetworkPlugin { get; set; }
    public string? NetworkPolicy { get; set; }
    public bool EnableRBAC { get; set; } = true;
    public Dictionary<string, string>? Tags { get; set; }
}

public class NodePoolCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string VmSize { get; set; } = "Standard_DS2_v2";
    public int Count { get; set; } = 3;
    public string OsType { get; set; } = "Linux";
    public int? OsDiskSizeGB { get; set; }
    public int? MinCount { get; set; }
    public int? MaxCount { get; set; }
    public bool EnableAutoScaling { get; set; }
    public string Mode { get; set; } = "System";
}

public class NodePoolUpdateRequest
{
    public int? Count { get; set; }
    public int? MinCount { get; set; }
    public int? MaxCount { get; set; }
    public bool? EnableAutoScaling { get; set; }
    public string? OrchestratorVersion { get; set; }
}

public class KubernetesCredentialsDto
{
    public string? Kubeconfig { get; set; }
    public string? ClusterServer { get; set; }
    public string? ClusterCertificate { get; set; }
    public string? ClientCertificate { get; set; }
    public string? ClientKey { get; set; }
}
