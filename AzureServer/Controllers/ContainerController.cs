using AzureServer.Services.Container;
using AzureServer.Services.Container.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContainerController(IContainerService containerService, ILogger<ContainerController> logger) : ControllerBase
{
    #region Container Groups

    [HttpGet("groups")]
    public async Task<ActionResult> ListContainerGroups(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var groups = await containerService.ListContainerGroupsAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, containerGroups = groups.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container groups");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListContainerGroups", type = ex.GetType().Name });
        }
    }

    [HttpGet("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}")]
    public async Task<ActionResult> GetContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            var group = await containerService.GetContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            if (group is null)
                return NotFound(new { success = false, error = $"Container group {containerGroupName} not found" });

            return Ok(new { success = true, containerGroup = group });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container group {GroupName}", containerGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpPost("groups/{subscriptionId}/{resourceGroupName}")]
    public async Task<ActionResult> CreateContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        [FromBody] ContainerGroupCreateRequest request)
    {
        try
        {
            var group = await containerService.CreateContainerGroupAsync(subscriptionId, resourceGroupName, request);
            return Ok(new { success = true, containerGroup = group });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container group {GroupName}", request.Name);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpDelete("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}")]
    public async Task<ActionResult> DeleteContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            var result = await containerService.DeleteContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return Ok(new { success = result, message = result ? "Container group deleted" : "Container group not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container group {GroupName}", containerGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpPost("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}/restart")]
    public async Task<ActionResult> RestartContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            var group = await containerService.RestartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return Ok(new { success = true, containerGroup = group });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting container group {GroupName}", containerGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "RestartContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpPost("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}/stop")]
    public async Task<ActionResult> StopContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            var group = await containerService.StopContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return Ok(new { success = true, containerGroup = group });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping container group {GroupName}", containerGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StopContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpPost("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}/start")]
    public async Task<ActionResult> StartContainerGroup(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName)
    {
        try
        {
            var group = await containerService.StartContainerGroupAsync(subscriptionId, resourceGroupName, containerGroupName);
            return Ok(new { success = true, containerGroup = group });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting container group {GroupName}", containerGroupName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StartContainerGroup", type = ex.GetType().Name });
        }
    }

    [HttpGet("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}/logs")]
    public async Task<ActionResult> GetContainerLogs(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName,
        [FromQuery] string containerName,
        [FromQuery] int? tail = null)
    {
        try
        {
            var logs = await containerService.GetContainerLogsAsync(subscriptionId, resourceGroupName, containerGroupName, containerName, tail);
            return Ok(new { success = true, logs });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for container {ContainerName}", containerName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetContainerLogs", type = ex.GetType().Name });
        }
    }

    [HttpPost("groups/{subscriptionId}/{resourceGroupName}/{containerGroupName}/exec")]
    public async Task<ActionResult> ExecuteCommand(
        string subscriptionId,
        string resourceGroupName,
        string containerGroupName,
        [FromBody] ExecuteCommandRequest request)
    {
        try
        {
            var result = await containerService.ExecuteCommandAsync(subscriptionId, resourceGroupName, containerGroupName, request.ContainerName, request.Command);
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command in container {ContainerName}", request.ContainerName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ExecuteCommand", type = ex.GetType().Name });
        }
    }

    #endregion

    #region Container Registries

    [HttpGet("registries")]
    public async Task<ActionResult> ListRegistries(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var registries = await containerService.ListRegistriesAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, registries = registries.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container registries");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListRegistries", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}")]
    public async Task<ActionResult> GetRegistry(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            var registry = await containerService.GetRegistryAsync(subscriptionId, resourceGroupName, registryName);
            if (registry is null)
                return NotFound(new { success = false, error = $"Registry {registryName} not found" });

            return Ok(new { success = true, registry });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetRegistry", type = ex.GetType().Name });
        }
    }

    [HttpPost("registries/{subscriptionId}/{resourceGroupName}")]
    public async Task<ActionResult> CreateRegistry(
        string subscriptionId,
        string resourceGroupName,
        [FromBody] ContainerRegistryCreateRequest request)
    {
        try
        {
            var registry = await containerService.CreateRegistryAsync(subscriptionId, resourceGroupName, request);
            return Ok(new { success = true, registry });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating registry {RegistryName}", request.Name);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateRegistry", type = ex.GetType().Name });
        }
    }

    [HttpDelete("registries/{subscriptionId}/{resourceGroupName}/{registryName}")]
    public async Task<ActionResult> DeleteRegistry(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            var result = await containerService.DeleteRegistryAsync(subscriptionId, resourceGroupName, registryName);
            return Ok(new { success = result, message = result ? "Registry deleted" : "Registry not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteRegistry", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/credentials")]
    public async Task<ActionResult> GetRegistryCredentials(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            var credentials = await containerService.GetRegistryCredentialsAsync(subscriptionId, resourceGroupName, registryName);
            return Ok(new { success = true, credentials });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting credentials for registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetRegistryCredentials", type = ex.GetType().Name });
        }
    }

    [HttpPost("registries/{subscriptionId}/{resourceGroupName}/{registryName}/credentials/regenerate")]
    public async Task<ActionResult> RegenerateRegistryCredential(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        [FromBody] RegenerateCredentialRequest request)
    {
        try
        {
            var credentials = await containerService.RegenerateRegistryCredentialAsync(subscriptionId, resourceGroupName, registryName, request.PasswordName);
            return Ok(new { success = true, credentials });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error regenerating credential for registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "RegenerateRegistryCredential", type = ex.GetType().Name });
        }
    }

    #endregion

    #region Registry Repositories and Images

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/repositories")]
    public async Task<ActionResult> ListRepositories(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            var repositories = await containerService.ListRepositoriesAsync(subscriptionId, resourceGroupName, registryName);
            return Ok(new { success = true, repositories = repositories.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing repositories in registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListRepositories", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/images")]
    public async Task<ActionResult> ListImages(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        [FromQuery] string? repositoryName = null)
    {
        try
        {
            var images = await containerService.ListImagesAsync(subscriptionId, resourceGroupName, registryName, repositoryName);
            return Ok(new { success = true, images = images.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing images in registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListImages", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/repositories/{repositoryName}/images/{tag}")]
    public async Task<ActionResult> GetImage(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName,
        string tag)
    {
        try
        {
            var image = await containerService.GetImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);
            if (image is null)
                return NotFound(new { success = false, error = $"Image {repositoryName}:{tag} not found" });

            return Ok(new { success = true, image });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting image {Repository}:{Tag}", repositoryName, tag);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetImage", type = ex.GetType().Name });
        }
    }

    [HttpDelete("registries/{subscriptionId}/{resourceGroupName}/{registryName}/repositories/{repositoryName}/images/{tag}")]
    public async Task<ActionResult> DeleteImage(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName,
        string tag)
    {
        try
        {
            var result = await containerService.DeleteImageAsync(subscriptionId, resourceGroupName, registryName, repositoryName, tag);
            return Ok(new { success = result, message = result ? "Image deleted" : "Image not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting image {Repository}:{Tag}", repositoryName, tag);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteImage", type = ex.GetType().Name });
        }
    }

    [HttpDelete("registries/{subscriptionId}/{resourceGroupName}/{registryName}/repositories/{repositoryName}")]
    public async Task<ActionResult> DeleteRepository(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string repositoryName)
    {
        try
        {
            var result = await containerService.DeleteRepositoryAsync(subscriptionId, resourceGroupName, registryName, repositoryName);
            return Ok(new { success = result, message = result ? "Repository deleted" : "Repository not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting repository {Repository}", repositoryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteRepository", type = ex.GetType().Name });
        }
    }

    #endregion

    #region Registry Build Operations

    [HttpPost("registries/{subscriptionId}/{resourceGroupName}/{registryName}/builds/tasks")]
    public async Task<ActionResult> CreateBuildTask(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        [FromBody] BuildTaskCreateRequest request)
    {
        try
        {
            var buildTask = await containerService.CreateBuildTaskAsync(subscriptionId, resourceGroupName, registryName, request);
            return Ok(new { success = true, buildTask });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating build task {TaskName}", request.Name);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateBuildTask", type = ex.GetType().Name });
        }
    }

    [HttpPost("registries/{subscriptionId}/{resourceGroupName}/{registryName}/builds/tasks/{buildTaskName}/run")]
    public async Task<ActionResult> RunBuildTask(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string buildTaskName)
    {
        try
        {
            var buildRun = await containerService.RunBuildTaskAsync(subscriptionId, resourceGroupName, registryName, buildTaskName);
            return Ok(new { success = true, buildRun });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running build task {TaskName}", buildTaskName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "RunBuildTask", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/builds/runs")]
    public async Task<ActionResult> ListBuildRuns(
        string subscriptionId,
        string resourceGroupName,
        string registryName)
    {
        try
        {
            var buildRuns = await containerService.ListBuildRunsAsync(subscriptionId, resourceGroupName, registryName);
            return Ok(new { success = true, buildRuns = buildRuns.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing build runs for registry {RegistryName}", registryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListBuildRuns", type = ex.GetType().Name });
        }
    }

    [HttpGet("registries/{subscriptionId}/{resourceGroupName}/{registryName}/builds/runs/{runId}/logs")]
    public async Task<ActionResult> GetBuildLog(
        string subscriptionId,
        string resourceGroupName,
        string registryName,
        string runId)
    {
        try
        {
            var log = await containerService.GetBuildLogAsync(subscriptionId, resourceGroupName, registryName, runId);
            return Ok(new { success = true, log });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build log for run {RunId}", runId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildLog", type = ex.GetType().Name });
        }
    }

    #endregion

    #region Kubernetes (AKS) Clusters

    [HttpGet("kubernetes/clusters")]
    public async Task<ActionResult> ListKubernetesClusters(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var clusters = await containerService.ListKubernetesClustersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, clusters = clusters.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Kubernetes clusters");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListKubernetesClusters", type = ex.GetType().Name });
        }
    }

    [HttpGet("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}")]
    public async Task<ActionResult> GetKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var cluster = await containerService.GetKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            if (cluster is null)
                return NotFound(new { success = false, error = $"Kubernetes cluster {clusterName} not found" });

            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}")]
    public async Task<ActionResult> CreateKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        [FromBody] KubernetesClusterCreateRequest request)
    {
        try
        {
            var cluster = await containerService.CreateKubernetesClusterAsync(subscriptionId, resourceGroupName, request);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Kubernetes cluster {ClusterName}", request.Name);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpDelete("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}")]
    public async Task<ActionResult> DeleteKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var result = await containerService.DeleteKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return Ok(new { success = result, message = result ? "Cluster deleted" : "Cluster not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/scale")]
    public async Task<ActionResult> ScaleKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        [FromBody] ScaleClusterRequest request)
    {
        try
        {
            var cluster = await containerService.ScaleKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName, request.NodePoolName, request.NodeCount);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ScaleKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/upgrade")]
    public async Task<ActionResult> UpgradeKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        [FromBody] UpgradeClusterRequest request)
    {
        try
        {
            var cluster = await containerService.UpgradeKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName, request.KubernetesVersion);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error upgrading Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpgradeKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpGet("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/credentials")]
    public async Task<ActionResult> GetKubernetesCredentials(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var credentials = await containerService.GetKubernetesCredentialsAsync(subscriptionId, resourceGroupName, clusterName);
            return Ok(new { success = true, credentials });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting credentials for Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetKubernetesCredentials", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/start")]
    public async Task<ActionResult> StartKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var cluster = await containerService.StartKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StartKubernetesCluster", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/stop")]
    public async Task<ActionResult> StopKubernetesCluster(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var cluster = await containerService.StopKubernetesClusterAsync(subscriptionId, resourceGroupName, clusterName);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping Kubernetes cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "StopKubernetesCluster", type = ex.GetType().Name });
        }
    }

    #endregion

    #region Node Pools

    [HttpGet("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/nodepools")]
    public async Task<ActionResult> ListNodePools(
        string subscriptionId,
        string resourceGroupName,
        string clusterName)
    {
        try
        {
            var nodePools = await containerService.ListNodePoolsAsync(subscriptionId, resourceGroupName, clusterName);
            return Ok(new { success = true, nodePools = nodePools.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing node pools for cluster {ClusterName}", clusterName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListNodePools", type = ex.GetType().Name });
        }
    }

    [HttpGet("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/nodepools/{nodePoolName}")]
    public async Task<ActionResult> GetNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName)
    {
        try
        {
            var nodePool = await containerService.GetNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);
            if (nodePool is null)
                return NotFound(new { success = false, error = $"Node pool {nodePoolName} not found" });

            return Ok(new { success = true, nodePool });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting node pool {NodePoolName}", nodePoolName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetNodePool", type = ex.GetType().Name });
        }
    }

    [HttpPost("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/nodepools")]
    public async Task<ActionResult> CreateNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        [FromBody] NodePoolCreateRequest request)
    {
        try
        {
            var nodePool = await containerService.CreateNodePoolAsync(subscriptionId, resourceGroupName, clusterName, request);
            return Ok(new { success = true, nodePool });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating node pool {NodePoolName}", request.Name);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateNodePool", type = ex.GetType().Name });
        }
    }

    [HttpDelete("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/nodepools/{nodePoolName}")]
    public async Task<ActionResult> DeleteNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName)
    {
        try
        {
            var result = await containerService.DeleteNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName);
            return Ok(new { success = result, message = result ? "Node pool deleted" : "Node pool not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting node pool {NodePoolName}", nodePoolName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteNodePool", type = ex.GetType().Name });
        }
    }

    [HttpPut("kubernetes/clusters/{subscriptionId}/{resourceGroupName}/{clusterName}/nodepools/{nodePoolName}")]
    public async Task<ActionResult> UpdateNodePool(
        string subscriptionId,
        string resourceGroupName,
        string clusterName,
        string nodePoolName,
        [FromBody] NodePoolUpdateRequest request)
    {
        try
        {
            var nodePool = await containerService.UpdateNodePoolAsync(subscriptionId, resourceGroupName, clusterName, nodePoolName, request);
            return Ok(new { success = true, nodePool });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating node pool {NodePoolName}", nodePoolName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpdateNodePool", type = ex.GetType().Name });
        }
    }

    #endregion
}

public record ExecuteCommandRequest(string ContainerName, string Command);
public record RegenerateCredentialRequest(string PasswordName);
public record ScaleClusterRequest(string NodePoolName, int NodeCount);
public record UpgradeClusterRequest(string KubernetesVersion);