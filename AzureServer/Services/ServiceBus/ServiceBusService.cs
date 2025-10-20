using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;
using AzureServer.Services.Core;
using AzureServer.Services.ServiceBus.Models;

namespace AzureServer.Services.ServiceBus;

public class ServiceBusService(
    ArmClientFactory armClientFactory,
    ILogger<ServiceBusService> logger) : IServiceBusService
{
    private readonly Dictionary<string, ServiceBusAdministrationClient> _adminClients = new();
    private readonly Dictionary<string, ServiceBusClient> _dataClients = new();

    private async Task<ServiceBusAdministrationClient> GetAdministrationClientAsync(string namespaceName)
    {
        if (_adminClients.TryGetValue(namespaceName, out ServiceBusAdministrationClient? existingClient))
            return existingClient;

        var fullyQualifiedNamespace = $"{namespaceName}.servicebus.windows.net";
        var client = new ServiceBusAdministrationClient(fullyQualifiedNamespace, await armClientFactory.GetCredentialAsync());
        _adminClients[namespaceName] = client;

        return client;
    }

    private async Task<ServiceBusClient> GetDataClientAsync(string namespaceName)
    {
        if (_dataClients.TryGetValue(namespaceName, out ServiceBusClient? existingClient))
            return existingClient;

        var fullyQualifiedNamespace = $"{namespaceName}.servicebus.windows.net";
        var client = new ServiceBusClient(fullyQualifiedNamespace, await armClientFactory.GetCredentialAsync());
        _dataClients[namespaceName] = client;

        return client;
    }

    #region Namespace Management

    public async Task<IEnumerable<ServiceBusNamespaceDto>> ListNamespacesAsync(string? resourceGroupName = null, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var namespaces = new List<ServiceBusNamespaceDto>();

            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                // List namespaces in specific resource group
                SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                    ? await client.GetDefaultSubscriptionAsync()
                    : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

                ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                
                await foreach (ServiceBusNamespaceResource? ns in resourceGroup.GetServiceBusNamespaces())
                {
                    namespaces.Add(MapNamespace(ns));
                }
            }
            else
            {
                // List all namespaces across all resource groups
                SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                    ? await client.GetDefaultSubscriptionAsync()
                    : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

                await foreach (ServiceBusNamespaceResource? ns in subscription.GetServiceBusNamespacesAsync())
                {
                    namespaces.Add(MapNamespace(ns));
                }
            }

            logger.LogInformation("Retrieved {Count} Service Bus namespaces", namespaces.Count);
            return namespaces;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Service Bus namespaces");
            throw;
        }
    }

    public async Task<ServiceBusNamespaceDto?> GetNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            return MapNamespace(ns);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Service Bus namespace {NamespaceName} not found in resource group {ResourceGroup}", namespaceName, resourceGroupName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Service Bus namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task<ServiceBusNamespaceDto> CreateNamespaceAsync(string resourceGroupName, string namespaceName, string location, string? subscriptionId = null, string? sku = "Standard")
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            var data = new ServiceBusNamespaceData(new AzureLocation(location))
            {
                Sku = new ServiceBusSku(sku == "Premium" ? ServiceBusSkuName.Premium : ServiceBusSkuName.Standard)
            };

            ArmOperation<ServiceBusNamespaceResource> operation = await resourceGroup.GetServiceBusNamespaces()
                .CreateOrUpdateAsync(WaitUntil.Completed, namespaceName, data);

            logger.LogInformation("Created Service Bus namespace {NamespaceName} in resource group {ResourceGroup}", namespaceName, resourceGroupName);
            return MapNamespace(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Service Bus namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task DeleteNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            await ns.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted Service Bus namespace {NamespaceName} from resource group {ResourceGroup}", namespaceName, resourceGroupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Service Bus namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    #endregion

    #region Queue Management

    public async Task<IEnumerable<ServiceBusQueueDto>> ListQueuesAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            var queues = new List<ServiceBusQueueDto>();
            await foreach (ServiceBusQueueResource? queue in ns.GetServiceBusQueues())
            {
                queues.Add(MapQueue(queue));
            }

            logger.LogInformation("Retrieved {Count} queues from namespace {NamespaceName}", queues.Count, namespaceName);
            return queues;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing queues in namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task<ServiceBusQueueDto?> GetQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusQueueResource queue = await ns.GetServiceBusQueueAsync(queueName);

            return MapQueue(queue);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Queue {QueueName} not found in namespace {NamespaceName}", queueName, namespaceName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<ServiceBusQueueDto> CreateQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null, int? maxDeliveryCount = null, TimeSpan? lockDuration = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            var data = new ServiceBusQueueData();
            if (maxDeliveryCount.HasValue)
                data.MaxDeliveryCount = maxDeliveryCount.Value;
            if (lockDuration.HasValue)
                data.LockDuration = lockDuration.Value;

            ArmOperation<ServiceBusQueueResource> operation = await ns.GetServiceBusQueues()
                .CreateOrUpdateAsync(WaitUntil.Completed, queueName, data);

            logger.LogInformation("Created queue {QueueName} in namespace {NamespaceName}", queueName, namespaceName);
            return MapQueue(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task DeleteQueueAsync(string resourceGroupName, string namespaceName, string queueName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusQueueResource queue = await ns.GetServiceBusQueueAsync(queueName);

            await queue.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted queue {QueueName} from namespace {NamespaceName}", queueName, namespaceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting queue {QueueName}", queueName);
            throw;
        }
    }

    #endregion

    #region Topic Management

    public async Task<IEnumerable<ServiceBusTopicDto>> ListTopicsAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            var topics = new List<ServiceBusTopicDto>();
            await foreach (ServiceBusTopicResource? topic in ns.GetServiceBusTopics())
            {
                topics.Add(MapTopic(topic));
            }

            logger.LogInformation("Retrieved {Count} topics from namespace {NamespaceName}", topics.Count, namespaceName);
            return topics;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing topics in namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task<ServiceBusTopicDto?> GetTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);

            return MapTopic(topic);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Topic {TopicName} not found in namespace {NamespaceName}", topicName, namespaceName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting topic {TopicName}", topicName);
            throw;
        }
    }

    public async Task<ServiceBusTopicDto> CreateTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);

            var data = new ServiceBusTopicData();
            ArmOperation<ServiceBusTopicResource> operation = await ns.GetServiceBusTopics()
                .CreateOrUpdateAsync(WaitUntil.Completed, topicName, data);

            logger.LogInformation("Created topic {TopicName} in namespace {NamespaceName}", topicName, namespaceName);
            return MapTopic(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating topic {TopicName}", topicName);
            throw;
        }
    }

    public async Task DeleteTopicAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);

            await topic.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted topic {TopicName} from namespace {NamespaceName}", topicName, namespaceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting topic {TopicName}", topicName);
            throw;
        }
    }

    #endregion

    #region Subscription Management

    public async Task<IEnumerable<ServiceBusSubscriptionDto>> ListSubscriptionsAsync(string resourceGroupName, string namespaceName, string topicName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);

            var subscriptions = new List<ServiceBusSubscriptionDto>();
            await foreach (ServiceBusSubscriptionResource? sub in topic.GetServiceBusSubscriptions())
            {
                subscriptions.Add(MapSubscription(sub));
            }

            logger.LogInformation("Retrieved {Count} subscriptions from topic {TopicName}", subscriptions.Count, topicName);
            return subscriptions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subscriptions in topic {TopicName}", topicName);
            throw;
        }
    }

    public async Task<ServiceBusSubscriptionDto?> GetSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);
            ServiceBusSubscriptionResource sub = await topic.GetServiceBusSubscriptionAsync(subscriptionName);

            return MapSubscription(sub);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Subscription {SubscriptionName} not found in topic {TopicName}", subscriptionName, topicName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subscription {SubscriptionName}", subscriptionName);
            throw;
        }
    }

    public async Task<ServiceBusSubscriptionDto> CreateSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null, int? maxDeliveryCount = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);

            var data = new ServiceBusSubscriptionData();
            if (maxDeliveryCount.HasValue)
                data.MaxDeliveryCount = maxDeliveryCount.Value;

            ArmOperation<ServiceBusSubscriptionResource> operation = await topic.GetServiceBusSubscriptions()
                .CreateOrUpdateAsync(WaitUntil.Completed, subscriptionName, data);

            logger.LogInformation("Created subscription {SubscriptionName} in topic {TopicName}", subscriptionName, topicName);
            return MapSubscription(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating subscription {SubscriptionName}", subscriptionName);
            throw;
        }
    }

    public async Task DeleteSubscriptionAsync(string resourceGroupName, string namespaceName, string topicName, string subscriptionName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            ServiceBusNamespaceResource ns = await resourceGroup.GetServiceBusNamespaceAsync(namespaceName);
            ServiceBusTopicResource topic = await ns.GetServiceBusTopicAsync(topicName);
            ServiceBusSubscriptionResource sub = await topic.GetServiceBusSubscriptionAsync(subscriptionName);

            await sub.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted subscription {SubscriptionName} from topic {TopicName}", subscriptionName, topicName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting subscription {SubscriptionName}", subscriptionName);
            throw;
        }
    }

    #endregion

    #region Message Operations

    public async Task<string> SendMessageAsync(string namespaceName, string queueOrTopicName, string messageBody, Dictionary<string, object>? properties = null)
    {
        try
        {
            ServiceBusClient client = await GetDataClientAsync(namespaceName);
            ServiceBusSender? sender = client.CreateSender(queueOrTopicName);

            var message = new ServiceBusMessage(messageBody);
            
            if (properties != null)
            {
                foreach (KeyValuePair<string, object> kvp in properties)
                {
                    message.ApplicationProperties.Add(kvp.Key, kvp.Value);
                }
            }

            await sender.SendMessageAsync(message);
            
            string? messageId = message.MessageId;
            logger.LogInformation("Sent message {MessageId} to {QueueOrTopic} in namespace {NamespaceName}", 
                messageId, queueOrTopicName, namespaceName);

            return messageId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {QueueOrTopic}", queueOrTopicName);
            throw;
        }
    }

    public async Task<IEnumerable<ServiceBusMessageDto>> ReceiveMessagesAsync(string namespaceName, string queueName, int maxMessages = 10, int maxWaitTimeSeconds = 5)
    {
        try
        {
            ServiceBusClient client = await GetDataClientAsync(namespaceName);
            ServiceBusReceiver? receiver = client.CreateReceiver(queueName);

            var messages = new List<ServiceBusMessageDto>();
            IReadOnlyList<ServiceBusReceivedMessage> receivedMessages = await receiver.ReceiveMessagesAsync(
                maxMessages, 
                TimeSpan.FromSeconds(maxWaitTimeSeconds));

            foreach (ServiceBusReceivedMessage message in receivedMessages)
            {
                messages.Add(MapReceivedMessage(message));
                await receiver.CompleteMessageAsync(message);
            }

            logger.LogInformation("Received and completed {Count} messages from queue {QueueName}", messages.Count, queueName);
            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving messages from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<IEnumerable<ServiceBusMessageDto>> PeekMessagesAsync(string namespaceName, string queueName, int maxMessages = 10)
    {
        try
        {
            ServiceBusClient client = await GetDataClientAsync(namespaceName);
            ServiceBusReceiver? receiver = client.CreateReceiver(queueName);

            var messages = new List<ServiceBusMessageDto>();
            IReadOnlyList<ServiceBusReceivedMessage> peekedMessages = await receiver.PeekMessagesAsync(maxMessages);

            foreach (ServiceBusReceivedMessage message in peekedMessages)
            {
                messages.Add(MapReceivedMessage(message));
            }

            logger.LogInformation("Peeked {Count} messages from queue {QueueName}", messages.Count, queueName);
            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error peeking messages from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<long> GetQueueMessageCountAsync(string namespaceName, string queueName)
    {
        try
        {
            ServiceBusAdministrationClient adminClient = await GetAdministrationClientAsync(namespaceName);
            QueueRuntimeProperties properties = await adminClient.GetQueueRuntimePropertiesAsync(queueName);

            logger.LogInformation("Queue {QueueName} has {Count} active messages", queueName, properties.ActiveMessageCount);
            return properties.ActiveMessageCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message count for queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<string> PurgeQueueAsync(string namespaceName, string queueName)
    {
        try
        {
            ServiceBusClient client = await GetDataClientAsync(namespaceName);
            ServiceBusReceiver? receiver = client.CreateReceiver(queueName);

            var purgedCount = 0;
            var hasMore = true;

            while (hasMore)
            {
                IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(1));
                
                if (messages.Count == 0)
                {
                    hasMore = false;
                }
                else
                {
                    foreach (ServiceBusReceivedMessage message in messages)
                    {
                        await receiver.CompleteMessageAsync(message);
                        purgedCount++;
                    }
                }
            }

            logger.LogInformation("Purged {Count} messages from queue {QueueName}", purgedCount, queueName);
            return $"Purged {purgedCount} messages from queue {queueName}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging queue {QueueName}", queueName);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static ServiceBusNamespaceDto MapNamespace(ServiceBusNamespaceResource ns)
    {
        return new ServiceBusNamespaceDto
        {
            Name = ns.Data.Name,
            Id = ns.Data.Id.ToString(),
            Location = ns.Data.Location.ToString(),
            ResourceGroup = ns.Id.ResourceGroupName ?? string.Empty,
            SubscriptionId = ns.Id.SubscriptionId ?? string.Empty,
            Sku = ns.Data.Sku?.Name.ToString(),
            Status = ns.Data.Status,
            Endpoint = ns.Data.ServiceBusEndpoint,
            Tags = ns.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CreatedAt = ns.Data.CreatedOn?.DateTime,
            UpdatedAt = ns.Data.UpdatedOn?.DateTime
        };
    }

    private static ServiceBusQueueDto MapQueue(ServiceBusQueueResource queue)
    {
        return new ServiceBusQueueDto
        {
            Name = queue.Data.Name,
            Id = queue.Data.Id.ToString(),
            MessageCount = queue.Data.MessageCount,
            SizeInBytes = queue.Data.SizeInBytes,
            Status = queue.Data.Status?.ToString(),
            MaxDeliveryCount = queue.Data.MaxDeliveryCount,
            LockDuration = queue.Data.LockDuration,
            DefaultMessageTimeToLive = queue.Data.DefaultMessageTimeToLive,
            RequiresDuplicateDetection = queue.Data.RequiresDuplicateDetection,
            RequiresSession = queue.Data.RequiresSession,
            DeadLetteringOnMessageExpiration = queue.Data.DeadLetteringOnMessageExpiration,
            CreatedAt = queue.Data.CreatedOn?.DateTime,
            UpdatedAt = queue.Data.UpdatedOn?.DateTime,
            AccessedAt = queue.Data.AccessedOn?.DateTime
        };
    }

    private static ServiceBusTopicDto MapTopic(ServiceBusTopicResource topic)
    {
        return new ServiceBusTopicDto
        {
            Name = topic.Data.Name,
            Id = topic.Data.Id.ToString(),
            SizeInBytes = topic.Data.SizeInBytes,
            Status = topic.Data.Status?.ToString(),
            SubscriptionCount = topic.Data.SubscriptionCount,
            DefaultMessageTimeToLive = topic.Data.DefaultMessageTimeToLive,
            RequiresDuplicateDetection = topic.Data.RequiresDuplicateDetection,
            CreatedAt = topic.Data.CreatedOn?.DateTime,
            UpdatedAt = topic.Data.UpdatedOn?.DateTime,
            AccessedAt = topic.Data.AccessedOn?.DateTime
        };
    }

    private static ServiceBusSubscriptionDto MapSubscription(ServiceBusSubscriptionResource subscription)
    {
        return new ServiceBusSubscriptionDto
        {
            Name = subscription.Data.Name,
            Id = subscription.Data.Id.ToString(),
            MessageCount = subscription.Data.MessageCount,
            Status = subscription.Data.Status?.ToString(),
            MaxDeliveryCount = subscription.Data.MaxDeliveryCount,
            LockDuration = subscription.Data.LockDuration,
            DefaultMessageTimeToLive = subscription.Data.DefaultMessageTimeToLive,
            RequiresSession = subscription.Data.RequiresSession,
            DeadLetteringOnMessageExpiration = subscription.Data.DeadLetteringOnMessageExpiration,
            CreatedAt = subscription.Data.CreatedOn?.DateTime,
            UpdatedAt = subscription.Data.UpdatedOn?.DateTime,
            AccessedAt = subscription.Data.AccessedOn?.DateTime
        };
    }

    private static ServiceBusMessageDto MapReceivedMessage(ServiceBusReceivedMessage message)
    {
        return new ServiceBusMessageDto
        {
            MessageId = message.MessageId,
            Body = message.Body.ToString(),
            ContentType = message.ContentType,
            Properties = message.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            DeliveryCount = message.DeliveryCount,
            EnqueuedTime = message.EnqueuedTime.DateTime,
            ExpiresAt = message.ExpiresAt.DateTime,
            LockToken = message.LockToken,
            SequenceNumber = message.SequenceNumber
        };
    }

    #endregion
}
