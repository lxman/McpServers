using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking.Interfaces;

public interface ILoadBalancerService
{
    Task<IEnumerable<LoadBalancerDto>> ListLoadBalancersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<LoadBalancerDto?> GetLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName);
    Task<LoadBalancerDto> CreateLoadBalancerAsync(string subscriptionId, string resourceGroupName, LoadBalancerCreateRequest request);
    Task<bool> DeleteLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName);
    Task<LoadBalancerDto> UpdateLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName, LoadBalancerUpdateRequest request);
}