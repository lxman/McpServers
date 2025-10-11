using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking.Interfaces;

public interface IApplicationGatewayService
{
    Task<IEnumerable<ApplicationGatewayDto>> ListApplicationGatewaysAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<ApplicationGatewayDto?> GetApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
    Task<ApplicationGatewayDto> CreateApplicationGatewayAsync(string subscriptionId, string resourceGroupName, ApplicationGatewayCreateRequest request);
    Task<bool> DeleteApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
    Task<ApplicationGatewayDto> StartApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
    Task<ApplicationGatewayDto> StopApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName);
}