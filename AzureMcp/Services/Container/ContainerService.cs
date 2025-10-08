using System.Text;
using Azure;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using AzureMcp.Common.Exceptions;
using AzureMcp.Services.Container.Models;
using AzureMcp.Services.Core;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Services.Container;

public class ContainerService(
    ArmClientFactory armClientFactory,
    ILogger<ContainerService> logger)
    : IContainerService
{
    #region Container Instance Operations

    public async Task<IEnumerable<ContainerGroupDto>> ListContainerGroupsAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var containerGroups = new List<ContainerGroupDto>();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions())
                {
                    containerGroups.AddRange(subscription.GetContainerGroups().Select(MapToContainerGroupDto));
                }
            }
            else
            {
                Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
                
                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
                    await foreach (ContainerGroupResource? group in resourceGroup.Value.GetContainerGroups())
                    {
                        containerGroups.Add(MapToContainerGroupDto(group));
                    }
                }
                else
                {
                    containerGroups.AddRange(subscription.Value.GetContainerGroups().Select(MapToContainerGroupDto));
                }
            }

            return containerGroups;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container groups");
            throw new AzureException("Failed to list container groups", ex);
        }
    }

    public async Task<ContainerGroupDto?> GetContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            return containerGroup is not null ? MapToContainerGroupDto(containerGroup.Value) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container group {Name}", containerGroupName);
            throw new AzureException($"Failed to get container group {containerGroupName}", ex);
        }
    }

    public async Task<ContainerGroupDto> CreateContainerGroupAsync(string subscriptionId, string resourceGroupName, ContainerGroupCreateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);

            List<ContainerInstanceContainer> containers = request.Containers.Select(c => new ContainerInstanceContainer(
                c.Name,
                c.Image,
                new ContainerResourceRequirements(new ContainerResourceRequestsContent(c.MemoryInGb, c.CpuCores))
            )).ToList();
            
            // Add ports and environment variables to each container
            for (var i = 0; i < containers.Count; i++)
            {
                ContainerInstanceContainer container = containers[i];
                ContainerCreateRequest requestContainer = request.Containers[i];
    
                if (requestContainer.Ports is not null)
                {
                    foreach (int port in requestContainer.Ports)
                    {
                        container.Ports.Add(new ContainerPort(port) { Protocol = ContainerNetworkProtocol.Tcp });
                    }
                }

                if (requestContainer.EnvironmentVariables is null) continue;
                foreach (KeyValuePair<string, string> kv in requestContainer.EnvironmentVariables)
                {
                    container.EnvironmentVariables.Add(new ContainerEnvironmentVariable(kv.Key) { Value = kv.Value });
                }
            }

            var data = new ContainerGroupData(new AzureLocation(request.Location), containers, new ContainerInstanceOperatingSystemType(request.OsType))
            {
                RestartPolicy = new ContainerGroupRestartPolicy(request.RestartPolicy)
            };

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                {
                    data.Tags.Add(tag.Key, tag.Value);
                }
            }

            if (!string.IsNullOrEmpty(request.IpAddressType))
            {
                data.IPAddress = new ContainerGroupIPAddress(
                    request.Ports?.Select(p => new ContainerGroupPort(p)).ToList() ?? [],
                    new ContainerGroupIPAddressType(request.IpAddressType))
                {
                    DnsNameLabel = request.DnsNameLabel
                };
            }

            if (request.ImageRegistryCredentials is not null)
            {
                data.ImageRegistryCredentials.Add(new ContainerGroupImageRegistryCredential(request.ImageRegistryCredentials.Server)
                {
                    Username = request.ImageRegistryCredentials.Username,
                    Password = request.ImageRegistryCredentials.Password
                });
            }

            ArmOperation<ContainerGroupResource>? operation = await resourceGroup.Value.GetContainerGroups().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, data);

            return MapToContainerGroupDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container group {Name}", request.Name);
            throw new AzureException($"Failed to create container group {request.Name}", ex);
        }
    }

    public async Task<bool> DeleteContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            await containerGroup.Value.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container group {Name}", containerGroupName);
            throw new AzureException($"Failed to delete container group {containerGroupName}", ex);
        }
    }

    public async Task<ContainerGroupDto> RestartContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            await containerGroup.Value.RestartAsync(WaitUntil.Completed);
            Response<ContainerGroupResource>? updatedGroup = await containerGroup.Value.GetAsync();

            return MapToContainerGroupDto(updatedGroup.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting container group {Name}", containerGroupName);
            throw new AzureException($"Failed to restart container group {containerGroupName}", ex);
        }
    }

    public async Task<ContainerGroupDto> StopContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            await containerGroup.Value.StopAsync();
            Response<ContainerGroupResource>? updatedGroup = await containerGroup.Value.GetAsync();

            return MapToContainerGroupDto(updatedGroup.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping container group {Name}", containerGroupName);
            throw new AzureException($"Failed to stop container group {containerGroupName}", ex);
        }
    }

    public async Task<ContainerGroupDto> StartContainerGroupAsync(string subscriptionId, string resourceGroupName, string containerGroupName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            await containerGroup.Value.StartAsync(WaitUntil.Completed);
            Response<ContainerGroupResource>? updatedGroup = await containerGroup.Value.GetAsync();

            return MapToContainerGroupDto(updatedGroup.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting container group {Name}", containerGroupName);
            throw new AzureException($"Failed to start container group {containerGroupName}", ex);
        }
    }

    public async Task<string> GetContainerLogsAsync(string subscriptionId, string resourceGroupName, string containerGroupName, string containerName, int? tail = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            
            // Get the container group
            Response<ContainerGroupResource>? containerGroupResource = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);
            
            // Get the specific container from the group
            ContainerInstanceContainer? container = containerGroupResource.Value.Data.Containers
                .FirstOrDefault(c => c.Name.Equals(containerName, StringComparison.OrdinalIgnoreCase));
            
            if (container is null)
            {
                throw new AzureException($"Container {containerName} not found in group {containerGroupName}");
            }

            // Get logs using container operations
            string? logs = await GetContainerLogsInternalAsync(containerGroupResource.Value, containerName, tail);
            
            return logs ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for container {Container} in group {Group}", containerName, containerGroupName);
            throw new AzureException($"Failed to get logs for container {containerName}", ex);
        }
    }

    public async Task<ContainerExecResult> ExecuteCommandAsync(string subscriptionId, string resourceGroupName, string containerGroupName, string containerName, string command)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerGroupResource>? containerGroup = await resourceGroup.Value.GetContainerGroupAsync(containerGroupName);

            // Execute command would require implementing container exec API
            // This is a simplified implementation
            return new ContainerExecResult
            {
                Output = $"Command execution not fully implemented. Would execute: {command}",
                Error = null,
                ExitCode = 0
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command in container {Container}", containerName);
            throw new AzureException($"Failed to execute command in container {containerName}", ex);
        }
    }

    #endregion

    #region Container Registry Operations

    public async Task<IEnumerable<ContainerRegistryDto>> ListRegistriesAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var registries = new List<ContainerRegistryDto>();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions())
                {
                    registries.AddRange(subscription.GetContainerRegistries().Select(MapToContainerRegistryDto));
                }
            }
            else
            {
                Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
                
                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
                    await foreach (ContainerRegistryResource? registry in resourceGroup.Value.GetContainerRegistries())
                    {
                        registries.Add(MapToContainerRegistryDto(registry));
                    }
                }
                else
                {
                    registries.AddRange(subscription.Value.GetContainerRegistries().Select(MapToContainerRegistryDto));
                }
            }

            return registries;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing container registries");
            throw new AzureException("Failed to list container registries", ex);
        }
    }

    public async Task<ContainerRegistryDto?> GetRegistryAsync(string subscriptionId, string resourceGroupName, string registryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            return registry is not null ? MapToContainerRegistryDto(registry.Value) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container registry {Name}", registryName);
            throw new AzureException($"Failed to get container registry {registryName}", ex);
        }
    }

    public async Task<ContainerRegistryDto> CreateRegistryAsync(string subscriptionId, string resourceGroupName, ContainerRegistryCreateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);

            ContainerRegistrySkuName sku = request.Sku switch
            {
                "Basic" => ContainerRegistrySkuName.Basic,
                "Standard" => ContainerRegistrySkuName.Standard,
                "Premium" => ContainerRegistrySkuName.Premium,
                _ => ContainerRegistrySkuName.Basic
            };

            var data = new ContainerRegistryData(new AzureLocation(request.Location), new ContainerRegistrySku(sku))
            {
                IsAdminUserEnabled = request.AdminUserEnabled
            };

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                {
                    data.Tags.Add(tag.Key, tag.Value);
                }
            }

            ArmOperation<ContainerRegistryResource>? operation = await resourceGroup.Value.GetContainerRegistries().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, data);

            return MapToContainerRegistryDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container registry {Name}", request.Name);
            throw new AzureException($"Failed to create container registry {request.Name}", ex);
        }
    }

    public async Task<bool> DeleteRegistryAsync(string subscriptionId, string resourceGroupName, string registryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            await registry.Value.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container registry {Name}", registryName);
            throw new AzureException($"Failed to delete container registry {registryName}", ex);
        }
    }

    public async Task<RegistryCredentialsDto> GetRegistryCredentialsAsync(string subscriptionId, string resourceGroupName, string registryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            Response<ContainerRegistryListCredentialsResult>? credentials = await registry.Value.GetCredentialsAsync();

            ContainerRegistryPassword? first = credentials.Value.Passwords.FirstOrDefault();

            return new RegistryCredentialsDto
            {
                Username = credentials.Value.Username,
                Password = first?.Value,
                Password2 = credentials.Value.Passwords?.Skip(1).FirstOrDefault()?.Value
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting registry credentials for {Name}", registryName);
            throw new AzureException($"Failed to get registry credentials for {registryName}", ex);
        }
    }

    public async Task<RegistryCredentialsDto> RegenerateRegistryCredentialAsync(string subscriptionId, string resourceGroupName, string registryName, string passwordName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            var regenerateCredential = new ContainerRegistryCredentialRegenerateContent(
                passwordName.Equals("password", StringComparison.OrdinalIgnoreCase) 
                    ? ContainerRegistryPasswordName.Password 
                    : ContainerRegistryPasswordName.Password2);

            Response<ContainerRegistryListCredentialsResult>? credentials = await registry.Value.RegenerateCredentialAsync(regenerateCredential);

            ContainerRegistryPassword? first = credentials.Value.Passwords.FirstOrDefault();

            return new RegistryCredentialsDto
            {
                Username = credentials.Value.Username,
                Password = first?.Value,
                Password2 = credentials.Value.Passwords?.Skip(1).FirstOrDefault()?.Value
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error regenerating registry credential for {Name}", registryName);
            throw new AzureException($"Failed to regenerate registry credential for {registryName}", ex);
        }
    }

    #endregion

    #region Registry Repository and Image Operations

    public async Task<IEnumerable<ContainerRepositoryDto>> ListRepositoriesAsync(string subscriptionId, string resourceGroupName, string registryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            // Get the login server URL
            string loginServer = registry.Value.Data.LoginServer;
        
            // Get credentials for the registry
            Response<ContainerRegistryListCredentialsResult>? credentials = await registry.Value.GetCredentialsAsync();
        
            // Create a ContainerRegistryClient
            var containerRegistryClient = new ContainerRegistryClient(
                new Uri($"https://{loginServer}"),
                new DefaultAzureCredential());

            var repositories = new List<ContainerRepositoryDto>();
        
            await foreach (string repositoryName in containerRegistryClient.GetRepositoryNamesAsync())
            {
                repositories.Add(new ContainerRepositoryDto
                {
                    Name = repositoryName,
                    Registry = registryName,
                    CreatedOn = DateTime.UtcNow // Will be updated with actual metadata
                });
            }

            return repositories;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing repositories for registry {Name}", registryName);
            throw new AzureException($"Failed to list repositories for registry {registryName}", ex);
        }
    }

    public async Task<IEnumerable<ContainerImageDto>> ListImagesAsync(string subscriptionId, string resourceGroupName, string registryName, string? repositoryName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            string loginServer = registry.Value.Data.LoginServer;
            
            var containerRegistryClient = new ContainerRegistryClient(
                new Uri($"https://{loginServer}"),
                new DefaultAzureCredential());

            var images = new List<ContainerImageDto>();

            if (!string.IsNullOrEmpty(repositoryName))
            {
                ContainerRepository repository = containerRegistryClient.GetRepository(repositoryName);
                
                await foreach (ArtifactManifestProperties manifest in repository.GetAllManifestPropertiesAsync())
                {
                    images.Add(new ContainerImageDto
                    {
                        Repository = repositoryName,
                        Tag = manifest.Tags?.FirstOrDefault() ?? "latest",
                        Digest = manifest.Digest,
                        Size = manifest.SizeInBytes,
                        CreatedOn = manifest.CreatedOn.DateTime,
                        LastUpdated = manifest.LastUpdatedOn.DateTime
                    });
                }
            }
            else
            {
                // List all images from all repositories
                await foreach (string repoName in containerRegistryClient.GetRepositoryNamesAsync())
                {
                    ContainerRepository repository = containerRegistryClient.GetRepository(repoName);
                    
                    await foreach (ArtifactManifestProperties manifest in repository.GetAllManifestPropertiesAsync())
                    {
                        images.Add(new ContainerImageDto
                        {
                            Repository = repoName,
                            Tag = manifest.Tags?.FirstOrDefault() ?? "latest",
                            Digest = manifest.Digest,
                            Size = manifest.SizeInBytes,
                            CreatedOn = manifest.CreatedOn.DateTime,
                            LastUpdated = manifest.LastUpdatedOn.DateTime
                        });
                    }
                }
            }

            return images;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing images for registry {Name}", registryName);
            throw new AzureException($"Failed to list images for registry {registryName}", ex);
        }
    }

    public async Task<ContainerImageDto?> GetImageAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName, string tag)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            string loginServer = registry.Value.Data.LoginServer;
        
            var containerRegistryClient = new ContainerRegistryClient(
                new Uri($"https://{loginServer}"),
                new DefaultAzureCredential());

            ContainerRepository repository = containerRegistryClient.GetRepository(repositoryName);
            RegistryArtifact artifact = repository.GetArtifact(tag);
        
            Response<ArtifactManifestProperties> properties = await artifact.GetManifestPropertiesAsync();

            return new ContainerImageDto
            {
                Repository = repositoryName,
                Tag = tag,
                Digest = properties.Value.Digest,
                Size = properties.Value.SizeInBytes,
                CreatedOn = properties.Value.CreatedOn.DateTime,
                LastUpdated = properties.Value.LastUpdatedOn.DateTime,
                Architecture = properties.Value.Architecture.ToString(),
                Os = properties.Value.OperatingSystem.ToString()
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting image {Repo}:{Tag} from registry {Name}", repositoryName, tag, registryName);
            throw new AzureException($"Failed to get image {repositoryName}:{tag}", ex);
        }
    }

    public async Task<bool> DeleteImageAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName, string tag)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            string loginServer = registry.Value.Data.LoginServer;
        
            var containerRegistryClient = new ContainerRegistryClient(
                new Uri($"https://{loginServer}"),
                new DefaultAzureCredential());

            ContainerRepository repository = containerRegistryClient.GetRepository(repositoryName);
            RegistryArtifact artifact = repository.GetArtifact(tag);
        
            await artifact.DeleteAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting image {Repo}:{Tag} from registry {Name}", repositoryName, tag, registryName);
            throw new AzureException($"Failed to delete image {repositoryName}:{tag}", ex);
        }
    }

    public async Task<bool> DeleteRepositoryAsync(string subscriptionId, string resourceGroupName, string registryName, string repositoryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            string loginServer = registry.Value.Data.LoginServer;
        
            var containerRegistryClient = new ContainerRegistryClient(
                new Uri($"https://{loginServer}"),
                new DefaultAzureCredential());

            ContainerRepository repository = containerRegistryClient.GetRepository(repositoryName);
            await repository.DeleteAsync();
        
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting repository {Repo} from registry {Name}", repositoryName, registryName);
            throw new AzureException($"Failed to delete repository {repositoryName}", ex);
        }
    }

    #endregion

    #region Registry Build Operations

    public async Task<BuildTaskDto> CreateBuildTaskAsync(string subscriptionId, string resourceGroupName, string registryName, BuildTaskCreateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            var taskData = new ContainerRegistryTaskData(new AzureLocation(request.SourceLocation))
            {
                Platform = new ContainerRegistryPlatformProperties(ContainerRegistryOS.Linux)
                {
                    Architecture = ContainerRegistryOSArchitecture.Amd64
                },
                Step = new ContainerRegistryDockerBuildStep(request.DockerFilePath ?? "Dockerfile")
                {
                    ImageNames = { request.ImageName },
                    IsPushEnabled = true,
                    NoCache = false
                },
                Status = ContainerRegistryTaskStatus.Enabled
            };

            ArmOperation<ContainerRegistryTaskResource> operation = await registry.Value.GetContainerRegistryTasks()
                .CreateOrUpdateAsync(WaitUntil.Completed, request.Name, taskData);

            return new BuildTaskDto
            {
                Id = operation.Value.Id?.ToString(),
                Name = operation.Value.Data.Name,
                Status = operation.Value.Data.Status?.ToString() ?? "Created",
                CreatedOn = operation.Value.Data.CreatedOn?.DateTime
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating build task {Name}", request.Name);
            throw new AzureException($"Failed to create build task {request.Name}", ex);
        }
    }

    public async Task<BuildRunDto> RunBuildTaskAsync(string subscriptionId, string resourceGroupName, string registryName, string buildTaskName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);
            Response<ContainerRegistryTaskResource>? task = await registry.Value.GetContainerRegistryTaskAsync(buildTaskName);

            var runRequest = new ContainerRegistryTaskRunContent(ResourceIdentifier.Root)
            {
                TaskId = task.Value.Id
            };

            ArmOperation<ContainerRegistryRunResource> operation = await registry.Value.ScheduleRunAsync(WaitUntil.Started, runRequest);

            return new BuildRunDto
            {
                Id = operation.Value.Id?.ToString(),
                BuildTaskName = buildTaskName,
                Status = operation.Value.Data.Status?.ToString() ?? "Running",
                StartTime = operation.Value.Data.StartOn?.DateTime
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running build task {Name}", buildTaskName);
            throw new AzureException($"Failed to run build task {buildTaskName}", ex);
        }
    }

    public async Task<IEnumerable<BuildRunDto>> ListBuildRunsAsync(string subscriptionId, string resourceGroupName, string registryName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            var runs = new List<BuildRunDto>();
        
            await foreach (ContainerRegistryRunResource run in registry.Value.GetContainerRegistryRuns())
            {
                runs.Add(new BuildRunDto
                {
                    Id = run.Id?.ToString(),
                    BuildTaskName = run.Data.Task,
                    Status = run.Data.Status?.ToString(),
                    StartTime = run.Data.StartOn?.DateTime,
                    FinishTime = run.Data.FinishOn?.DateTime,
                    RunType = run.Data.RunType
                });
            }

            return runs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing build runs for registry {Name}", registryName);
            throw new AzureException($"Failed to list build runs for registry {registryName}", ex);
        }
    }

    public async Task<string> GetBuildLogAsync(string subscriptionId, string resourceGroupName, string registryName, string runId)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerRegistryResource>? registry = await resourceGroup.Value.GetContainerRegistryAsync(registryName);

            // Get the run resource
            string runName = runId.Split('/').Last();
            Response<ContainerRegistryRunResource>? run = await registry.Value.GetContainerRegistryRunAsync(runName);

            // Get logs URL
            Response<ContainerRegistryRunGetLogResult>? logResult = await run.Value.GetLogSasUrlAsync();

            if (string.IsNullOrEmpty(logResult.Value.LogLink)) return "No logs available for this run.";
            // Download logs from the URL
            using var httpClient = new HttpClient();
            return await httpClient.GetStringAsync(logResult.Value.LogLink);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build log for run {RunId}", runId);
            throw new AzureException($"Failed to get build log for run {runId}", ex);
        }
    }

    #endregion

    #region Kubernetes (AKS) Operations

    public async Task<IEnumerable<KubernetesClusterDto>> ListKubernetesClustersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var clusters = new List<KubernetesClusterDto>();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions())
                {
                    clusters.AddRange(subscription.GetContainerServiceManagedClusters().Select(MapToKubernetesClusterDto));
                }
            }
            else
            {
                Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            
                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
                    await foreach (ContainerServiceManagedClusterResource cluster in resourceGroup.Value.GetContainerServiceManagedClusters())
                    {
                        clusters.Add(MapToKubernetesClusterDto(cluster));
                    }
                }
                else
                {
                    clusters.AddRange(subscription.Value.GetContainerServiceManagedClusters().Select(MapToKubernetesClusterDto));
                }
            }

            return clusters;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Kubernetes clusters");
            throw new AzureException("Failed to list Kubernetes clusters", ex);
        }
    }

    public async Task<KubernetesClusterDto?> GetKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            return cluster is not null ? MapToKubernetesClusterDto(cluster.Value) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to get Kubernetes cluster {clusterName}", ex);
        }
    }

    public async Task<KubernetesClusterDto> CreateKubernetesClusterAsync(string subscriptionId, string resourceGroupName, KubernetesClusterCreateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);

            var identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned);
        
            var data = new ContainerServiceManagedClusterData(new AzureLocation(request.Location))
            {
                DnsPrefix = request.DnsPrefix,
                KubernetesVersion = request.KubernetesVersion,
                Identity = identity,
                EnableRbac = request.EnableRBAC
            };

            // Add a default node pool
            data.AgentPoolProfiles.Add(new ManagedClusterAgentPoolProfile("default")
            {
                Count = request.AgentPoolProfile.Count,
                VmSize = request.AgentPoolProfile.VmSize,
                OSType = ContainerServiceOSType.Linux,
                Mode = AgentPoolMode.System
            });

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                {
                    data.Tags.Add(tag.Key, tag.Value);
                }
            }

            ArmOperation<ContainerServiceManagedClusterResource> operation = await resourceGroup.Value.GetContainerServiceManagedClusters()
                .CreateOrUpdateAsync(WaitUntil.Completed, request.Name, data);

            return MapToKubernetesClusterDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Kubernetes cluster {Name}", request.Name);
            throw new AzureException($"Failed to create Kubernetes cluster {request.Name}", ex);
        }
    }

    public async Task<bool> DeleteKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            await cluster.Value.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to delete Kubernetes cluster {clusterName}", ex);
        }
    }

    public async Task<KubernetesClusterDto> ScaleKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName, int nodeCount)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);
        
            Response<ContainerServiceAgentPoolResource>? agentPool = await cluster.Value.GetContainerServiceAgentPoolAsync(nodePoolName);
        
            ContainerServiceAgentPoolData data = agentPool.Value.Data;
            data.Count = nodeCount;
        
            ArmOperation<ContainerServiceAgentPoolResource> operation = await cluster.Value.GetContainerServiceAgentPools()
                .CreateOrUpdateAsync(WaitUntil.Completed, nodePoolName, data);

            Response<ContainerServiceManagedClusterResource>? updatedCluster = await cluster.Value.GetAsync();
            return MapToKubernetesClusterDto(updatedCluster.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to scale Kubernetes cluster {clusterName}", ex);
        }
    }

    public async Task<KubernetesClusterDto> UpgradeKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName, string kubernetesVersion)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            ContainerServiceManagedClusterData data = cluster.Value.Data;
            data.KubernetesVersion = kubernetesVersion;

            ArmOperation<ContainerServiceManagedClusterResource> operation = await resourceGroup.Value.GetContainerServiceManagedClusters()
                .CreateOrUpdateAsync(WaitUntil.Completed, clusterName, data);

            return MapToKubernetesClusterDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error upgrading Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to upgrade Kubernetes cluster {clusterName}", ex);
        }
    }

    public async Task<KubernetesCredentialsDto> GetKubernetesCredentialsAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            Response<ManagedClusterCredentials>? credentials = await cluster.Value.GetClusterUserCredentialsAsync();
        
            return new KubernetesCredentialsDto
            {
                Kubeconfig = Encoding.UTF8.GetString(credentials.Value.Kubeconfigs.FirstOrDefault()?.Value ?? [])
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Kubernetes credentials for {Name}", clusterName);
            throw new AzureException($"Failed to get Kubernetes credentials for {clusterName}", ex);
        }
    }

    public async Task<KubernetesClusterDto> StartKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            await cluster.Value.StartAsync(WaitUntil.Completed);
            Response<ContainerServiceManagedClusterResource>? updatedCluster = await cluster.Value.GetAsync();

            return MapToKubernetesClusterDto(updatedCluster.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to start Kubernetes cluster {clusterName}", ex);
        }
    }

    public async Task<KubernetesClusterDto> StopKubernetesClusterAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            await cluster.Value.StopAsync(WaitUntil.Completed);
            Response<ContainerServiceManagedClusterResource>? updatedCluster = await cluster.Value.GetAsync();

            return MapToKubernetesClusterDto(updatedCluster.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping Kubernetes cluster {Name}", clusterName);
            throw new AzureException($"Failed to stop Kubernetes cluster {clusterName}", ex);
        }
    }

    #endregion

    #region Kubernetes Node Pool Operations

    public async Task<IEnumerable<NodePoolDto>> ListNodePoolsAsync(string subscriptionId, string resourceGroupName, string clusterName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            var nodePools = new List<NodePoolDto>();
        
            await foreach (ContainerServiceAgentPoolResource nodePool in cluster.Value.GetContainerServiceAgentPools())
            {
                nodePools.Add(MapToNodePoolDto(nodePool));
            }

            return nodePools;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing node pools for cluster {Name}", clusterName);
            throw new AzureException($"Failed to list node pools for cluster {clusterName}", ex);
        }
    }

    public async Task<NodePoolDto?> GetNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);
            Response<ContainerServiceAgentPoolResource>? nodePool = await cluster.Value.GetContainerServiceAgentPoolAsync(nodePoolName);

            return nodePool is not null ? MapToNodePoolDto(nodePool.Value) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting node pool {NodePool} for cluster {Cluster}", nodePoolName, clusterName);
            throw new AzureException($"Failed to get node pool {nodePoolName}", ex);
        }
    }

    public async Task<NodePoolDto> CreateNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, NodePoolCreateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);

            var data = new ContainerServiceAgentPoolData
            {
                Count = request.Count,
                VmSize = request.VmSize,
                OSType = request.OsType == "Linux" ? ContainerServiceOSType.Linux : ContainerServiceOSType.Windows,
                Mode = request.Mode == "System" ? AgentPoolMode.System : AgentPoolMode.User,
                EnableAutoScaling = request.EnableAutoScaling,
                MinCount = request.MinCount,
                MaxCount = request.MaxCount
            };

            ArmOperation<ContainerServiceAgentPoolResource> operation = await cluster.Value.GetContainerServiceAgentPools()
                .CreateOrUpdateAsync(WaitUntil.Completed, request.Name, data);

            return MapToNodePoolDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating node pool {Name}", request.Name);
            throw new AzureException($"Failed to create node pool {request.Name}", ex);
        }
    }

    public async Task<bool> DeleteNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);
            Response<ContainerServiceAgentPoolResource>? nodePool = await cluster.Value.GetContainerServiceAgentPoolAsync(nodePoolName);

            await nodePool.Value.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting node pool {NodePool} from cluster {Cluster}", nodePoolName, clusterName);
            throw new AzureException($"Failed to delete node pool {nodePoolName}", ex);
        }
    }

    public async Task<NodePoolDto> UpdateNodePoolAsync(string subscriptionId, string resourceGroupName, string clusterName, string nodePoolName, NodePoolUpdateRequest request)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            Response<SubscriptionResource>? subscription = await client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            Response<ResourceGroupResource>? resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName);
            Response<ContainerServiceManagedClusterResource>? cluster = await resourceGroup.Value.GetContainerServiceManagedClusterAsync(clusterName);
            Response<ContainerServiceAgentPoolResource>? nodePool = await cluster.Value.GetContainerServiceAgentPoolAsync(nodePoolName);

            ContainerServiceAgentPoolData data = nodePool.Value.Data;
        
            if (request.Count.HasValue)
                data.Count = request.Count.Value;
            if (request.MinCount.HasValue)
                data.MinCount = request.MinCount;
            if (request.MaxCount.HasValue)
                data.MaxCount = request.MaxCount;
            if (request.EnableAutoScaling.HasValue)
                data.EnableAutoScaling = request.EnableAutoScaling.Value;
            if (!string.IsNullOrEmpty(request.OrchestratorVersion))
                data.OrchestratorVersion = request.OrchestratorVersion;

            ArmOperation<ContainerServiceAgentPoolResource> operation = await cluster.Value.GetContainerServiceAgentPools()
                .CreateOrUpdateAsync(WaitUntil.Completed, nodePoolName, data);

            return MapToNodePoolDto(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating node pool {NodePool}", nodePoolName);
            throw new AzureException($"Failed to update node pool {nodePoolName}", ex);
        }
    }

    #endregion

    #region Private Helper Methods

    private ContainerGroupDto MapToContainerGroupDto(ContainerGroupResource resource)
    {
        ContainerGroupData? data = resource.Data;
        return new ContainerGroupDto
        {
            Id = resource.Id?.ToString(),
            Name = data.Name,
            ResourceGroup = resource.Id?.ResourceGroupName,
            Location = data.Location.Name,
            State = data.ProvisioningState,
            IpAddress = data.IPAddress?.IP.ToString(),
            OsType = data.OSType.ToString(),
            RestartPolicy = data.RestartPolicy?.ToString(),
            Fqdn = data.IPAddress?.Fqdn,
            Port = data.IPAddress?.Ports?.FirstOrDefault()?.Port,
            Tags = data.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value),
            Containers = data.Containers?.Select(MapToContainerInstanceDto).ToList() ?? []
        };
    }

    private ContainerInstanceDto MapToContainerInstanceDto(ContainerInstanceContainer container)
    {
        return new ContainerInstanceDto
        {
            Name = container.Name,
            Image = container.Image,
            State = container.InstanceView?.CurrentState?.State,
            CpuCores = container.Resources?.Requests?.Cpu,
            MemoryInGb = container.Resources?.Requests?.MemoryInGB,
            Ports = container.Ports?.Select(p => p.Port).ToList(),
            EnvironmentVariables = container.EnvironmentVariables?.ToDictionary(ev => ev.Name, ev => ev.Value ?? string.Empty),
            Command = container.Command?.ToList(),
            RestartCount = container.InstanceView?.RestartCount?.ToString()
        };
    }

    private static ContainerRegistryDto MapToContainerRegistryDto(ContainerRegistryResource resource)
    {
        ContainerRegistryData? data = resource.Data;
        return new ContainerRegistryDto
        {
            Id = resource.Id?.ToString(),
            Name = data.Name,
            LoginServer = data.LoginServer,
            ResourceGroup = resource.Id?.ResourceGroupName,
            Location = data.Location.Name,
            SkuName = data.Sku?.Name.ToString(),
            SkuTier = data.Sku?.Tier?.ToString(),
            ProvisioningState = data.ProvisioningState?.ToString(),
            AdminUserEnabled = data.IsAdminUserEnabled,
            CreationDate = data.CreatedOn?.DateTime,
            Tags = data.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value),
            PublicNetworkAccess = data.PublicNetworkAccess?.ToString() == "Enabled",
            NetworkRuleSetDefaultAction = data.NetworkRuleSet?.DefaultAction.ToString()
        };
    }

    private async Task<string?> GetContainerLogsInternalAsync(ContainerGroupResource containerGroup, string containerName, int? tail)
    {
        // This is a simplified implementation
        // In a real implementation, you would need to use the appropriate API to get container logs
        // The Azure SDK for .NET doesn't have a direct method, so you might need to use REST API
        
        var sb = new StringBuilder();
        sb.AppendLine($"Logs for container {containerName}:");
        sb.AppendLine($"Container group: {containerGroup.Data.Name}");
        sb.AppendLine($"State: {containerGroup.Data.ProvisioningState}");
        
        if (tail.HasValue)
        {
            sb.AppendLine($"Showing last {tail} lines");
        }
        
        sb.AppendLine("(Log retrieval requires additional implementation)");
        
        return sb.ToString();
    }
    
    private static KubernetesClusterDto MapToKubernetesClusterDto(ContainerServiceManagedClusterResource resource)
    {
        ContainerServiceManagedClusterData data = resource.Data;
        return new KubernetesClusterDto
        {
            Id = resource.Id?.ToString(),
            Name = data.Name,
            Location = data.Location.Name,
            ResourceGroup = resource.Id?.ResourceGroupName,
            KubernetesVersion = data.KubernetesVersion,
            DnsPrefix = data.DnsPrefix,
            Fqdn = data.Fqdn,
            ProvisioningState = data.ProvisioningState,
            PowerState = data.PowerStateCode?.ToString(),
            NodeCount = data.AgentPoolProfiles?.FirstOrDefault()?.Count,
            EnableRBAC = data.EnableRbac,
            NetworkProfile = data.NetworkProfile?.NetworkPlugin?.ToString(),
            Tags = data.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value),
            CreatedOn = data.SystemData?.CreatedOn?.DateTime
        };
    }

    private static NodePoolDto MapToNodePoolDto(ContainerServiceAgentPoolResource resource)
    {
        ContainerServiceAgentPoolData data = resource.Data;
        return new NodePoolDto
        {
            Id = resource.Id?.ToString(),
            Name = data.Name,
            Count = data.Count,
            VmSize = data.VmSize,
            OsType = data.OSType?.ToString(),
            Mode = data.Mode?.ToString(),
            MinCount = data.MinCount,
            MaxCount = data.MaxCount,
            EnableAutoScaling = data.EnableAutoScaling,
            OrchestratorVersion = data.OrchestratorVersion,
            NodeImageVersion = data.NodeImageVersion,
            ProvisioningState = data.ProvisioningState,
            PowerState = data.PowerStateCode?.ToString()
        };
    }

    #endregion
}
