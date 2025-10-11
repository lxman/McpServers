using Amazon.ECS.Model;
using AwsServer.Configuration;
using AwsServer.ECS;
using Microsoft.AspNetCore.Mvc;
// ReSharper disable InconsistentNaming

namespace AwsServer.Controllers;

[ApiController]
[Route("api/ecs")]
public class ECSController(EcsService ecsService) : ControllerBase
{
    /// <summary>
    /// Initialize ECS service with AWS credentials
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] AwsConfiguration config)
    {
        try
        {
            bool success = await ecsService.InitializeAsync(config);
            return Ok(new { success, message = success ? "ECS service initialized successfully" : "Failed to initialize ECS service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List ECS clusters
    /// </summary>
    [HttpGet("clusters")]
    public async Task<IActionResult> ListClusters()
    {
        try
        {
            ListClustersResponse clusters = await ecsService.ListClustersAsync();
            return Ok(new { success = true, clusterCount = clusters.ClusterArns.Count, clusters });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe an ECS cluster
    /// </summary>
    [HttpGet("clusters/{clusterName}")]
    public async Task<IActionResult> DescribeCluster(string clusterName)
    {
        try
        {
            DescribeClustersResponse cluster = await ecsService.DescribeClustersAsync([clusterName]);
            return Ok(new { success = true, cluster });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List ECS services in a cluster
    /// </summary>
    [HttpGet("clusters/{clusterName}/services")]
    public async Task<IActionResult> ListServices(string clusterName)
    {
        try
        {
            ListServicesResponse services = await ecsService.ListServicesAsync(clusterName);
            return Ok(new { success = true, serviceCount = services.ServiceArns.Count, services });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe ECS services
    /// </summary>
    [HttpPost("clusters/{clusterName}/services/describe")]
    public async Task<IActionResult> DescribeServices(string clusterName, [FromBody] List<string> serviceNames)
    {
        try
        {
            DescribeServicesResponse services = await ecsService.DescribeServicesAsync(serviceNames, clusterName);
            return Ok(new { success = true, serviceCount = services.Services.Count, services });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List tasks in a cluster
    /// </summary>
    [HttpGet("clusters/{clusterName}/tasks")]
    public async Task<IActionResult> ListTasks(string clusterName, [FromQuery] string? serviceName = null)
    {
        try
        {
            ListTasksResponse tasks = await ecsService.ListTasksAsync(clusterName, serviceName);
            return Ok(new { success = true, taskCount = tasks.TaskArns.Count, tasks });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe ECS tasks
    /// </summary>
    [HttpPost("clusters/{clusterName}/tasks/describe")]
    public async Task<IActionResult> DescribeTasks(string clusterName, [FromBody] List<string> taskArns)
    {
        try
        {
            DescribeTasksResponse tasks = await ecsService.DescribeTasksAsync(taskArns, clusterName);
            return Ok(new { success = true, taskCount = tasks.Tasks.Count, tasks });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List task definitions
    /// </summary>
    [HttpGet("task-definitions")]
    public async Task<IActionResult> ListTaskDefinitions([FromQuery] string? familyPrefix = null)
    {
        try
        {
            ListTaskDefinitionsResponse taskDefinitions = await ecsService.ListTaskDefinitionsAsync(familyPrefix);
            return Ok(new { success = true, taskDefinitionCount = taskDefinitions.TaskDefinitionArns.Count, taskDefinitions });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a task definition
    /// </summary>
    [HttpGet("task-definitions/{taskDefinition}")]
    public async Task<IActionResult> DescribeTaskDefinition(string taskDefinition)
    {
        try
        {
            DescribeTaskDefinitionResponse td = await ecsService.DescribeTaskDefinitionAsync(taskDefinition);
            return Ok(new { success = true, taskDefinition = td });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run a task
    /// </summary>
    [HttpPost("clusters/{clusterName}/tasks/run")]
    public async Task<IActionResult> RunTask(string clusterName, [FromBody] RunTaskRequest request)
    {
        try
        {
            RunTaskResponse response = await ecsService.RunTaskAsync(clusterName, request.TaskDefinition, request.Count);
            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop a task
    /// </summary>
    [HttpPost("clusters/{clusterName}/tasks/{taskArn}/stop")]
    public async Task<IActionResult> StopTask(string clusterName, string taskArn, [FromQuery] string? reason = null)
    {
        try
        {
            StopTaskResponse response = await ecsService.StopTaskAsync(clusterName, taskArn, reason);
            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update service
    /// </summary>
    [HttpPut("clusters/{clusterName}/services/{serviceName}")]
    public async Task<IActionResult> UpdateService(
        string clusterName,
        string serviceName,
        [FromBody] UpdateServiceRequest request)
    {
        try
        {
            UpdateServiceResponse response = await ecsService.UpdateServiceAsync(
                clusterName,
                serviceName,
                request.DesiredCount,
                request.TaskDefinition);
            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class RunTaskRequest
{
    public required string TaskDefinition { get; set; }
    public int Count { get; set; } = 1;
}

public class UpdateServiceRequest
{
    public int? DesiredCount { get; set; }
    public string? TaskDefinition { get; set; }
}