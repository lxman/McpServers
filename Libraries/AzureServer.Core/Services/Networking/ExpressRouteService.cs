using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Networking;

public class ExpressRouteService(ArmClientFactory armClientFactory, ILogger<ExpressRouteService> logger) : IExpressRouteService
{
    public async Task<IEnumerable<ExpressRouteCircuitDto>> ListExpressRouteCircuitsAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await armClient.GetDefaultSubscriptionAsync()
                : armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var circuits = new List<ExpressRouteCircuitDto>();

            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                // List circuits in a specific resource group
                Response<ResourceGroupResource>? resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                await foreach (ExpressRouteCircuitResource? circuit in resourceGroup.Value.GetExpressRouteCircuits().GetAllAsync())
                {
                    circuits.Add(MappingService.MapToExpressRouteCircuitDto(circuit.Data));
                }
            }
            else
            {
                // List all circuits in the subscription
                await foreach (ExpressRouteCircuitResource? circuit in subscription.GetExpressRouteCircuitsAsync())
                {
                    circuits.Add(MappingService.MapToExpressRouteCircuitDto(circuit.Data));
                }
            }

            logger.LogInformation("Listed {Count} ExpressRoute circuits", circuits.Count);
            return circuits;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ExpressRoute circuits");
            throw;
        }
    }

    public async Task<ExpressRouteCircuitDto?> GetExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, string circuitName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            Response<ResourceGroupResource>? resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            
            Response<ExpressRouteCircuitResource>? circuit = await resourceGroup.Value.GetExpressRouteCircuits().GetAsync(circuitName);
            
            logger.LogInformation("Retrieved ExpressRoute circuit: {CircuitName}", circuitName);
            return MappingService.MapToExpressRouteCircuitDto(circuit.Value.Data);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("ExpressRoute circuit not found: {CircuitName}", circuitName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ExpressRoute circuit: {CircuitName}", circuitName);
            throw;
        }
    }

    public async Task<ExpressRouteCircuitDto> CreateExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, ExpressRouteCircuitCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            Response<ResourceGroupResource>? resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            var circuitData = new ExpressRouteCircuitData
            {
                Location = new AzureLocation(request.Location),
                Sku = new ExpressRouteCircuitSku
                {
                    Name = $"{request.SkuTier}_{request.SkuFamily}",
                    Tier = Enum.Parse<ExpressRouteCircuitSkuTier>(request.SkuTier),
                    Family = Enum.Parse<ExpressRouteCircuitSkuFamily>(request.SkuFamily)
                },
                ServiceProviderProperties = new ExpressRouteCircuitServiceProviderProperties
                {
                    ServiceProviderName = request.ServiceProviderName,
                    PeeringLocation = request.PeeringLocation,
                    BandwidthInMbps = request.BandwidthInMbps
                },
                AllowClassicOperations = request.AllowClassicOperations
            };

            if (request.Tags != null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                {
                    circuitData.Tags.Add(tag.Key, tag.Value);
                }
            }

            ArmOperation<ExpressRouteCircuitResource>? operation = await resourceGroup.Value.GetExpressRouteCircuits()
                .CreateOrUpdateAsync(WaitUntil.Completed, request.Name, circuitData);

            logger.LogInformation("Created ExpressRoute circuit: {CircuitName}", request.Name);
            return MappingService.MapToExpressRouteCircuitDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating ExpressRoute circuit: {CircuitName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteExpressRouteCircuitAsync(string subscriptionId, string resourceGroupName, string circuitName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            Response<ResourceGroupResource>? resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            Response<ExpressRouteCircuitResource>? circuit = await resourceGroup.Value.GetExpressRouteCircuits().GetAsync(circuitName);
            await circuit.Value.DeleteAsync(WaitUntil.Completed);

            logger.LogInformation("Deleted ExpressRoute circuit: {CircuitName}", circuitName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("ExpressRoute circuit not found for deletion: {CircuitName}", circuitName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting ExpressRoute circuit: {CircuitName}", circuitName);
            throw;
        }
    }
}
