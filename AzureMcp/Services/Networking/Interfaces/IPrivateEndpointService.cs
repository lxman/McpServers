using AzureMcp.Services.Networking.Models;

namespace AzureMcp.Services.Networking.Interfaces;

public interface IPrivateEndpointService
{
    Task<IEnumerable<PrivateEndpointDto>> ListPrivateEndpointsAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<PrivateEndpointDto?> GetPrivateEndpointAsync(string subscriptionId, string resourceGroupName, string privateEndpointName);
    Task<PrivateEndpointDto> CreatePrivateEndpointAsync(string subscriptionId, string resourceGroupName, PrivateEndpointCreateRequest request);
    Task<bool> DeletePrivateEndpointAsync(string subscriptionId, string resourceGroupName, string privateEndpointName);
}