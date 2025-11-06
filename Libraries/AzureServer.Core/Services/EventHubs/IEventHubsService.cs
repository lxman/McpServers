using AzureServer.Core.Services.EventHubs.Models;

namespace AzureServer.Core.Services.EventHubs;

public interface IEventHubsService
{
    // Namespace Management (ResourceManager)
    Task<IEnumerable<EventHubsNamespaceDto>> ListNamespacesAsync(string? resourceGroupName = null, string? subscriptionId = null);
    Task<EventHubsNamespaceDto?> GetNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    Task<EventHubsNamespaceDto> CreateNamespaceAsync(string resourceGroupName, string namespaceName, string location, string? subscriptionId = null, string? sku = "Standard");
    Task DeleteNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    
    // Event Hub Management (ResourceManager)
    Task<IEnumerable<EventHubDto>> ListEventHubsAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    Task<EventHubDto?> GetEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null);
    Task<EventHubDto> CreateEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null, int? partitionCount = null, int? messageRetentionInDays = null);
    Task DeleteEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null);
    
    // Consumer Group Management (ResourceManager)
    Task<IEnumerable<ConsumerGroupDto>> ListConsumerGroupsAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null);
    Task<ConsumerGroupDto?> GetConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null);
    Task<ConsumerGroupDto> CreateConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null);
    Task DeleteConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null);
    
    // Event Operations (Data Plane)
    Task<string> SendEventAsync(string namespaceName, string eventHubName, string eventBody, Dictionary<string, object>? properties = null);
    Task<IEnumerable<string>> SendEventBatchAsync(string namespaceName, string eventHubName, IEnumerable<string> eventBodies);
    Task<IEnumerable<EventDataDto>> ReceiveEventsAsync(string namespaceName, string eventHubName, string consumerGroup = "$Default", int maxEvents = 100, int maxWaitTimeSeconds = 10);
    Task<EventHubPropertiesDto> GetEventHubPropertiesAsync(string namespaceName, string eventHubName);
    Task<PartitionPropertiesDto> GetPartitionPropertiesAsync(string namespaceName, string eventHubName, string partitionId);
}
