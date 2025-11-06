using AzureServer.Core.Services.Networking.Models;

namespace AzureServer.Core.Services.Networking.Interfaces;

public interface IVpnGatewayService
{
    Task<IEnumerable<VpnGatewayDto>> ListVpnGatewaysAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<VpnGatewayDto?> GetVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
    Task<VpnGatewayDto> CreateVpnGatewayAsync(string subscriptionId, string resourceGroupName, VpnGatewayCreateRequest request);
    Task<bool> DeleteVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
}