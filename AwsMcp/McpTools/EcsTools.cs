using System.ComponentModel;
using System.Text.Json;
using Amazon.ECS.Model;
using AwsServer.Core.Services.ECS;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AwsMcp.McpTools;

/// <summary>
/// MCP tools for AWS ECS operations
/// </summary>
[McpServerToolType]
public class EcsTools(
    EcsService ecsService,
    ILogger<EcsTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_ecs_clusters")]
    [Description("List ECS clusters. See skills/aws/ecs/list-clusters.md only when using this tool")]
    public async Task<string> ListEcsClusters()
    {
        try
        {
            logger.LogDebug("Listing ECS clusters");
            var response = await ecsService.ListClustersAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                clusterCount = response.ClusterArns.Count,
                clusters = response.ClusterArns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ECS clusters");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_ecs_services")]
    [Description("List ECS services in cluster. See skills/aws/ecs/list-services.md only when using this tool")]
    public async Task<string> ListEcsServices(
        string clusterName)
    {
        try
        {
            logger.LogDebug("Listing ECS services in cluster {ClusterName}", clusterName);
            var response = await ecsService.ListServicesAsync(clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceCount = response.ServiceArns.Count,
                services = response.ServiceArns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ECS services in cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_ecs_tasks")]
    [Description("List ECS tasks. See skills/aws/ecs/list-tasks.md only when using this tool")]
    public async Task<string> ListEcsTasks(
        string clusterName,
        string? serviceName = null)
    {
        try
        {
            logger.LogDebug("Listing ECS tasks in cluster {ClusterName}", clusterName);
            var response = await ecsService.ListTasksAsync(clusterName, serviceName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskCount = response.TaskArns.Count,
                tasks = response.TaskArns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ECS tasks in cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("run_ecs_task")]
    [Description("Run ECS task. See skills/aws/ecs/run-task.md only when using this tool")]
    public async Task<string> RunEcsTask(
        string clusterName,
        string taskDefinition,
        int count = 1)
    {
        try
        {
            logger.LogDebug("Running {Count} ECS task(s) in cluster {ClusterName}", count, clusterName);
            var response = await ecsService.RunTaskAsync(clusterName, taskDefinition, count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                tasksStarted = response.Tasks.Count,
                tasks = response.Tasks.Select(t => new
                {
                    taskArn = t.TaskArn,
                    taskDefinitionArn = t.TaskDefinitionArn,
                    desiredStatus = t.DesiredStatus,
                    lastStatus = t.LastStatus,
                    launchType = t.LaunchType?.Value
                }),
                failures = response.Failures.Select(f => new
                {
                    arn = f.Arn,
                    reason = f.Reason,
                    detail = f.Detail
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running ECS task in cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("stop_ecs_task")]
    [Description("Stop ECS task. See skills/aws/ecs/stop-task.md only when using this tool")]
    public async Task<string> StopEcsTask(
        string clusterName,
        string taskArn,
        string? reason = null)
    {
        try
        {
            logger.LogDebug("Stopping ECS task {TaskArn} in cluster {ClusterName}", taskArn, clusterName);
            var response = await ecsService.StopTaskAsync(clusterName, taskArn, reason);

            return JsonSerializer.Serialize(new
            {
                success = true,
                task = new
                {
                    taskArn = response.Task.TaskArn,
                    desiredStatus = response.Task.DesiredStatus,
                    lastStatus = response.Task.LastStatus,
                    stoppedReason = response.Task.StoppedReason
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping ECS task {TaskArn} in cluster {ClusterName}",
                taskArn, clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_ecs_cluster")]
    [Description("Describe ECS cluster. See skills/aws/ecs/describe-cluster.md only when using this tool")]
    public async Task<string> DescribeEcsCluster(string clusterName)
    {
        try
        {
            logger.LogDebug("Describing ECS cluster {ClusterName}", clusterName);
            var response = await ecsService.DescribeClustersAsync([clusterName]);

            return JsonSerializer.Serialize(new
            {
                success = true,
                clusters = response.Clusters.Select(c => new
                {
                    clusterArn = c.ClusterArn,
                    clusterName = c.ClusterName,
                    status = c.Status,
                    registeredContainerInstancesCount = c.RegisteredContainerInstancesCount,
                    runningTasksCount = c.RunningTasksCount,
                    pendingTasksCount = c.PendingTasksCount,
                    activeServicesCount = c.ActiveServicesCount
                }),
                failures = response.Failures.Select(f => new
                {
                    arn = f.Arn,
                    reason = f.Reason,
                    detail = f.Detail
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing ECS cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_ecs_services")]
    [Description("Describe ECS services. See skills/aws/ecs/describe-services.md only when using this tool")]
    public async Task<string> DescribeEcsServices(string clusterName, List<string> serviceNames)
    {
        try
        {
            logger.LogDebug("Describing ECS services in cluster {ClusterName}", clusterName);
            var response = await ecsService.DescribeServicesAsync(serviceNames, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceCount = response.Services.Count,
                services = response.Services.Select(s => new
                {
                    serviceArn = s.ServiceArn,
                    serviceName = s.ServiceName,
                    status = s.Status,
                    desiredCount = s.DesiredCount,
                    runningCount = s.RunningCount,
                    pendingCount = s.PendingCount,
                    taskDefinition = s.TaskDefinition,
                    launchType = s.LaunchType?.Value
                }),
                failures = response.Failures.Select(f => new
                {
                    arn = f.Arn,
                    reason = f.Reason,
                    detail = f.Detail
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing ECS services in cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_ecs_tasks")]
    [Description("Describe ECS tasks. See skills/aws/ecs/describe-tasks.md only when using this tool")]
    public async Task<string> DescribeEcsTasks(string clusterName, List<string> taskArns)
    {
        try
        {
            logger.LogDebug("Describing ECS tasks in cluster {ClusterName}", clusterName);
            var response = await ecsService.DescribeTasksAsync(taskArns, clusterName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskCount = response.Tasks.Count,
                tasks = response.Tasks.Select(t => new
                {
                    taskArn = t.TaskArn,
                    taskDefinitionArn = t.TaskDefinitionArn,
                    clusterArn = t.ClusterArn,
                    desiredStatus = t.DesiredStatus,
                    lastStatus = t.LastStatus,
                    launchType = t.LaunchType?.Value,
                    cpu = t.Cpu,
                    memory = t.Memory,
                    startedAt = t.StartedAt,
                    stoppedAt = t.StoppedAt,
                    stoppedReason = t.StoppedReason
                }),
                failures = response.Failures.Select(f => new
                {
                    arn = f.Arn,
                    reason = f.Reason,
                    detail = f.Detail
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing ECS tasks in cluster {ClusterName}", clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_task_definitions")]
    [Description("List ECS task definitions. See skills/aws/ecs/list-task-definitions.md only when using this tool")]
    public async Task<string> ListTaskDefinitions(string? familyPrefix = null)
    {
        try
        {
            logger.LogDebug("Listing ECS task definitions with prefix {FamilyPrefix}", familyPrefix);
            var response = await ecsService.ListTaskDefinitionsAsync(familyPrefix);

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskDefinitionCount = response.TaskDefinitionArns.Count,
                taskDefinitions = response.TaskDefinitionArns
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ECS task definitions");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_task_definition")]
    [Description("Describe ECS task definition. See skills/aws/ecs/describe-task-definition.md only when using this tool")]
    public async Task<string> DescribeTaskDefinition(string taskDefinition)
    {
        try
        {
            logger.LogDebug("Describing ECS task definition {TaskDefinition}", taskDefinition);
            var response = await ecsService.DescribeTaskDefinitionAsync(taskDefinition);

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskDefinition = new
                {
                    taskDefinitionArn = response.TaskDefinition.TaskDefinitionArn,
                    family = response.TaskDefinition.Family,
                    revision = response.TaskDefinition.Revision,
                    status = response.TaskDefinition.Status?.Value,
                    cpu = response.TaskDefinition.Cpu,
                    memory = response.TaskDefinition.Memory,
                    networkMode = response.TaskDefinition.NetworkMode?.Value,
                    requiresCompatibilities = response.TaskDefinition.RequiresCompatibilities,
                    containerDefinitions = response.TaskDefinition.ContainerDefinitions.Select(cd => new
                    {
                        name = cd.Name,
                        image = cd.Image,
                        cpu = cd.Cpu,
                        memory = cd.Memory,
                        essential = cd.Essential,
                        portMappings = cd.PortMappings
                    })
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing ECS task definition {TaskDefinition}", taskDefinition);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_ecs_service")]
    [Description("Update ECS service. See skills/aws/ecs/update-service.md only when using this tool")]
    public async Task<string> UpdateEcsService(
        string clusterName,
        string serviceName,
        int? desiredCount = null,
        string? taskDefinition = null)
    {
        try
        {
            logger.LogDebug("Updating ECS service {ServiceName} in cluster {ClusterName}", serviceName, clusterName);
            var response = await ecsService.UpdateServiceAsync(
                clusterName,
                serviceName,
                desiredCount,
                taskDefinition);

            return JsonSerializer.Serialize(new
            {
                success = true,
                service = new
                {
                    serviceArn = response.Service.ServiceArn,
                    serviceName = response.Service.ServiceName,
                    status = response.Service.Status,
                    desiredCount = response.Service.DesiredCount,
                    runningCount = response.Service.RunningCount,
                    pendingCount = response.Service.PendingCount,
                    taskDefinition = response.Service.TaskDefinition
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating ECS service {ServiceName} in cluster {ClusterName}",
                serviceName, clusterName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}