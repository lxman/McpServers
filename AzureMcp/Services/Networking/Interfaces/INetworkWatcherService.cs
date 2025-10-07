using AzureMcp.Services.Networking.Models;

namespace AzureMcp.Services.Networking.Interfaces;

public interface INetworkWatcherService
{
    Task<IEnumerable<NetworkWatcherDto>> ListNetworkWatchersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<NetworkWatcherDto?> GetNetworkWatcherAsync(string subscriptionId, string resourceGroupName, string watcherName);
    Task<NetworkWatcherDto> CreateNetworkWatcherAsync(string subscriptionId, string resourceGroupName, NetworkWatcherCreateRequest request);
    Task<bool> DeleteNetworkWatcherAsync(string subscriptionId, string resourceGroupName, string watcherName);
    Task<ConnectivityCheckResult> CheckConnectivityAsync(string subscriptionId, string resourceGroupName, string watcherName, ConnectivityCheckRequest request);
    Task<NextHopResult> GetNextHopAsync(string subscriptionId, string resourceGroupName, string watcherName, NextHopRequest request);
    Task<SecurityGroupViewResult> GetSecurityGroupViewAsync(string subscriptionId, string resourceGroupName, string watcherName, string targetVmId);
}