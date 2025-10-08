using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.ServiceBus;
using AzureMcp.Services.ServiceBus.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class ServiceBusTools(IServiceBusService serviceBusService)
{
    #region Namespace Tools

    [McpServerTool]
    [Description("List Service Bus namespaces")]
    public async Task<string> ListServiceBusNamespacesAsync(
        [Description("Optional resource group name")] string? resourceGroupName = null,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusNamespaceDto> namespaces = await serviceBusService.ListNamespacesAsync(resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, namespaces = namespaces.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListServiceBusNamespaces");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Service Bus namespace")]
    public async Task<string> GetServiceBusNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServiceBusNamespaceDto? ns = await serviceBusService.GetNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
            if (ns is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Namespace {namespaceName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, @namespace = ns },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServiceBusNamespace");
        }
    }

    [McpServerTool]
    [Description("Create a new Service Bus namespace")]
    public async Task<string> CreateServiceBusNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Azure location (e.g., 'eastus', 'westus2')")] string location,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional SKU tier (Standard or Premium, default: Standard)")] string? sku = "Standard")
    {
        try
        {
            ServiceBusNamespaceDto ns = await serviceBusService.CreateNamespaceAsync(resourceGroupName, namespaceName, location, subscriptionId, sku);
            return JsonSerializer.Serialize(new { success = true, @namespace = ns },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateServiceBusNamespace");
        }
    }

    [McpServerTool]
    [Description("Delete a Service Bus namespace")]
    public async Task<string> DeleteServiceBusNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await serviceBusService.DeleteNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Namespace {namespaceName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteServiceBusNamespace");
        }
    }

    #endregion

    #region Queue Tools

    [McpServerTool]
    [Description("List queues in a Service Bus namespace")]
    public async Task<string> ListServiceBusQueuesAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusQueueDto> queues = await serviceBusService.ListQueuesAsync(resourceGroupName, namespaceName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, queues = queues.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListServiceBusQueues");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Service Bus queue")]
    public async Task<string> GetServiceBusQueueAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServiceBusQueueDto? queue = await serviceBusService.GetQueueAsync(resourceGroupName, namespaceName, queueName, subscriptionId);
            if (queue is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Queue {queueName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, queue },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServiceBusQueue");
        }
    }

    [McpServerTool]
    [Description("Create a new Service Bus queue")]
    public async Task<string> CreateServiceBusQueueAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional max delivery count (default: 10)")] int? maxDeliveryCount = null,
        [Description("Optional lock duration in seconds (default: 60)")] int? lockDurationSeconds = null)
    {
        try
        {
            TimeSpan? lockDuration = lockDurationSeconds.HasValue ? TimeSpan.FromSeconds(lockDurationSeconds.Value) : null;
            ServiceBusQueueDto queue = await serviceBusService.CreateQueueAsync(resourceGroupName, namespaceName, queueName, subscriptionId, maxDeliveryCount, lockDuration);
            return JsonSerializer.Serialize(new { success = true, queue },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateServiceBusQueue");
        }
    }

    [McpServerTool]
    [Description("Delete a Service Bus queue")]
    public async Task<string> DeleteServiceBusQueueAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await serviceBusService.DeleteQueueAsync(resourceGroupName, namespaceName, queueName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Queue {queueName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteServiceBusQueue");
        }
    }

    #endregion

    #region Topic Tools

    [McpServerTool]
    [Description("List topics in a Service Bus namespace")]
    public async Task<string> ListServiceBusTopicsAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusTopicDto> topics = await serviceBusService.ListTopicsAsync(resourceGroupName, namespaceName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, topics = topics.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListServiceBusTopics");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Service Bus topic")]
    public async Task<string> GetServiceBusTopicAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServiceBusTopicDto? topic = await serviceBusService.GetTopicAsync(resourceGroupName, namespaceName, topicName, subscriptionId);
            if (topic is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Topic {topicName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, topic },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServiceBusTopic");
        }
    }

    [McpServerTool]
    [Description("Create a new Service Bus topic")]
    public async Task<string> CreateServiceBusTopicAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServiceBusTopicDto topic = await serviceBusService.CreateTopicAsync(resourceGroupName, namespaceName, topicName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, topic },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateServiceBusTopic");
        }
    }

    [McpServerTool]
    [Description("Delete a Service Bus topic")]
    public async Task<string> DeleteServiceBusTopicAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await serviceBusService.DeleteTopicAsync(resourceGroupName, namespaceName, topicName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Topic {topicName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteServiceBusTopic");
        }
    }

    #endregion

    #region Subscription Tools

    [McpServerTool]
    [Description("List subscriptions for a Service Bus topic")]
    public async Task<string> ListServiceBusSubscriptionsAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ServiceBusSubscriptionDto> subscriptions = await serviceBusService.ListSubscriptionsAsync(resourceGroupName, namespaceName, topicName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, subscriptions = subscriptions.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListServiceBusSubscriptions");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Service Bus topic subscription")]
    public async Task<string> GetServiceBusSubscriptionAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Subscription name")] string subscriptionName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServiceBusSubscriptionDto? subscription = await serviceBusService.GetSubscriptionAsync(resourceGroupName, namespaceName, topicName, subscriptionName, subscriptionId);
            if (subscription is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Subscription {subscriptionName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, subscription },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServiceBusSubscription");
        }
    }

    [McpServerTool]
    [Description("Create a new Service Bus topic subscription")]
    public async Task<string> CreateServiceBusSubscriptionAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Subscription name")] string subscriptionName,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional max delivery count (default: 10)")] int? maxDeliveryCount = null)
    {
        try
        {
            ServiceBusSubscriptionDto subscription = await serviceBusService.CreateSubscriptionAsync(resourceGroupName, namespaceName, topicName, subscriptionName, subscriptionId, maxDeliveryCount);
            return JsonSerializer.Serialize(new { success = true, subscription },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateServiceBusSubscription");
        }
    }

    [McpServerTool]
    [Description("Delete a Service Bus topic subscription")]
    public async Task<string> DeleteServiceBusSubscriptionAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Topic name")] string topicName,
        [Description("Subscription name")] string subscriptionName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await serviceBusService.DeleteSubscriptionAsync(resourceGroupName, namespaceName, topicName, subscriptionName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Subscription {subscriptionName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteServiceBusSubscription");
        }
    }

    #endregion

    #region Message Tools

    [McpServerTool]
    [Description("Send a message to a Service Bus queue or topic")]
    public async Task<string> SendServiceBusMessageAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Queue or topic name")] string queueOrTopicName,
        [Description("Message body (text content)")] string messageBody,
        [Description("Optional message properties as JSON object (e.g., '{\"Priority\":\"High\",\"CustomerId\":123}')")] string? propertiesJson = null)
    {
        try
        {
            Dictionary<string, object>? properties = null;
            if (!string.IsNullOrEmpty(propertiesJson))
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, object>>(propertiesJson);
            }

            string messageId = await serviceBusService.SendMessageAsync(namespaceName, queueOrTopicName, messageBody, properties);
            return JsonSerializer.Serialize(new { success = true, messageId, message = $"Message sent to {queueOrTopicName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SendServiceBusMessage");
        }
    }

    [McpServerTool]
    [Description("Receive and complete messages from a Service Bus queue (destructive read)")]
    public async Task<string> ReceiveServiceBusMessagesAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName,
        [Description("Maximum number of messages to receive (default: 10)")] int maxMessages = 10,
        [Description("Maximum wait time in seconds (default: 5)")] int maxWaitTimeSeconds = 5)
    {
        try
        {
            IEnumerable<ServiceBusMessageDto> messages = await serviceBusService.ReceiveMessagesAsync(namespaceName, queueName, maxMessages, maxWaitTimeSeconds);
            return JsonSerializer.Serialize(new { success = true, messages = messages.ToArray(), count = messages.Count() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ReceiveServiceBusMessages");
        }
    }

    [McpServerTool]
    [Description("Peek messages from a Service Bus queue without removing them (non-destructive read)")]
    public async Task<string> PeekServiceBusMessagesAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName,
        [Description("Maximum number of messages to peek (default: 10)")] int maxMessages = 10)
    {
        try
        {
            IEnumerable<ServiceBusMessageDto> messages = await serviceBusService.PeekMessagesAsync(namespaceName, queueName, maxMessages);
            return JsonSerializer.Serialize(new { success = true, messages = messages.ToArray(), count = messages.Count() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "PeekServiceBusMessages");
        }
    }

    [McpServerTool]
    [Description("Get the current message count in a Service Bus queue")]
    public async Task<string> GetServiceBusQueueMessageCountAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName)
    {
        try
        {
            long messageCount = await serviceBusService.GetQueueMessageCountAsync(namespaceName, queueName);
            return JsonSerializer.Serialize(new { success = true, queueName, messageCount },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServiceBusQueueMessageCount");
        }
    }

    [McpServerTool]
    [Description("Purge all messages from a Service Bus queue (WARNING: Destructive operation)")]
    public async Task<string> PurgeServiceBusQueueAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Queue name")] string queueName)
    {
        try
        {
            string result = await serviceBusService.PurgeQueueAsync(namespaceName, queueName);
            return JsonSerializer.Serialize(new { success = true, message = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "PurgeServiceBusQueue");
        }
    }

    #endregion

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            operation,
            error = ex.Message,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}
