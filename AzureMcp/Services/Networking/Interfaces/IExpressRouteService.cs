using AzureMcp.Services.Networking.Models;

namespace AzureMcp.Services.Networking.Interfaces;

public interface IExpressRouteService
{
    Task<IEnumerable<ExpressRouteCircuitDto>> ListExpressRouteCircuitsAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<ExpressRouteCircuitDto?> GetExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, string circuitName);
    Task<ExpressRouteCircuitDto> CreateExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, ExpressRouteCircuitCreateRequest request);
    Task<bool> DeleteExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, string circuitName);
}