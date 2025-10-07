using AzureMcp.Services.Networking.Models;

namespace AzureMcp.Services.Networking.Interfaces;

public interface IVirtualNetworkService
{
    Task<IEnumerable<VirtualNetworkDto>> ListVirtualNetworksAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<VirtualNetworkDto?> GetVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName);
    Task<VirtualNetworkDto> CreateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, VirtualNetworkCreateRequest request);
    Task<bool> DeleteVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName);
    Task<VirtualNetworkDto> UpdateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName, VirtualNetworkUpdateRequest request);
}