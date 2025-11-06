using AzureServer.Core.Services.ServiceBus.Models;

namespace AzureServer.Core.Services.ServiceBus;

public interface IServiceBusService
{
    // Namespace Management (ResourceManager)
    Task<IEnumerable<ServiceBusNamespaceDto>> ListNamespacesAsync(string? resourceGroupName = null, string? subscriptionId = null);
    Task<ServiceBusNamespaceDto?> GetNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    Task<ServiceBusNamespaceDto> CreateNamespaceAsync(string resourceGroupName, string namespaceName, string location, string? subscriptionId = null, string? sku = "Standard");
    Task DeleteNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    
    // Queue Management (ResourceManager)
    Task<IEnumerable<ServiceBusQueueDto>> ListQueuesAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    Task<ServiceBusQueueDto?> GetQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null);
    Task<ServiceBusQueueDto> CreateQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null, int? maxDeliveryCount = null, TimeSpan? lockDuration = null);
    Task DeleteQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null);
    
    // Topic Management (ResourceManager)
    Task<IEnumerable<ServiceBusTopicDto>> ListTopicsAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null);
    Task<ServiceBusTopicDto?> GetTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null);
    Task<ServiceBusTopicDto> CreateTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null);
    Task DeleteTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null);
    
    // Subscription Management (ResourceManager)
    Task<IEnumerable<ServiceBusSubscriptionDto>> ListSubscriptionsAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null);
    Task<ServiceBusSubscriptionDto?> GetSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null);
    Task<ServiceBusSubscriptionDto> CreateSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null, int? maxDeliveryCount = null);
    Task DeleteSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null);
    
    // Message Operations (Data Plane)
    Task<string> SendMessageAsync(string namespaceName, string queueOrTopicName, string messageBody, Dictionary<string, object>? properties = null);
    Task<IEnumerable<ServiceBusMessageDto>> ReceiveMessagesAsync(string namespaceName, string queueName, int maxMessages = 10, int maxWaitTimeSeconds = 5);
    Task<IEnumerable<ServiceBusMessageDto>> PeekMessagesAsync(string namespaceName, string queueName, int maxMessages = 10);
    Task<long> GetQueueMessageCountAsync(string namespaceName, string queueName);
    Task<string> PurgeQueueAsync(string namespaceName, string queueName);
}
