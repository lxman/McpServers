using AzureServer.Core.Services.Networking.Models;

namespace AzureServer.Core.Services.Networking.Interfaces;

public interface ISubnetService
{
    Task<IEnumerable<SubnetDto>> ListSubnetsAsync(string subscriptionId, string resourceGroupName, string vnetName);
    Task<SubnetDto?> GetSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName);
    Task<SubnetDto> CreateSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, SubnetCreateRequest request);
    Task<bool> DeleteSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName);
    Task<SubnetDto> UpdateSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName, SubnetUpdateRequest request);
}