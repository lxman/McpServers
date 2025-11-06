using AzureServer.Core.Services.Networking.Models;

namespace AzureServer.Core.Services.Networking.Interfaces;

public interface INetworkSecurityGroupService
{
    Task<IEnumerable<NetworkSecurityGroupDto>> ListNetworkSecurityGroupsAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<NetworkSecurityGroupDto?> GetNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName);
    Task<NetworkSecurityGroupDto> CreateNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, NetworkSecurityGroupCreateRequest request);
    Task<bool> DeleteNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName);
    Task<NetworkSecurityGroupDto> UpdateNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName, NetworkSecurityGroupUpdateRequest request);
}