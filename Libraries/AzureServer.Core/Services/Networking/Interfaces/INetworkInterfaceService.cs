using AzureServer.Core.Services.Networking.Models;

namespace AzureServer.Core.Services.Networking.Interfaces;

public interface INetworkInterfaceService
{
    Task<IEnumerable<NetworkInterfaceDto>> ListNetworkInterfacesAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<NetworkInterfaceDto?> GetNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, string nicName);
    Task<NetworkInterfaceDto> CreateNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, NetworkInterfaceCreateRequest request);
    Task<bool> DeleteNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, string nicName);
}