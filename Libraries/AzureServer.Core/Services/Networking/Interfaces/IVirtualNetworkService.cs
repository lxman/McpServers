using AzureServer.Core.Services.Networking.Models;

namespace AzureServer.Core.Services.Networking.Interfaces;

public interface IVirtualNetworkService
{
    Task<IEnumerable<VirtualNetworkDto>> ListVirtualNetworksAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<VirtualNetworkDto?> GetVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName);
    Task<VirtualNetworkDto> CreateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, VirtualNetworkCreateRequest request);
    Task<bool> DeleteVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName);
    Task<VirtualNetworkDto> UpdateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName, VirtualNetworkUpdateRequest request);
}