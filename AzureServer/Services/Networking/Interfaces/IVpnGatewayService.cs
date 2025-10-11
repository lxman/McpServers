using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking.Interfaces;

public interface IVpnGatewayService
{
    Task<IEnumerable<VpnGatewayDto>> ListVpnGatewaysAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<VpnGatewayDto?> GetVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
    Task<VpnGatewayDto> CreateVpnGatewayAsync(string subscriptionId, string resourceGroupName, VpnGatewayCreateRequest request);
    Task<bool> DeleteVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
}