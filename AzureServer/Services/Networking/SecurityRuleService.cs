using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using AzureServer.Services.Core;
using AzureServer.Services.Networking.Interfaces;
using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking;

public class SecurityRuleService(ArmClientFactory armClientFactory, ILogger<SecurityRuleService> logger) : ISecurityRuleService
{
    public async Task<IEnumerable<SecurityRuleDto>> ListSecurityRulesAsync(string subscriptionId, string resourceGroupName, string nsgName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = NetworkSecurityGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName);
            NetworkSecurityGroupResource? nsg = armClient.GetNetworkSecurityGroupResource(resourceId);
            
            var rules = new List<SecurityRuleDto>();
            await foreach (SecurityRuleResource? rule in nsg.GetSecurityRules())
            {
                rules.Add(MappingService.MapToSecurityRuleDto(rule.Data));
            }
            
            return rules;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing security rules for NSG {NsgName}", nsgName);
            throw;
        }
    }

    public async Task<SecurityRuleDto?> GetSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = SecurityRuleResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName, ruleName);
            Response<SecurityRuleResource>? response = await armClient.GetSecurityRuleResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToSecurityRuleDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting security rule {RuleName}", ruleName);
            throw;
        }
    }

    public async Task<SecurityRuleDto> CreateSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, SecurityRuleCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? nsgResourceId = NetworkSecurityGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName);
            NetworkSecurityGroupResource? nsg = armClient.GetNetworkSecurityGroupResource(nsgResourceId);

            SecurityRuleData ruleData = MappingService.MapToSecurityRuleData(request);

            ArmOperation<SecurityRuleResource>? operation = await nsg.GetSecurityRules().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, ruleData);
            
            return MappingService.MapToSecurityRuleDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating security rule {RuleName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = SecurityRuleResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName, ruleName);
            SecurityRuleResource? rule = armClient.GetSecurityRuleResource(resourceId);
            
            await rule.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting security rule {RuleName}", ruleName);
            throw;
        }
    }

    public async Task<SecurityRuleDto> UpdateSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName, SecurityRuleUpdateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = SecurityRuleResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName, ruleName);
            SecurityRuleResource? rule = armClient.GetSecurityRuleResource(resourceId);
            
            Response<SecurityRuleResource>? response = await rule.GetAsync();
            SecurityRuleData? ruleData = response.Value.Data;

            if (!string.IsNullOrEmpty(request.Description))
                ruleData.Description = request.Description;

            if (!string.IsNullOrEmpty(request.Protocol))
                ruleData.Protocol = request.Protocol == "*" ? SecurityRuleProtocol.Asterisk : Enum.Parse<SecurityRuleProtocol>(request.Protocol);

            if (!string.IsNullOrEmpty(request.SourcePortRange))
                ruleData.SourcePortRange = request.SourcePortRange;

            if (!string.IsNullOrEmpty(request.DestinationPortRange))
                ruleData.DestinationPortRange = request.DestinationPortRange;

            if (!string.IsNullOrEmpty(request.SourceAddressPrefix))
                ruleData.SourceAddressPrefix = request.SourceAddressPrefix;

            if (!string.IsNullOrEmpty(request.DestinationAddressPrefix))
                ruleData.DestinationAddressPrefix = request.DestinationAddressPrefix;

            if (!string.IsNullOrEmpty(request.Access))
                ruleData.Access = Enum.Parse<SecurityRuleAccess>(request.Access);

            if (request.Priority.HasValue)
                ruleData.Priority = request.Priority.Value;

            if (!string.IsNullOrEmpty(request.Direction))
                ruleData.Direction = Enum.Parse<SecurityRuleDirection>(request.Direction);

            ArmOperation<SecurityRuleResource>? operation = await rule.UpdateAsync(WaitUntil.Completed, ruleData);
            return MappingService.MapToSecurityRuleDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating security rule {RuleName}", ruleName);
            throw;
        }
    }
}