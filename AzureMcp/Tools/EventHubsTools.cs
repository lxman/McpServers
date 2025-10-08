using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.EventHubs;
using AzureMcp.Services.EventHubs.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class EventHubsTools(IEventHubsService eventHubsService)
{
    #region Namespace Tools

    [McpServerTool]
    [Description("List Event Hubs namespaces")]
    public async Task<string> ListEventHubsNamespacesAsync(
        [Description("Optional resource group name")] string? resourceGroupName = null,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<EventHubsNamespaceDto> namespaces = await eventHubsService.ListNamespacesAsync(resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, namespaces = namespaces.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListEventHubsNamespaces");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Event Hubs namespace")]
    public async Task<string> GetEventHubsNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            EventHubsNamespaceDto? ns = await eventHubsService.GetNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
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
            return HandleError(ex, "GetEventHubsNamespace");
        }
    }

    [McpServerTool]
    [Description("Create a new Event Hubs namespace")]
    public async Task<string> CreateEventHubsNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Azure location (e.g., 'eastus', 'westus2')")] string location,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional SKU tier (Standard or Premium, default: Standard)")] string? sku = "Standard")
    {
        try
        {
            EventHubsNamespaceDto ns = await eventHubsService.CreateNamespaceAsync(resourceGroupName, namespaceName, location, subscriptionId, sku);
            return JsonSerializer.Serialize(new { success = true, @namespace = ns },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateEventHubsNamespace");
        }
    }

    [McpServerTool]
    [Description("Delete an Event Hubs namespace")]
    public async Task<string> DeleteEventHubsNamespaceAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await eventHubsService.DeleteNamespaceAsync(resourceGroupName, namespaceName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Namespace {namespaceName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteEventHubsNamespace");
        }
    }

    #endregion

    #region Event Hub Tools

    [McpServerTool]
    [Description("List event hubs in an Event Hubs namespace")]
    public async Task<string> ListEventHubsAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<EventHubDto> eventHubs = await eventHubsService.ListEventHubsAsync(resourceGroupName, namespaceName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, eventHubs = eventHubs.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListEventHubs");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific event hub")]
    public async Task<string> GetEventHubAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            EventHubDto? eventHub = await eventHubsService.GetEventHubAsync(resourceGroupName, namespaceName, eventHubName, subscriptionId);
            if (eventHub is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Event hub {eventHubName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, eventHub },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetEventHub");
        }
    }

    [McpServerTool]
    [Description("Create a new event hub")]
    public async Task<string> CreateEventHubAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional partition count (default: 4)")] int? partitionCount = null,
        [Description("Optional message retention in days (default: 1)")] int? messageRetentionInDays = null)
    {
        try
        {
            EventHubDto eventHub = await eventHubsService.CreateEventHubAsync(resourceGroupName, namespaceName, eventHubName, subscriptionId, partitionCount, messageRetentionInDays);
            return JsonSerializer.Serialize(new { success = true, eventHub },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateEventHub");
        }
    }

    [McpServerTool]
    [Description("Delete an event hub")]
    public async Task<string> DeleteEventHubAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await eventHubsService.DeleteEventHubAsync(resourceGroupName, namespaceName, eventHubName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Event hub {eventHubName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteEventHub");
        }
    }

    #endregion

    #region Consumer Group Tools

    [McpServerTool]
    [Description("List consumer groups for an event hub")]
    public async Task<string> ListEventHubConsumerGroupsAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ConsumerGroupDto> consumerGroups = await eventHubsService.ListConsumerGroupsAsync(resourceGroupName, namespaceName, eventHubName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, consumerGroups = consumerGroups.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListEventHubConsumerGroups");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific consumer group")]
    public async Task<string> GetEventHubConsumerGroupAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Consumer group name")] string consumerGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ConsumerGroupDto? consumerGroup = await eventHubsService.GetConsumerGroupAsync(resourceGroupName, namespaceName, eventHubName, consumerGroupName, subscriptionId);
            if (consumerGroup is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Consumer group {consumerGroupName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, consumerGroup },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetEventHubConsumerGroup");
        }
    }

    [McpServerTool]
    [Description("Create a new consumer group for an event hub")]
    public async Task<string> CreateEventHubConsumerGroupAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Consumer group name")] string consumerGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ConsumerGroupDto consumerGroup = await eventHubsService.CreateConsumerGroupAsync(resourceGroupName, namespaceName, eventHubName, consumerGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, consumerGroup },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateEventHubConsumerGroup");
        }
    }

    [McpServerTool]
    [Description("Delete a consumer group from an event hub")]
    public async Task<string> DeleteEventHubConsumerGroupAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Consumer group name")] string consumerGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            await eventHubsService.DeleteConsumerGroupAsync(resourceGroupName, namespaceName, eventHubName, consumerGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, message = $"Consumer group {consumerGroupName} deleted successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteEventHubConsumerGroup");
        }
    }

    #endregion

    #region Event Tools

    [McpServerTool]
    [Description("Send an event to an event hub")]
    public async Task<string> SendEventToEventHubAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Event body (text content)")] string eventBody,
        [Description("Optional event properties as JSON object (e.g., '{\"DeviceId\":\"sensor-01\",\"Temperature\":72.5}')")] string? propertiesJson = null)
    {
        try
        {
            Dictionary<string, object>? properties = null;
            if (!string.IsNullOrEmpty(propertiesJson))
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, object>>(propertiesJson);
            }

            string result = await eventHubsService.SendEventAsync(namespaceName, eventHubName, eventBody, properties);
            return JsonSerializer.Serialize(new { success = true, message = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SendEventToEventHub");
        }
    }

    [McpServerTool]
    [Description("Send a batch of events to an event hub")]
    public async Task<string> SendEventBatchToEventHubAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Event bodies as JSON array of strings (e.g., '[\"event1\",\"event2\",\"event3\"]')")] string eventBodiesJson)
    {
        try
        {
            IEnumerable<string>? eventBodies = JsonSerializer.Deserialize<IEnumerable<string>>(eventBodiesJson);
            if (eventBodies is null || !eventBodies.Any())
            {
                return JsonSerializer.Serialize(new { success = false, error = "Event bodies array is empty or invalid" },
                    SerializerOptions.JsonOptionsIndented);
            }

            IEnumerable<string> results = await eventHubsService.SendEventBatchAsync(namespaceName, eventHubName, eventBodies);
            return JsonSerializer.Serialize(new { success = true, batchResults = results.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SendEventBatchToEventHub");
        }
    }

    [McpServerTool]
    [Description("Receive events from an event hub (reads from all partitions)")]
    public async Task<string> ReceiveEventsFromEventHubAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Consumer group name (default: $Default)")] string consumerGroup = "$Default",
        [Description("Maximum number of events to receive (default: 100)")] int maxEvents = 100,
        [Description("Maximum wait time in seconds (default: 10)")] int maxWaitTimeSeconds = 10)
    {
        try
        {
            IEnumerable<EventDataDto> events = await eventHubsService.ReceiveEventsAsync(namespaceName, eventHubName, consumerGroup, maxEvents, maxWaitTimeSeconds);
            return JsonSerializer.Serialize(new { success = true, events = events.ToArray(), count = events.Count() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ReceiveEventsFromEventHub");
        }
    }

    [McpServerTool]
    [Description("Get event hub properties including partition information")]
    public async Task<string> GetEventHubPropertiesAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName)
    {
        try
        {
            EventHubPropertiesDto properties = await eventHubsService.GetEventHubPropertiesAsync(namespaceName, eventHubName);
            return JsonSerializer.Serialize(new { success = true, properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetEventHubProperties");
        }
    }

    [McpServerTool]
    [Description("Get partition properties for a specific partition")]
    public async Task<string> GetEventHubPartitionPropertiesAsync(
        [Description("Namespace name")] string namespaceName,
        [Description("Event hub name")] string eventHubName,
        [Description("Partition ID")] string partitionId)
    {
        try
        {
            PartitionPropertiesDto properties = await eventHubsService.GetPartitionPropertiesAsync(namespaceName, eventHubName, partitionId);
            return JsonSerializer.Serialize(new { success = true, properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetEventHubPartitionProperties");
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
