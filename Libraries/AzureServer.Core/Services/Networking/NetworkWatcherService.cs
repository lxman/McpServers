using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;
using Microsoft.Extensions.Logging;
using NextHopResult = AzureServer.Core.Services.Networking.Models.NextHopResult;
using SecurityGroupViewResult = AzureServer.Core.Services.Networking.Models.SecurityGroupViewResult;

namespace AzureServer.Core.Services.Networking;

public class NetworkWatcherService(ArmClientFactory armClientFactory, ILogger<NetworkWatcherService> logger) : INetworkWatcherService
{
    public async Task<IEnumerable<NetworkWatcherDto>> ListNetworkWatchersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var watchers = new List<NetworkWatcherDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    // List watchers in specific resource group
                    ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (NetworkWatcherResource? watcher in resourceGroup.GetNetworkWatchers())
                    {
                        watchers.Add(new NetworkWatcherDto
                        {
                            Id = watcher.Data.Id?.ToString(),
                            Name = watcher.Data.Name,
                            Location = watcher.Data.Location?.Name,
                            ProvisioningState = watcher.Data.ProvisioningState?.ToString(),
                            Tags = watcher.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
                        });
                    }

                    break;
                }
                case false:
                {
                    // List watchers in specific subscription
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (NetworkWatcherResource? watcher in subscription.GetNetworkWatchersAsync())
                    {
                        watchers.Add(new NetworkWatcherDto
                        {
                            Id = watcher.Data.Id?.ToString(),
                            Name = watcher.Data.Name,
                            Location = watcher.Data.Location?.Name,
                            ProvisioningState = watcher.Data.ProvisioningState?.ToString(),
                            Tags = watcher.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
                        });
                    }

                    break;
                }
                default:
                {
                    // List watchers across all subscriptions
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                    {
                        await foreach (NetworkWatcherResource? watcher in subscription.GetNetworkWatchersAsync())
                        {
                            watchers.Add(new NetworkWatcherDto
                            {
                                Id = watcher.Data.Id?.ToString(),
                                Name = watcher.Data.Name,
                                Location = watcher.Data.Location?.Name,
                                ProvisioningState = watcher.Data.ProvisioningState?.ToString(),
                                Tags = watcher.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
                            });
                        }
                    }

                    break;
                }
            }

            return watchers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing network watchers");
            throw;
        }
    }

    public async Task<NetworkWatcherDto?> GetNetworkWatcherAsync(string subscriptionId, string resourceGroupName, string watcherName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            ResourceIdentifier? watcherId = NetworkWatcherResource.CreateResourceIdentifier(
                subscriptionId, resourceGroupName, watcherName);
            
            NetworkWatcherResource? watcher = armClient.GetNetworkWatcherResource(watcherId);
            Response<NetworkWatcherResource> response = await watcher.GetAsync();

            return new NetworkWatcherDto
            {
                Id = response.Value.Data.Id?.ToString(),
                Name = response.Value.Data.Name,
                Location = response.Value.Data.Location?.Name,
                ProvisioningState = response.Value.Data.ProvisioningState?.ToString(),
                Tags = response.Value.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Network watcher not found: {WatcherName} in resource group {ResourceGroupName}", 
                watcherName, resourceGroupName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network watcher {WatcherName}", watcherName);
            throw;
        }
    }

    public async Task<NetworkWatcherDto> CreateNetworkWatcherAsync(string subscriptionId, string resourceGroupName, NetworkWatcherCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var watcherData = new NetworkWatcherData
            {
                Location = new AzureLocation(request.Location)
            };

            // Add tags if provided
            if (request.Tags != null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                {
                    watcherData.Tags.Add(tag.Key, tag.Value);
                }
            }

            NetworkWatcherCollection? watcherCollection = resourceGroup.GetNetworkWatchers();
            ArmOperation<NetworkWatcherResource> operation = await watcherCollection.CreateOrUpdateAsync(
                WaitUntil.Completed, 
                request.Name, 
                watcherData);

            NetworkWatcherResource? watcher = operation.Value;

            logger.LogInformation("Created network watcher {WatcherName} in resource group {ResourceGroupName}", 
                request.Name, resourceGroupName);

            return new NetworkWatcherDto
            {
                Id = watcher.Data.Id?.ToString(),
                Name = watcher.Data.Name,
                Location = watcher.Data.Location?.Name,
                ProvisioningState = watcher.Data.ProvisioningState?.ToString(),
                Tags = watcher.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating network watcher {WatcherName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteNetworkWatcherAsync(string subscriptionId, string resourceGroupName, string watcherName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            ResourceIdentifier? watcherId = NetworkWatcherResource.CreateResourceIdentifier(
                subscriptionId, resourceGroupName, watcherName);
            
            NetworkWatcherResource? watcher = armClient.GetNetworkWatcherResource(watcherId);
            
            // Check if the watcher exists before attempting to delete
            try
            {
                await watcher.GetAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                logger.LogWarning("Network watcher not found: {WatcherName} in resource group {ResourceGroupName}", 
                    watcherName, resourceGroupName);
                return false;
            }

            // Delete the watcher
            ArmOperation? deleteOperation = await watcher.DeleteAsync(WaitUntil.Completed);

            logger.LogInformation("Deleted network watcher {WatcherName} from resource group {ResourceGroupName}", 
                watcherName, resourceGroupName);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting network watcher {WatcherName}", watcherName);
            throw;
        }
    }

    public async Task<ConnectivityCheckResult> CheckConnectivityAsync(string subscriptionId, string resourceGroupName, string watcherName, ConnectivityCheckRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            ResourceIdentifier? watcherId = NetworkWatcherResource.CreateResourceIdentifier(
                subscriptionId, resourceGroupName, watcherName);
            
            NetworkWatcherResource? watcher = armClient.GetNetworkWatcherResource(watcherId);

            // Build connectivity parameters
            var parameters = new ConnectivityContent(
                new ConnectivitySource(new ResourceIdentifier(request.SourceResourceId))
                {
                    Port = request.SourcePort != null ? int.Parse(request.SourcePort) : null
                },
                new ConnectivityDestination
                {
                    ResourceId = new ResourceIdentifier(request.DestinationResourceId),
                    Address = request.DestinationAddress,
                    Port = request.DestinationPort
                }
            )
            {
                Protocol = request.Protocol?.ToLowerInvariant() switch
                {
                    "tcp" => NetworkWatcherProtocol.Tcp,
                    "http" => NetworkWatcherProtocol.Http,
                    "https" => NetworkWatcherProtocol.Https,
                    "icmp" => NetworkWatcherProtocol.Icmp,
                    _ => NetworkWatcherProtocol.Tcp
                }
            };

            // Execute connectivity check
            ArmOperation<ConnectivityInformation> operation = 
                await watcher.CheckConnectivityAsync(WaitUntil.Completed, parameters);

            ConnectivityInformation? result = operation.Value;

            logger.LogInformation("Completed connectivity check from {Source} to {Destination}", 
                request.SourceResourceId, request.DestinationResourceId);

            // Map the result
            return new ConnectivityCheckResult
            {
                ConnectionStatus = result.NetworkConnectionStatus?.ToString(),
                AvgLatencyInMs = result.AvgLatencyInMs,
                MinLatencyInMs = result.MinLatencyInMs,
                MaxLatencyInMs = result.MaxLatencyInMs,
                ProbesSent = result.ProbesSent,
                ProbesFailed = result.ProbesFailed,
                Hops = result.Hops?.Select(hop => new HopDto
                {
                    Type = hop.ConnectivityHopType,
                    Id = hop.Id,
                    Address = hop.Address,
                    Issues = hop.Issues?.Select(i => i.ToString()).ToList() ?? []
                }).ToList() ?? []
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking connectivity from {Source} to {Destination}", 
                request.SourceResourceId, request.DestinationResourceId);
            throw;
        }
    }

    public async Task<NextHopResult> GetNextHopAsync(string subscriptionId, string resourceGroupName, string watcherName, NextHopRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            ResourceIdentifier? watcherId = NetworkWatcherResource.CreateResourceIdentifier(
                subscriptionId, resourceGroupName, watcherName);
            
            NetworkWatcherResource? watcher = armClient.GetNetworkWatcherResource(watcherId);

            // Build next hop parameters
            var parameters = new NextHopContent(
                new ResourceIdentifier(request.TargetVirtualMachineId),
                request.SourceIPAddress,
                request.DestinationIPAddress);

            // Execute next hop check
            ArmOperation<Azure.ResourceManager.Network.Models.NextHopResult> operation = 
                await watcher.GetNextHopAsync(WaitUntil.Completed, parameters);

            Azure.ResourceManager.Network.Models.NextHopResult? result = operation.Value;

            logger.LogInformation("Completed next hop check for VM {VmId} from {SourceIP} to {DestinationIP}", 
                request.TargetVirtualMachineId, request.SourceIPAddress, request.DestinationIPAddress);

            // Map the result
            return new NextHopResult
            {
                NextHopType = result.NextHopType.ToString(),
                NextHopIpAddress = result.NextHopIPAddress,
                RouteTableId = result.RouteTableId?.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next hop for VM {VmId}", request.TargetVirtualMachineId);
            throw;
        }
    }

    public Task<SecurityGroupViewResult> GetSecurityGroupViewAsync(string subscriptionId, string resourceGroupName, string watcherName, string targetVmId)
        => throw new NotImplementedException("Network Watcher operations coming soon");
}