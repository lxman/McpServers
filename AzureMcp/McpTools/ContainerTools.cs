using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.Container;
using AzureServer.Core.Services.Container.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Container operations
/// </summary>
[McpServerToolType]
public class ContainerTools(
    IContainerService containerService,
    ILogger<ContainerTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    #region Container Groups

    [McpServerTool, DisplayName("list_container_groups")]
    [Description("List container groups. See skills/azure/container/list-groups.md only when using this tool")]
    public async Task<string> ListContainerGroups(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing container groups");
            IEnumerable<ContainerGroupDto> groups = await containerService.ListContainerGroupsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroups = groups.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container groups");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_container_group")]
    [Description("Get container group. See skills/azure/container/get-group.md only when using this tool")]
    public async Task<string> GetContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            logger.LogDebug("Getting container group {GroupName}", containerGroupName);
            ContainerGroupDto? group = await containerService.GetContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroup = group
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container group {GroupName}", containerGroupName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_container_group")]
    [Description("Create container group. See skills/azure/container/create-group.md only when using this tool")]
    public async Task<string> CreateContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        ContainerGroupCreateRequest request)
    {
        try
        {
            logger.LogDebug("Creating container group {GroupName}", request.Name);
            ContainerGroupDto group = await containerService.CreateContainerGroupAsync(subscriptionId, resourceGroupName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroup = group
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container group {GroupName}", request.Name);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_container_group")]
    [Description("Delete container group. See skills/azure/container/delete-group.md only when using this tool")]
    public async Task<string> DeleteContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            logger.LogDebug("Deleting container group {GroupName}", containerGroupName);
            bool result = await containerService.DeleteContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Container group deleted" : "Container group not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container group {GroupName}", containerGroupName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("restart_container_group")]
    [Description("Restart container group. See skills/azure/container/restart-group.md only when using this tool")]
    public async Task<string> RestartContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            logger.LogDebug("Restarting container group {GroupName}", containerGroupName);
            ContainerGroupDto group = await containerService.RestartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroup = group
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting container group {GroupName}", containerGroupName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("stop_container_group")]
    [Description("Stop container group. See skills/azure/container/stop-group.md only when using this tool")]
    public async Task<string> StopContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            logger.LogDebug("Stopping container group {GroupName}", containerGroupName);
            ContainerGroupDto group = await containerService.StopContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroup = group
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping container group {GroupName}", containerGroupName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("start_container_group")]
    [Description("Start container group. See skills/azure/container/start-group.md only when using this tool")]
    public async Task<string> StartContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            logger.LogDebug("Starting container group {GroupName}", containerGroupName);
            ContainerGroupDto group = await containerService.StartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerGroup = group
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting container group {GroupName}", containerGroupName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_container_logs")]
    [Description("Get container logs. See skills/azure/container/get-logs.md only when using this tool")]
    public async Task<string> GetContainerLogs(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName,
        string containerName,
        int? tail = null)
    {
        try
        {
            logger.LogDebug("Getting logs for container {ContainerName}", containerName);
            string logs = await containerService.GetContainerLogsAsync(subscriptionId, resourceGroupName, containerGroupName, containerName, tail);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logs
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("execute_container_command")]
    [Description("Execute command in container. See skills/azure/container/execute-command.md only when using this tool")]
    public async Task<string> ExecuteContainerCommand(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName,
        string containerName,
        string command)
    {
        try
        {
            logger.LogDebug("Executing command in container {ContainerName}", containerName);
            ContainerExecResult result = await containerService.ExecuteCommandAsync(subscriptionId, resourceGroupName, containerGroupName, containerName, command);

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command in container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Container Registries

    [McpServerTool, DisplayName("list_registries")]
    [Description("List container registries. See skills/azure/container/list-registries.md only when using this tool")]
    public async Task<string> ListRegistries(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing container registries");
            IEnumerable<ContainerRegistryDto> registries = await containerService.ListRegistriesAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                registries = registries.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container registries");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_registry")]
    [Description("Get container registry. See skills/azure/container/get-registry.md only when using this tool")]
    public async Task<string> GetRegistry(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            logger.LogDebug("Getting registry {RegistryName}", registryName);
            ContainerRegistryDto? registry = await containerService.GetRegistryAsync(subscriptionId, resourceGroupName, registryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                registry
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_registry")]
    [Description("Create container registry. See skills/azure/container/create-registry.md only when using this tool")]
    public async Task<string> CreateRegistry(
        string subscriptionId,
        string resourceGroupName,
        ContainerRegistryCreateRequest request)
    {
        try
        {
            logger.LogDebug("Creating registry {RegistryName}", request.Name);
            ContainerRegistryDto registry = await containerService.CreateRegistryAsync(subscriptionId, resourceGroupName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                registry
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating registry {RegistryName}", request.Name);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_registry")]
    [Description("Delete container registry. See skills/azure/container/delete-registry.md only when using this tool")]
    public async Task<string> DeleteRegistry(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            logger.LogDebug("Deleting registry {RegistryName}", registryName);
            bool result = await containerService.DeleteRegistryAsync(subscriptionId, resourceGroupName, registryName);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Registry deleted" : "Registry not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_registry_credentials")]
    [Description("Get registry credentials. See skills/azure/container/get-credentials.md only when using this tool")]
    public async Task<string> GetRegistryCredentials(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            logger.LogDebug("Getting credentials for registry {RegistryName}", registryName);
            RegistryCredentialsDto credentials = await containerService.GetRegistryCredentialsAsync(subscriptionId, resourceGroupName, registryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                credentials
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting credentials for registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("regenerate_registry_credential")]
    [Description("Regenerate registry credential. See skills/azure/container/regenerate-credential.md only when using this tool")]
    public async Task<string> RegenerateRegistryCredential(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string passwordName)
    {
        try
        {
            logger.LogDebug("Regenerating credential for registry {RegistryName}", registryName);
            RegistryCredentialsDto credentials = await containerService.RegenerateRegistryCredentialAsync(subscriptionId, resourceGroupName, registryName, passwordName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                credentials
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error regenerating credential for registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Registry Repositories and Images

    [McpServerTool, DisplayName("list_repositories")]
    [Description("List registry repositories. See skills/azure/container/list-repositories.md only when using this tool")]
    public async Task<string> ListRepositories(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            logger.LogDebug("Listing repositories in registry {RegistryName}", registryName);
            IEnumerable<ContainerRepositoryDto> repositories = await containerService.ListRepositoriesAsync(subscriptionId, resourceGroupName, registryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositories = repositories.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing repositories in registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_images")]
    [Description("List container images. See skills/azure/container/list-images.md only when using this tool")]
    public async Task<string> ListImages(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string? repositoryName = null)
    {
        try
        {
            logger.LogDebug("Listing images in registry {RegistryName}", registryName);
            IEnumerable<ContainerImageDto> images = await containerService.ListImagesAsync(subscriptionId, resourceGroupName, registryName, repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                images = images.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing images in registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_image")]
    [Description("Get container image. See skills/azure/container/get-image.md only when using this tool")]
    public async Task<string> GetImage(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName,
        string tag)
    {
        try
        {
            logger.LogDebug("Getting image {Repository}:{Tag}", repositoryName, tag);
            ContainerImageDto? image = await containerService.GetImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);

            return JsonSerializer.Serialize(new
            {
                success = true,
                image
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting image {Repository}:{Tag}", repositoryName, tag);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_image")]
    [Description("Delete container image. See skills/azure/container/delete-image.md only when using this tool")]
    public async Task<string> DeleteImage(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName,
        string tag)
    {
        try
        {
            logger.LogDebug("Deleting image {Repository}:{Tag}", repositoryName, tag);
            bool result = await containerService.DeleteImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Image deleted" : "Image not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting image {Repository}:{Tag}", repositoryName, tag);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_repository")]
    [Description("Delete container repository. See skills/azure/container/delete-repository.md only when using this tool")]
    public async Task<string> DeleteRepository(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName)
    {
        try
        {
            logger.LogDebug("Deleting repository {Repository}", repositoryName);
            bool result = await containerService.DeleteRepositoryAsync(subscriptionId, resourceGroupName, registryName, repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Repository deleted" : "Repository not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting repository {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Registry Build Operations

    [McpServerTool, DisplayName("create_build_task")]
    [Description("Create build task. See skills/azure/container/create-build-task.md only when using this tool")]
    public async Task<string> CreateBuildTask(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        BuildTaskCreateRequest request)
    {
        try
        {
            logger.LogDebug("Creating build task {TaskName}", request.Name);
            BuildTaskDto buildTask = await containerService.CreateBuildTaskAsync(subscriptionId, resourceGroupName, registryName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                buildTask
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating build task {TaskName}", request.Name);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("run_build_task")]
    [Description("Run build task. See skills/azure/container/run-build-task.md only when using this tool")]
    public async Task<string> RunBuildTask(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string buildTaskName)
    {
        try
        {
            logger.LogDebug("Running build task {TaskName}", buildTaskName);
            BuildRunDto buildRun = await containerService.RunBuildTaskAsync(subscriptionId, resourceGroupName, registryName, buildTaskName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                buildRun
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running build task {TaskName}", buildTaskName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_build_runs")]
    [Description("List build runs. See skills/azure/container/list-build-runs.md only when using this tool")]
    public async Task<string> ListBuildRuns(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            logger.LogDebug("Listing build runs for registry {RegistryName}", registryName);
            IEnumerable<BuildRunDto> buildRuns = await containerService.ListBuildRunsAsync(subscriptionId, resourceGroupName, registryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                buildRuns = buildRuns.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing build runs for registry {RegistryName}", registryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_build_log")]
    [Description("Get build log. See skills/azure/container/get-build-log.md only when using this tool")]
    public async Task<string> GetBuildLog(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string runId)
    {
        try
        {
            logger.LogDebug("Getting build log for run {RunId}", runId);
            string log = await containerService.GetBuildLogAsync(subscriptionId, resourceGroupName, registryName, runId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                log
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build log for run {RunId}", runId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Kubernetes (AKS) Clusters

    [McpServerTool, DisplayName("list_kubernetes_clusters")]
    [Description("List Kubernetes clusters. See skills/azure/container/list-kubernetes.md only when using this tool")]
    public async Task<string> ListKubernetesClusters(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing Kubernetes clusters");
            IEnumerable<KubernetesClusterDto> clusters = await containerService.ListKubernetesClustersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                clusters = clusters.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Kubernetes clusters");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_kubernetes_cluster")]
    [Description("Get Kubernetes cluster. See skills/azure/container/get-kubernetes.md only when using this tool")]
    public async Task<string> GetKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Getting Kubernetes cluster {ClusterName}", clusterName);
            KubernetesClusterDto? cluster = await containerService.GetKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_kubernetes_cluster")]
    [Description("Create Kubernetes cluster. See skills/azure/container/create-kubernetes.md only when using this tool")]
    public async Task<string> CreateKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        KubernetesClusterCreateRequest request)
    {
        try
        {
            logger.LogDebug("Creating Kubernetes cluster {ClusterName}", request.Name);
            KubernetesClusterDto cluster = await containerService.CreateKubernetesClusterAsync(subscriptionId, resourceGroupName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Kubernetes cluster {ClusterName}", request.Name);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_kubernetes_cluster")]
    [Description("Delete Kubernetes cluster. See skills/azure/container/delete-kubernetes.md only when using this tool")]
    public async Task<string> DeleteKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Deleting Kubernetes cluster {ClusterName}", clusterName);
            bool result = await containerService.DeleteKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Cluster deleted" : "Cluster not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("scale_kubernetes_cluster")]
    [Description("Scale Kubernetes cluster. See skills/azure/container/scale-kubernetes.md only when using this tool")]
    public async Task<string> ScaleKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName,
        int nodeCount)
    {
        try
        {
            logger.LogDebug("Scaling Kubernetes cluster {ClusterName}", clusterName);
            KubernetesClusterDto cluster = await containerService.ScaleKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName, nodeCount);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("upgrade_kubernetes_cluster")]
    [Description("Upgrade Kubernetes cluster. See skills/azure/container/upgrade-kubernetes.md only when using this tool")]
    public async Task<string> UpgradeKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string kubernetesVersion)
    {
        try
        {
            logger.LogDebug("Upgrading Kubernetes cluster {ClusterName}", clusterName);
            KubernetesClusterDto cluster = await containerService.UpgradeKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName, kubernetesVersion);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error upgrading Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_kubernetes_credentials")]
    [Description("Get Kubernetes credentials. See skills/azure/container/get-kubernetes-creds.md only when using this tool")]
    public async Task<string> GetKubernetesCredentials(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Getting credentials for Kubernetes cluster {ClusterName}", clusterName);
            KubernetesCredentialsDto credentials = await containerService.GetKubernetesCredentialsAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                credentials
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting credentials for Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("start_kubernetes_cluster")]
    [Description("Start Kubernetes cluster. See skills/azure/container/start-kubernetes.md only when using this tool")]
    public async Task<string> StartKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Starting Kubernetes cluster {ClusterName}", clusterName);
            KubernetesClusterDto cluster = await containerService.StartKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("stop_kubernetes_cluster")]
    [Description("Stop Kubernetes cluster. See skills/azure/container/stop-kubernetes.md only when using this tool")]
    public async Task<string> StopKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Stopping Kubernetes cluster {ClusterName}", clusterName);
            KubernetesClusterDto cluster = await containerService.StopKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping Kubernetes cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Node Pools

    [McpServerTool, DisplayName("list_node_pools")]
    [Description("List node pools. See skills/azure/container/list-node-pools.md only when using this tool")]
    public async Task<string> ListNodePools(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            logger.LogDebug("Listing node pools for cluster {ClusterName}", clusterName);
            IEnumerable<NodePoolDto> nodePools = await containerService.ListNodePoolsAsync(subscriptionId, resourceGroupName, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                nodePools = nodePools.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing node pools for cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_node_pool")]
    [Description("Get node pool. See skills/azure/container/get-node-pool.md only when using this tool")]
    public async Task<string> GetNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName)
    {
        try
        {
            logger.LogDebug("Getting node pool {NodePoolName}", nodePoolName);
            NodePoolDto? nodePool = await containerService.GetNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                nodePool
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting node pool {NodePoolName}", nodePoolName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_node_pool")]
    [Description("Create node pool. See skills/azure/container/create-node-pool.md only when using this tool")]
    public async Task<string> CreateNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        NodePoolCreateRequest request)
    {
        try
        {
            logger.LogDebug("Creating node pool {NodePoolName}", request.Name);
            NodePoolDto nodePool = await containerService.CreateNodePoolAsync(subscriptionId, resourceGroupName, clusterName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                nodePool
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating node pool {NodePoolName}", request.Name);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_node_pool")]
    [Description("Delete node pool. See skills/azure/container/delete-node-pool.md only when using this tool")]
    public async Task<string> DeleteNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName)
    {
        try
        {
            logger.LogDebug("Deleting node pool {NodePoolName}", nodePoolName);
            bool result = await containerService.DeleteNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Node pool deleted" : "Node pool not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting node pool {NodePoolName}", nodePoolName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_node_pool")]
    [Description("Update node pool. See skills/azure/container/update-node-pool.md only when using this tool")]
    public async Task<string> UpdateNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName,
        NodePoolUpdateRequest request)
    {
        try
        {
            logger.LogDebug("Updating node pool {NodePoolName}", nodePoolName);
            NodePoolDto nodePool = await containerService.UpdateNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                nodePool
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating node pool {NodePoolName}", nodePoolName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion
}