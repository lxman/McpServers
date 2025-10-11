using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking.Interfaces;

public interface ISecurityRuleService
{
    Task<IEnumerable<SecurityRuleDto>> ListSecurityRulesAsync(string subscriptionId, string resourceGroupName, string nsgName);
    Task<SecurityRuleDto?> GetSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName);
    Task<SecurityRuleDto> CreateSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, SecurityRuleCreateRequest request);
    Task<bool> DeleteSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName);
    Task<SecurityRuleDto> UpdateSecurityRuleAsync(string subscriptionId, string resourceGroupName, string nsgName, string ruleName, SecurityRuleUpdateRequest request);
}