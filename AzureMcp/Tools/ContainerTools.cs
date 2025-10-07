using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Container;
using AzureMcp.Services.Container.Models;
using ModelContextProtocol.Server;
// ReSharper disable InconsistentNaming

namespace AzureMcp.Tools;

[McpServerToolType]
public class ContainerTools(IContainerService containerService)
{
    #region Container Instance Operations

    [McpServerTool]
    [Description("List all container groups in a subscription or resource group")]
    public async Task<string> ListContainerGroupsAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ContainerGroupDto> groups = await containerService.ListContainerGroupsAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, groups = groups.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListContainerGroups");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific container group")]
    public async Task<string> GetContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName)
    {
        try
        {
            ContainerGroupDto? group = await containerService.GetContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            if (group is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Container group {containerGroupName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, group },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Create a new container group with one or more containers")]
    public async Task<string> CreateContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string name,
        [Description("Location")] string location,
        [Description("Container name")] string containerName,
        [Description("Container image")] string image,
        [Description("CPU cores (default: 1.0)")] double cpuCores = 1.0,
        [Description("Memory in GB (default: 1.5)")] double memoryGb = 1.5,
        [Description("Comma-separated ports (e.g., '80,443')")] string? ports = null,
        [Description("JSON string of environment variables")] string? environmentVariables = null,
        [Description("OS type (Linux or Windows, default: Linux)")] string osType = "Linux",
        [Description("Restart policy (Always, OnFailure, Never, default: Always)")] string restartPolicy = "Always",
        [Description("IP address type (Public or Private)")] string? ipAddressType = null,
        [Description("DNS name label")] string? dnsNameLabel = null)
    {
        try
        {
            var request = new ContainerGroupCreateRequest
            {
                Name = name,
                Location = location,
                OsType = osType,
                RestartPolicy = restartPolicy,
                IpAddressType = ipAddressType,
                DnsNameLabel = dnsNameLabel,
                Containers =
                [
                    new ContainerCreateRequest
                    {
                        Name = containerName,
                        Image = image,
                        CpuCores = cpuCores,
                        MemoryInGb = memoryGb,
                        Ports = ParseIntList(ports),
                        EnvironmentVariables = ParseEnvironmentVariables(environmentVariables)
                    }
                ]
            };

            if (!string.IsNullOrEmpty(ports))
            {
                request.Ports = ParseIntList(ports);
            }

            ContainerGroupDto group = await containerService.CreateContainerGroupAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, group, message = $"Container group {name} created successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Delete a container group")]
    public async Task<string> DeleteContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName)
    {
        try
        {
            bool result = await containerService.DeleteContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return JsonSerializer.Serialize(new { success = result, message = $"Container group {containerGroupName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Restart all containers in a container group")]
    public async Task<string> RestartContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName)
    {
        try
        {
            ContainerGroupDto group = await containerService.RestartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return JsonSerializer.Serialize(new { success = true, group, message = $"Container group {containerGroupName} restarted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "RestartContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Stop all containers in a container group")]
    public async Task<string> StopContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName)
    {
        try
        {
            ContainerGroupDto group = await containerService.StopContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return JsonSerializer.Serialize(new { success = true, group, message = $"Container group {containerGroupName} stopped successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StopContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Start all containers in a container group")]
    public async Task<string> StartContainerGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName)
    {
        try
        {
            ContainerGroupDto group = await containerService.StartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return JsonSerializer.Serialize(new { success = true, group, message = $"Container group {containerGroupName} started successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StartContainerGroup");
        }
    }

    [McpServerTool]
    [Description("Get logs from a container")]
    public async Task<string> GetContainerLogsAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName,
        [Description("Container name")] string containerName,
        [Description("Number of lines to tail (optional)")] int? tail = null)
    {
        try
        {
            string logs = await containerService.GetContainerLogsAsync(subscriptionId, resourceGroupName, containerGroupName, containerName, tail);
            return JsonSerializer.Serialize(new { success = true, logs },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetContainerLogs");
        }
    }

    [McpServerTool]
    [Description("Execute a command in a running container")]
    public async Task<string> ExecuteContainerCommandAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Container group name")] string containerGroupName,
        [Description("Container name")] string containerName,
        [Description("Command to execute")] string command)
    {
        try
        {
            ContainerExecResult result = await containerService.ExecuteCommandAsync(subscriptionId, resourceGroupName, containerGroupName, containerName, command);
            return JsonSerializer.Serialize(new { success = true, result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ExecuteContainerCommand");
        }
    }

    #endregion

    #region Container Registry Operations

    [McpServerTool]
    [Description("List all container registries in a subscription or resource group")]
    public async Task<string> ListRegistriesAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ContainerRegistryDto> registries = await containerService.ListRegistriesAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, registries = registries.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListRegistries");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific container registry")]
    public async Task<string> GetRegistryAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName)
    {
        try
        {
            ContainerRegistryDto? registry = await containerService.GetRegistryAsync(subscriptionId, resourceGroupName, registryName);
            if (registry is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Container registry {registryName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, registry },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetRegistry");
        }
    }

    [McpServerTool]
    [Description("Create a new container registry")]
    public async Task<string> CreateRegistryAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string name,
        [Description("Location")] string location,
        [Description("SKU (Basic, Standard, Premium, default: Basic)")] string sku = "Basic",
        [Description("Enable admin user (default: false)")] bool adminUserEnabled = false)
    {
        try
        {
            var request = new ContainerRegistryCreateRequest
            {
                Name = name,
                Location = location,
                Sku = sku,
                AdminUserEnabled = adminUserEnabled
            };

            ContainerRegistryDto registry = await containerService.CreateRegistryAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, registry, message = $"Container registry {name} created successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateRegistry");
        }
    }

    [McpServerTool]
    [Description("Delete a container registry")]
    public async Task<string> DeleteRegistryAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName)
    {
        try
        {
            bool result = await containerService.DeleteRegistryAsync(subscriptionId, resourceGroupName, registryName);
            return JsonSerializer.Serialize(new { success = result, message = $"Container registry {registryName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteRegistry");
        }
    }

    [McpServerTool]
    [Description("Get login credentials for a container registry")]
    public async Task<string> GetRegistryCredentialsAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName)
    {
        try
        {
            RegistryCredentialsDto credentials = await containerService.GetRegistryCredentialsAsync(subscriptionId, resourceGroupName, registryName);
            return JsonSerializer.Serialize(new { success = true, credentials },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetRegistryCredentials");
        }
    }

    [McpServerTool]
    [Description("Regenerate a password for a container registry")]
    public async Task<string> RegenerateRegistryCredentialAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName,
        [Description("Password name (password or password2)")] string passwordName)
    {
        try
        {
            RegistryCredentialsDto credentials = await containerService.RegenerateRegistryCredentialAsync(subscriptionId, resourceGroupName, registryName, passwordName);
            return JsonSerializer.Serialize(new { success = true, credentials, message = $"Credential regenerated for {registryName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "RegenerateRegistryCredential");
        }
    }

    #endregion

    #region Repository and Image Operations

    [McpServerTool]
    [Description("List repositories in a container registry")]
    public async Task<string> ListRepositoriesAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName)
    {
        try
        {
            IEnumerable<ContainerRepositoryDto> repositories = await containerService.ListRepositoriesAsync(subscriptionId, resourceGroupName, registryName);
            return JsonSerializer.Serialize(new { success = true, repositories = repositories.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListRepositories");
        }
    }

    [McpServerTool]
    [Description("List images in a container registry")]
    public async Task<string> ListImagesAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName,
        [Description("Optional repository name")] string? repositoryName = null)
    {
        try
        {
            IEnumerable<ContainerImageDto> images = await containerService.ListImagesAsync(subscriptionId, resourceGroupName, registryName, repositoryName);
            return JsonSerializer.Serialize(new { success = true, images = images.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListImages");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific container image")]
    public async Task<string> GetImageAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName,
        [Description("Repository name")] string repositoryName,
        [Description("Image tag")] string tag)
    {
        try
        {
            ContainerImageDto? image = await containerService.GetImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);
            if (image is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Image {repositoryName}:{tag} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, image },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetImage");
        }
    }

    [McpServerTool]
    [Description("Delete a container image")]
    public async Task<string> DeleteImageAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Registry name")] string registryName,
        [Description("Repository name")] string repositoryName,
        [Description("Image tag")] string tag)
    {
        try
        {
            bool result = await containerService.DeleteImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);
            return JsonSerializer.Serialize(new { success = result, message = $"Image {repositoryName}:{tag} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteImage");
        }
    }

    #endregion

    #region Kubernetes (AKS) Operations

    [McpServerTool]
    [Description("List all Kubernetes (AKS) clusters in a subscription or resource group")]
    public async Task<string> ListKubernetesClustersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<KubernetesClusterDto> clusters = await containerService.ListKubernetesClustersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, clusters = clusters.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListKubernetesClusters");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Kubernetes (AKS) cluster")]
    public async Task<string> GetKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            KubernetesClusterDto? cluster = await containerService.GetKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            if (cluster is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Kubernetes cluster {clusterName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, cluster },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetKubernetesCluster");
        }
    }

    [McpServerTool]
    [Description("Create a new Kubernetes (AKS) cluster")]
    public async Task<string> CreateKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string name,
        [Description("Location")] string location,
        [Description("Kubernetes version")] string kubernetesVersion,
        [Description("DNS prefix")] string dnsPrefix,
        [Description("Node count (default: 3)")] int nodeCount = 3,
        [Description("Node VM size (default: Standard_DS2_v2)")] string nodeVmSize = "Standard_DS2_v2",
        [Description("Enable RBAC (default: true)")] bool enableRBAC = true)
    {
        try
        {
            var request = new KubernetesClusterCreateRequest
            {
                Name = name,
                Location = location,
                KubernetesVersion = kubernetesVersion,
                DnsPrefix = dnsPrefix,
                EnableRBAC = enableRBAC,
                AgentPoolProfile = new NodePoolCreateRequest
                {
                    Name = "nodepool1",
                    VmSize = nodeVmSize,
                    Count = nodeCount
                }
            };

            KubernetesClusterDto cluster = await containerService.CreateKubernetesClusterAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, cluster, message = $"Kubernetes cluster {name} created successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateKubernetesCluster");
        }
    }

    [McpServerTool]
    [Description("Delete a Kubernetes (AKS) cluster")]
    public async Task<string> DeleteKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            bool result = await containerService.DeleteKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return JsonSerializer.Serialize(new { success = result, message = $"Kubernetes cluster {clusterName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteKubernetesCluster");
        }
    }

    [McpServerTool]
    [Description("Scale the node count in a Kubernetes (AKS) cluster")]
    public async Task<string> ScaleKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName,
        [Description("Node pool name")] string nodePoolName,
        [Description("New node count")] int nodeCount)
    {
        try
        {
            KubernetesClusterDto cluster = await containerService.ScaleKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName, nodeCount);
            return JsonSerializer.Serialize(new { success = true, cluster, message = $"Kubernetes cluster {clusterName} scaled to {nodeCount} nodes" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ScaleKubernetesCluster");
        }
    }

    [McpServerTool]
    [Description("Get credentials for connecting to a Kubernetes (AKS) cluster")]
    public async Task<string> GetKubernetesCredentialsAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            KubernetesCredentialsDto credentials = await containerService.GetKubernetesCredentialsAsync(subscriptionId, resourceGroupName, clusterName);
            return JsonSerializer.Serialize(new { success = true, credentials },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetKubernetesCredentials");
        }
    }

    [McpServerTool]
    [Description("Start a stopped Kubernetes (AKS) cluster")]
    public async Task<string> StartKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            KubernetesClusterDto cluster = await containerService.StartKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return JsonSerializer.Serialize(new { success = true, cluster, message = $"Kubernetes cluster {clusterName} started successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StartKubernetesCluster");
        }
    }

    [McpServerTool]
    [Description("Stop a running Kubernetes (AKS) cluster to save costs")]
    public async Task<string> StopKubernetesClusterAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            KubernetesClusterDto cluster = await containerService.StopKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return JsonSerializer.Serialize(new { success = true, cluster, message = $"Kubernetes cluster {clusterName} stopped successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StopKubernetesCluster");
        }
    }

    #endregion

    #region Node Pool Operations

    [McpServerTool]
    [Description("List node pools in a Kubernetes (AKS) cluster")]
    public async Task<string> ListNodePoolsAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName)
    {
        try
        {
            IEnumerable<NodePoolDto> nodePools = await containerService.ListNodePoolsAsync(subscriptionId, resourceGroupName, clusterName);
            return JsonSerializer.Serialize(new { success = true, nodePools = nodePools.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListNodePools");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific node pool")]
    public async Task<string> GetNodePoolAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName,
        [Description("Node pool name")] string nodePoolName)
    {
        try
        {
            NodePoolDto? nodePool = await containerService.GetNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);
            if (nodePool is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Node pool {nodePoolName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, nodePool },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetNodePool");
        }
    }

    [McpServerTool]
    [Description("Create a new node pool in a Kubernetes (AKS) cluster")]
    public async Task<string> CreateNodePoolAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName,
        [Description("Node pool name")] string name,
        [Description("VM size (default: Standard_DS2_v2)")] string vmSize = "Standard_DS2_v2",
        [Description("Node count (default: 3)")] int count = 3,
        [Description("Enable auto-scaling (default: false)")] bool enableAutoScaling = false,
        [Description("Minimum node count for auto-scaling")] int? minCount = null,
        [Description("Maximum node count for auto-scaling")] int? maxCount = null)
    {
        try
        {
            var request = new NodePoolCreateRequest
            {
                Name = name,
                VmSize = vmSize,
                Count = count,
                EnableAutoScaling = enableAutoScaling,
                MinCount = minCount,
                MaxCount = maxCount
            };

            NodePoolDto nodePool = await containerService.CreateNodePoolAsync(subscriptionId, resourceGroupName, clusterName, request);
            return JsonSerializer.Serialize(new { success = true, nodePool, message = $"Node pool {name} created successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateNodePool");
        }
    }

    [McpServerTool]
    [Description("Delete a node pool from a Kubernetes (AKS) cluster")]
    public async Task<string> DeleteNodePoolAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Cluster name")] string clusterName,
        [Description("Node pool name")] string nodePoolName)
    {
        try
        {
            bool result = await containerService.DeleteNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);
            return JsonSerializer.Serialize(new { success = result, message = $"Node pool {nodePoolName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteNodePool");
        }
    }

    #endregion

    #region Helper Methods

    private List<int>? ParseIntList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return value.Split(',').Select(int.Parse).ToList();
        }
        catch
        {
            return null;
        }
    }

    private Dictionary<string, string>? ParseEnvironmentVariables(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Error Handling

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            operation,
            error = ex.Message,
            type = ex.GetType().Name
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion
}
