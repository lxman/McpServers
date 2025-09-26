using System.ComponentModel;
using System.Text.Json;
using Amazon.ECS.Model;
using AwsMcp.Configuration;
using AwsMcp.ECS;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class EcsTools
{
    private readonly EcsService _ecsService;

    public EcsTools(EcsService ecsService)
    {
        _ecsService = ecsService;
    }

    [McpServerTool]
    [Description("Initialize ECS service with AWS credentials and configuration")]
    public async Task<string> InitializeEcs(
        [Description("AWS region (default: us-east-1)")]
        string region = "us-east-1",
        [Description("AWS Access Key ID (optional if using profile or environment)")]
        string? accessKeyId = null,
        [Description("AWS Secret Access Key (optional if using profile or environment)")]
        string? secretAccessKey = null,
        [Description("AWS Profile name (optional)")]
        string? profileName = null,
        [Description("Custom service URL for LocalStack or other endpoints (optional)")]
        string? serviceUrl = null)
    {
        try
        {
            var config = new AwsConfiguration
            {
                Region = region,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                ProfileName = profileName,
                ServiceUrl = serviceUrl
            };

            bool success = await _ecsService.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "ECS service initialized successfully" : "Failed to initialize ECS service",
                region = config.Region
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List all ECS clusters")]
    public async Task<string> ListClusters()
    {
        try
        {
            ListClustersResponse response = await _ecsService.ListClustersAsync();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                clusters = response.ClusterArns,
                count = response.ClusterArns.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe ECS clusters with detailed information")]
    public async Task<string> DescribeClusters(
        [Description("List of cluster ARNs or names to describe (optional, describes all if not provided)")]
        string? clusterNames = null)
    {
        try
        {
            List<string>? clusters = null;
            if (!string.IsNullOrEmpty(clusterNames))
            {
                clusters = clusterNames.Split(',').Select(c => c.Trim()).ToList();
            }

            DescribeClustersResponse response = await _ecsService.DescribeClustersAsync(clusters);
            
            var clusterDetails = response.Clusters.Select(cluster => new
            {
                ClusterName = cluster.ClusterName,
                ClusterArn = cluster.ClusterArn,
                Status = cluster.Status,
                RunningTasksCount = cluster.RunningTasksCount,
                PendingTasksCount = cluster.PendingTasksCount,
                ActiveServicesCount = cluster.ActiveServicesCount,
                RegisteredContainerInstancesCount = cluster.RegisteredContainerInstancesCount,
                Statistics = cluster.Statistics?.Select(stat => new
                {
                    Name = stat.Name,
                    Value = stat.Value
                }),
                Tags = cluster.Tags?.Select(tag => new
                {
                    Key = tag.Key,
                    Value = tag.Value
                })
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                clusters = clusterDetails,
                count = response.Clusters.Count,
                failures = response.Failures?.Select(f => new
                {
                    Arn = f.Arn,
                    Reason = f.Reason,
                    Detail = f.Detail
                })
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Create a new ECS cluster")]
    public async Task<string> CreateCluster(
        [Description("Name of the cluster to create")]
        string clusterName,
        [Description("Tags to apply to the cluster (JSON format: [{\"Key\":\"Environment\",\"Value\":\"Production\"}])")]
        string? tags = null)
    {
        try
        {
            List<Tag>? clusterTags = null;
            if (!string.IsNullOrEmpty(tags))
            {
                var tagData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(tags);
                clusterTags = tagData?.Select(t => new Tag { Key = t["Key"], Value = t["Value"] }).ToList();
            }

            CreateClusterResponse response = await _ecsService.CreateClusterAsync(clusterName, clusterTags);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster = new
                {
                    ClusterName = response.Cluster.ClusterName,
                    ClusterArn = response.Cluster.ClusterArn,
                    Status = response.Cluster.Status
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete an ECS cluster")]
    public async Task<string> DeleteCluster(
        [Description("Name or ARN of the cluster to delete")]
        string cluster)
    {
        try
        {
            DeleteClusterResponse response = await _ecsService.DeleteClusterAsync(cluster);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                cluster = new
                {
                    ClusterName = response.Cluster.ClusterName,
                    ClusterArn = response.Cluster.ClusterArn,
                    Status = response.Cluster.Status
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List services in an ECS cluster")]
    public async Task<string> ListServices(
        [Description("Name or ARN of the cluster (optional, lists from default cluster if not provided)")]
        string? cluster = null)
    {
        try
        {
            ListServicesResponse response = await _ecsService.ListServicesAsync(cluster);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                services = response.ServiceArns,
                count = response.ServiceArns.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe ECS services with detailed information")]
    public async Task<string> DescribeServices(
        [Description("Comma-separated list of service ARNs or names")]
        string services,
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null)
    {
        try
        {
            List<string> serviceList = services.Split(',').Select(s => s.Trim()).ToList();
            DescribeServicesResponse response = await _ecsService.DescribeServicesAsync(serviceList, cluster);
            
            var serviceDetails = response.Services.Select(service => new
            {
                ServiceName = service.ServiceName,
                ServiceArn = service.ServiceArn,
                ClusterArn = service.ClusterArn,
                TaskDefinition = service.TaskDefinition,
                Status = service.Status,
                RunningCount = service.RunningCount,
                PendingCount = service.PendingCount,
                DesiredCount = service.DesiredCount,
                LaunchType = service.LaunchType?.ToString(),
                PlatformVersion = service.PlatformVersion,
                CreatedAt = service.CreatedAt,
                LoadBalancers = service.LoadBalancers?.Select(lb => new
                {
                    TargetGroupArn = lb.TargetGroupArn,
                    LoadBalancerName = lb.LoadBalancerName,
                    ContainerName = lb.ContainerName,
                    ContainerPort = lb.ContainerPort
                }),
                Tags = service.Tags?.Select(tag => new
                {
                    Key = tag.Key,
                    Value = tag.Value
                })
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                services = serviceDetails,
                count = response.Services.Count,
                failures = response.Failures?.Select(f => new
                {
                    Arn = f.Arn,
                    Reason = f.Reason,
                    Detail = f.Detail
                })
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List tasks in an ECS cluster")]
    public async Task<string> ListTasks(
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null,
        [Description("Name or ARN of the service to filter tasks (optional)")]
        string? serviceName = null)
    {
        try
        {
            ListTasksResponse response = await _ecsService.ListTasksAsync(cluster, serviceName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                tasks = response.TaskArns,
                count = response.TaskArns.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe ECS tasks with detailed information")]
    public async Task<string> DescribeTasks(
        [Description("Comma-separated list of task ARNs")]
        string tasks,
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null)
    {
        try
        {
            List<string> taskList = tasks.Split(',').Select(t => t.Trim()).ToList();
            DescribeTasksResponse response = await _ecsService.DescribeTasksAsync(taskList, cluster);
            
            var taskDetails = response.Tasks.Select(task => new
            {
                TaskArn = task.TaskArn,
                TaskDefinitionArn = task.TaskDefinitionArn,
                ClusterArn = task.ClusterArn,
                LastStatus = task.LastStatus,
                DesiredStatus = task.DesiredStatus,
                HealthStatus = task.HealthStatus?.ToString(),
                LaunchType = task.LaunchType?.ToString(),
                PlatformVersion = task.PlatformVersion,
                Cpu = task.Cpu,
                Memory = task.Memory,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                StoppedAt = task.StoppedAt,
                StopCode = task.StopCode?.ToString(),
                StoppedReason = task.StoppedReason,
                Containers = task.Containers?.Select(container => new
                {
                    ContainerArn = container.ContainerArn,
                    Name = container.Name,
                    LastStatus = container.LastStatus,
                    ExitCode = container.ExitCode,
                    Reason = container.Reason,
                    HealthStatus = container.HealthStatus?.ToString(),
                    NetworkBindings = container.NetworkBindings?.Select(nb => new
                    {
                        BindIP = nb.BindIP,
                        ContainerPort = nb.ContainerPort,
                        HostPort = nb.HostPort,
                        Protocol = nb.Protocol?.ToString()
                    })
                }),
                Tags = task.Tags?.Select(tag => new
                {
                    Key = tag.Key,
                    Value = tag.Value
                })
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                tasks = taskDetails,
                count = response.Tasks.Count,
                failures = response.Failures?.Select(f => new
                {
                    Arn = f.Arn,
                    Reason = f.Reason,
                    Detail = f.Detail
                })
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List task definitions")]
    public async Task<string> ListTaskDefinitions(
        [Description("Family prefix to filter task definitions (optional)")]
        string? familyPrefix = null)
    {
        try
        {
            ListTaskDefinitionsResponse response = await _ecsService.ListTaskDefinitionsAsync(familyPrefix);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                taskDefinitions = response.TaskDefinitionArns,
                count = response.TaskDefinitionArns.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe a task definition with detailed information")]
    public async Task<string> DescribeTaskDefinition(
        [Description("Task definition ARN or family:revision")]
        string taskDefinition)
    {
        try
        {
            DescribeTaskDefinitionResponse response = await _ecsService.DescribeTaskDefinitionAsync(taskDefinition);
            
            TaskDefinition? taskDef = response.TaskDefinition;
            var result = new
            {
                TaskDefinitionArn = taskDef.TaskDefinitionArn,
                Family = taskDef.Family,
                Revision = taskDef.Revision,
                Status = taskDef.Status?.ToString(),
                Cpu = taskDef.Cpu,
                Memory = taskDef.Memory,
                NetworkMode = taskDef.NetworkMode?.ToString(),
                RequiresCompatibilities = taskDef.RequiresCompatibilities?.Select(rc => rc.ToString()),
                ExecutionRoleArn = taskDef.ExecutionRoleArn,
                TaskRoleArn = taskDef.TaskRoleArn,
                RegisteredAt = taskDef.RegisteredAt,
                RegisteredBy = taskDef.RegisteredBy,
                ContainerDefinitions = taskDef.ContainerDefinitions?.Select(cd => new
                {
                    Name = cd.Name,
                    Image = cd.Image,
                    Cpu = cd.Cpu,
                    Memory = cd.Memory,
                    MemoryReservation = cd.MemoryReservation,
                    Essential = cd.Essential,
                    PortMappings = cd.PortMappings?.Select(pm => new
                    {
                        ContainerPort = pm.ContainerPort,
                        HostPort = pm.HostPort,
                        Protocol = pm.Protocol?.ToString()
                    }),
                    Environment = cd.Environment?.Select(env => new
                    {
                        Name = env.Name,
                        Value = env.Value
                    }),
                    LogConfiguration = cd.LogConfiguration != null ? new
                    {
                        LogDriver = cd.LogConfiguration.LogDriver?.ToString(),
                        Options = cd.LogConfiguration.Options
                    } : null
                }),
                Tags = response.Tags?.Select(tag => new
                {
                    Key = tag.Key,
                    Value = tag.Value
                })
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskDefinition = result
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Run a task on ECS cluster")]
    public async Task<string> RunTask(
        [Description("Task definition ARN or family:revision")]
        string taskDefinition,
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null,
        [Description("Number of tasks to run (default: 1)")]
        int count = 1,
        [Description("Launch type: EC2 or FARGATE (default: EC2)")]
        string launchType = "EC2")
    {
        try
        {
            RunTaskResponse response = await _ecsService.RunTaskAsync(taskDefinition, cluster, count, launchType);
            
            var taskDetails = response.Tasks?.Select(task => new
            {
                TaskArn = task.TaskArn,
                TaskDefinitionArn = task.TaskDefinitionArn,
                ClusterArn = task.ClusterArn,
                LastStatus = task.LastStatus,
                DesiredStatus = task.DesiredStatus,
                LaunchType = task.LaunchType?.ToString(),
                CreatedAt = task.CreatedAt
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                tasks = taskDetails,
                count = response.Tasks?.Count ?? 0,
                failures = response.Failures?.Select(f => new
                {
                    Arn = f.Arn,
                    Reason = f.Reason,
                    Detail = f.Detail
                })
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Stop a running ECS task")]
    public async Task<string> StopTask(
        [Description("Task ARN to stop")]
        string task,
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null,
        [Description("Reason for stopping the task (optional)")]
        string? reason = null)
    {
        try
        {
            StopTaskResponse response = await _ecsService.StopTaskAsync(task, cluster, reason);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                task = new
                {
                    TaskArn = response.Task.TaskArn,
                    LastStatus = response.Task.LastStatus,
                    DesiredStatus = response.Task.DesiredStatus,
                    StoppedReason = response.Task.StoppedReason
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Update an ECS service")]
    public async Task<string> UpdateService(
        [Description("Service name or ARN")]
        string service,
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null,
        [Description("Desired number of tasks (optional)")]
        int? desiredCount = null,
        [Description("Task definition ARN or family:revision (optional)")]
        string? taskDefinition = null)
    {
        try
        {
            UpdateServiceResponse response = await _ecsService.UpdateServiceAsync(service, cluster, desiredCount, taskDefinition);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                service = new
                {
                    ServiceName = response.Service.ServiceName,
                    ServiceArn = response.Service.ServiceArn,
                    TaskDefinition = response.Service.TaskDefinition,
                    DesiredCount = response.Service.DesiredCount,
                    RunningCount = response.Service.RunningCount,
                    PendingCount = response.Service.PendingCount,
                    Status = response.Service.Status
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List container instances in an ECS cluster")]
    public async Task<string> ListContainerInstances(
        [Description("Name or ARN of the cluster (optional)")]
        string? cluster = null)
    {
        try
        {
            List<ContainerInstance> response = await _ecsService.ListContainerInstancesAsync(cluster);
            
            var instanceDetails = response.Select(instance => new
            {
                ContainerInstanceArn = instance.ContainerInstanceArn,
                Ec2InstanceId = instance.Ec2InstanceId,
                Status = instance.Status,
                RunningTasksCount = instance.RunningTasksCount,
                PendingTasksCount = instance.PendingTasksCount,
                AgentConnected = instance.AgentConnected,
                VersionInfo = instance.VersionInfo != null ? new
                {
                    AgentVersion = instance.VersionInfo.AgentVersion,
                    AgentHash = instance.VersionInfo.AgentHash,
                    DockerVersion = instance.VersionInfo.DockerVersion
                } : null,
                RegisteredResources = instance.RegisteredResources?.Select(resource => new
                {
                    Name = resource.Name,
                    Type = resource.Type,
                    IntegerValue = resource.IntegerValue,
                    DoubleValue = resource.DoubleValue,
                    LongValue = resource.LongValue,
                    StringSetValue = resource.StringSetValue
                }),
                RemainingResources = instance.RemainingResources?.Select(resource => new
                {
                    Name = resource.Name,
                    Type = resource.Type,
                    IntegerValue = resource.IntegerValue,
                    DoubleValue = resource.DoubleValue,
                    LongValue = resource.LongValue,
                    StringSetValue = resource.StringSetValue
                })
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerInstances = instanceDetails,
                count = response.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
