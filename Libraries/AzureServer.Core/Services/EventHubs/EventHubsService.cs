using Azure;
using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.EventHubs.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.EventHubs.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.EventHubs;

public class EventHubsService(
    ArmClientFactory armClientFactory,
    ILogger<EventHubsService> logger) : IEventHubsService
{
    private readonly Dictionary<string, EventHubProducerClient> _producerClients = new();
    private readonly Dictionary<string, EventHubConsumerClient> _consumerClients = new();

    private async Task<EventHubProducerClient> GetProducerClientAsync(string namespaceName, string eventHubName)
    {
        var key = $"{namespaceName}/{eventHubName}";
        if (_producerClients.TryGetValue(key, out var existingClient))
            return existingClient;

        var fullyQualifiedNamespace = $"{namespaceName}.servicebus.windows.net";
        var client = new EventHubProducerClient(fullyQualifiedNamespace, eventHubName, await armClientFactory.GetCredentialAsync());
        _producerClients[key] = client;

        return client;
    }

    private async Task<EventHubConsumerClient> GetConsumerClientAsync(string namespaceName, string eventHubName, string consumerGroup)
    {
        var key = $"{namespaceName}/{eventHubName}/{consumerGroup}";
        if (_consumerClients.TryGetValue(key, out var existingClient))
            return existingClient;

        var fullyQualifiedNamespace = $"{namespaceName}.servicebus.windows.net";
        var client = new EventHubConsumerClient(consumerGroup, fullyQualifiedNamespace, eventHubName, await armClientFactory.GetCredentialAsync());
        _consumerClients[key] = client;

        return client;
    }

    #region Namespace Management

    public async Task<IEnumerable<EventHubsNamespaceDto>> ListNamespacesAsync(string? resourceGroupName = null, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var namespaces = new List<EventHubsNamespaceDto>();

            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                // List namespaces in specific resource group
                var subscription = string.IsNullOrEmpty(subscriptionId)
                    ? await client.GetDefaultSubscriptionAsync()
                    : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

                ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                
                await foreach (var ns in resourceGroup.GetEventHubsNamespaces())
                {
                    namespaces.Add(MapNamespace(ns));
                }
            }
            else
            {
                // List all namespaces across all resource groups
                var subscription = string.IsNullOrEmpty(subscriptionId)
                    ? await client.GetDefaultSubscriptionAsync()
                    : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

                await foreach (var ns in subscription.GetEventHubsNamespacesAsync())
                {
                    namespaces.Add(MapNamespace(ns));
                }
            }

            logger.LogInformation("Retrieved {Count} Event Hubs namespaces", namespaces.Count);
            return namespaces;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Event Hubs namespaces");
            throw;
        }
    }

    public async Task<EventHubsNamespaceDto?> GetNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);

            return MapNamespace(ns);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Event Hubs namespace {NamespaceName} not found in resource group {ResourceGroup}", namespaceName, resourceGroupName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Event Hubs namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task<EventHubsNamespaceDto> CreateNamespaceAsync(string resourceGroupName, string namespaceName, string location, string? subscriptionId = null, string? sku = "Standard")
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            var data = new EventHubsNamespaceData(new AzureLocation(location))
            {
                Sku = new EventHubsSku(sku == "Premium" ? EventHubsSkuName.Premium : EventHubsSkuName.Standard)
            };

            ArmOperation<EventHubsNamespaceResource> operation = await resourceGroup.GetEventHubsNamespaces()
                .CreateOrUpdateAsync(WaitUntil.Completed, namespaceName, data);

            logger.LogInformation("Created Event Hubs namespace {NamespaceName} in resource group {ResourceGroup}", namespaceName, resourceGroupName);
            return MapNamespace(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Event Hubs namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task DeleteNamespaceAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);

            await ns.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted Event Hubs namespace {NamespaceName} from resource group {ResourceGroup}", namespaceName, resourceGroupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Event Hubs namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    #endregion

    #region Event Hub Management

    public async Task<IEnumerable<EventHubDto>> ListEventHubsAsync(string resourceGroupName, string namespaceName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);

            var eventHubs = new List<EventHubDto>();
            await foreach (var eventHub in ns.GetEventHubs())
            {
                eventHubs.Add(MapEventHub(eventHub));
            }

            logger.LogInformation("Retrieved {Count} event hubs from namespace {NamespaceName}", eventHubs.Count, namespaceName);
            return eventHubs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing event hubs in namespace {NamespaceName}", namespaceName);
            throw;
        }
    }

    public async Task<EventHubDto?> GetEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);

            return MapEventHub(eventHub);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Event Hub {EventHubName} not found in namespace {NamespaceName}", eventHubName, namespaceName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting event hub {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<EventHubDto> CreateEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null, int? partitionCount = null, int? messageRetentionInDays = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);

            var data = new EventHubData();
            if (partitionCount.HasValue)
                data.PartitionCount = partitionCount.Value;
            if (messageRetentionInDays.HasValue)
                data.MessageRetentionInDays = messageRetentionInDays.Value;

            ArmOperation<EventHubResource> operation = await ns.GetEventHubs()
                .CreateOrUpdateAsync(WaitUntil.Completed, eventHubName, data);

            logger.LogInformation("Created event hub {EventHubName} in namespace {NamespaceName}", eventHubName, namespaceName);
            return MapEventHub(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating event hub {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task DeleteEventHubAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);

            await eventHub.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted event hub {EventHubName} from namespace {NamespaceName}", eventHubName, namespaceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting event hub {EventHubName}", eventHubName);
            throw;
        }
    }

    #endregion

    #region Consumer Group Management

    public async Task<IEnumerable<ConsumerGroupDto>> ListConsumerGroupsAsync(string resourceGroupName, string namespaceName, string eventHubName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);

            var consumerGroups = new List<ConsumerGroupDto>();
            await foreach (var cg in eventHub.GetEventHubsConsumerGroups())
            {
                consumerGroups.Add(MapConsumerGroup(cg));
            }

            logger.LogInformation("Retrieved {Count} consumer groups from event hub {EventHubName}", consumerGroups.Count, eventHubName);
            return consumerGroups;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing consumer groups in event hub {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<ConsumerGroupDto?> GetConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);
            EventHubsConsumerGroupResource cg = await eventHub.GetEventHubsConsumerGroupAsync(consumerGroupName);

            return MapConsumerGroup(cg);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Consumer group {ConsumerGroupName} not found in event hub {EventHubName}", consumerGroupName, eventHubName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting consumer group {ConsumerGroupName}", consumerGroupName);
            throw;
        }
    }

    public async Task<ConsumerGroupDto> CreateConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);

            var data = new EventHubsConsumerGroupData();
            ArmOperation<EventHubsConsumerGroupResource> operation = await eventHub.GetEventHubsConsumerGroups()
                .CreateOrUpdateAsync(WaitUntil.Completed, consumerGroupName, data);

            logger.LogInformation("Created consumer group {ConsumerGroupName} in event hub {EventHubName}", consumerGroupName, eventHubName);
            return MapConsumerGroup(operation.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating consumer group {ConsumerGroupName}", consumerGroupName);
            throw;
        }
    }

    public async Task DeleteConsumerGroupAsync(string resourceGroupName, string namespaceName, string eventHubName, string consumerGroupName, string? subscriptionId = null)
    {
        try
        {
            var client = await armClientFactory.GetArmClientAsync();
            var subscription = string.IsNullOrEmpty(subscriptionId)
                ? await client.GetDefaultSubscriptionAsync()
                : await client.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}")).GetAsync();

            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            EventHubsNamespaceResource ns = await resourceGroup.GetEventHubsNamespaceAsync(namespaceName);
            EventHubResource eventHub = await ns.GetEventHubAsync(eventHubName);
            EventHubsConsumerGroupResource cg = await eventHub.GetEventHubsConsumerGroupAsync(consumerGroupName);

            await cg.DeleteAsync(WaitUntil.Completed);
            logger.LogInformation("Deleted consumer group {ConsumerGroupName} from event hub {EventHubName}", consumerGroupName, eventHubName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting consumer group {ConsumerGroupName}", consumerGroupName);
            throw;
        }
    }

    #endregion

    #region Event Operations

    public async Task<string> SendEventAsync(string namespaceName, string eventHubName, string eventBody, Dictionary<string, object>? properties = null)
    {
        try
        {
            var producer = await GetProducerClientAsync(namespaceName, eventHubName);
            
            var eventData = new EventData(eventBody);
            
            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    eventData.Properties.Add(kvp.Key, kvp.Value);
                }
            }

            await producer.SendAsync(new[] { eventData });
            
            logger.LogInformation("Sent event to event hub {EventHubName} in namespace {NamespaceName}", eventHubName, namespaceName);
            return $"Event sent successfully to {eventHubName}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending event to {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<IEnumerable<string>> SendEventBatchAsync(string namespaceName, string eventHubName, IEnumerable<string> eventBodies)
    {
        try
        {
            var producer = await GetProducerClientAsync(namespaceName, eventHubName);
            
            using var eventBatch = await producer.CreateBatchAsync();
            var sentMessages = new List<string>();

            foreach (var body in eventBodies)
            {
                var eventData = new EventData(body);
                if (eventBatch.TryAdd(eventData)) continue;
                // If we can't add to current batch, send it and create a new one
                await producer.SendAsync(eventBatch);
                sentMessages.Add($"Batch sent with {eventBatch.Count} events");
                    
                using var newBatch = await producer.CreateBatchAsync();
                newBatch.TryAdd(eventData);
            }

            // Send remaining events
            if (eventBatch.Count > 0)
            {
                await producer.SendAsync(eventBatch);
                sentMessages.Add($"Final batch sent with {eventBatch.Count} events");
            }
            
            logger.LogInformation("Sent batch of events to event hub {EventHubName}", eventHubName);
            return sentMessages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending event batch to {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<IEnumerable<EventDataDto>> ReceiveEventsAsync(string namespaceName, string eventHubName, string consumerGroup = "$Default", int maxEvents = 100, int maxWaitTimeSeconds = 10)
    {
        try
        {
            var consumer = await GetConsumerClientAsync(namespaceName, eventHubName, consumerGroup);
            
            var events = new List<EventDataDto>();
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(maxWaitTimeSeconds)).Token;

            await foreach (var partitionEvent in consumer.ReadEventsAsync(cancellationToken))
            {
                events.Add(MapEventData(partitionEvent));
                
                if (events.Count >= maxEvents)
                    break;
            }

            logger.LogInformation("Received {Count} events from event hub {EventHubName}", events.Count, eventHubName);
            return events;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Event receiving timed out or completed");
            return Array.Empty<EventDataDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving events from {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<EventHubPropertiesDto> GetEventHubPropertiesAsync(string namespaceName, string eventHubName)
    {
        try
        {
            var producer = await GetProducerClientAsync(namespaceName, eventHubName);
            var properties = await producer.GetEventHubPropertiesAsync();

            return new EventHubPropertiesDto
            {
                Name = properties.Name,
                CreatedAt = properties.CreatedOn.DateTime,
                PartitionIds = properties.PartitionIds
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting event hub properties for {EventHubName}", eventHubName);
            throw;
        }
    }

    public async Task<PartitionPropertiesDto> GetPartitionPropertiesAsync(string namespaceName, string eventHubName, string partitionId)
    {
        try
        {
            var producer = await GetProducerClientAsync(namespaceName, eventHubName);
            var properties = await producer.GetPartitionPropertiesAsync(partitionId);

            return new PartitionPropertiesDto
            {
                EventHubName = properties.EventHubName,
                PartitionId = properties.Id,
                BeginningSequenceNumber = properties.BeginningSequenceNumber,
                LastEnqueuedSequenceNumber = properties.LastEnqueuedSequenceNumber,
                LastEnqueuedOffset = properties.LastEnqueuedOffset,
                LastEnqueuedTime = properties.LastEnqueuedTime.DateTime,
                IsEmpty = properties.IsEmpty
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting partition properties for partition {PartitionId}", partitionId);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static EventHubsNamespaceDto MapNamespace(EventHubsNamespaceResource ns)
    {
        return new EventHubsNamespaceDto
        {
            Name = ns.Data.Name,
            Id = ns.Data.Id.ToString(),
            Location = ns.Data.Location.ToString(),
            ResourceGroup = ns.Id.ResourceGroupName ?? string.Empty,
            SubscriptionId = ns.Id.SubscriptionId ?? string.Empty,
            Sku = ns.Data.Sku?.Name.ToString(),
            Status = ns.Data.Status,
            ServiceBusEndpoint = ns.Data.ServiceBusEndpoint,
            IsAutoInflateEnabled = ns.Data.IsAutoInflateEnabled,
            MaximumThroughputUnits = ns.Data.MaximumThroughputUnits,
            Tags = ns.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CreatedAt = ns.Data.CreatedOn?.DateTime,
            UpdatedAt = ns.Data.UpdatedOn?.DateTime
        };
    }

    private static EventHubDto MapEventHub(EventHubResource eventHub)
    {
        return new EventHubDto
        {
            Name = eventHub.Data.Name,
            Id = eventHub.Data.Id.ToString(),
            PartitionCount = Convert.ToInt32(eventHub.Data.PartitionCount),
            MessageRetentionInDays = eventHub.Data.MessageRetentionInDays,
            Status = eventHub.Data.Status?.ToString(),
            PartitionIds = eventHub.Data.PartitionIds,
            CreatedAt = eventHub.Data.CreatedOn?.DateTime,
            UpdatedAt = eventHub.Data.UpdatedOn?.DateTime
        };
    }

    private static ConsumerGroupDto MapConsumerGroup(EventHubsConsumerGroupResource cg)
    {
        return new ConsumerGroupDto
        {
            Name = cg.Data.Name,
            Id = cg.Data.Id.ToString(),
            UserMetadata = cg.Data.UserMetadata,
            CreatedAt = cg.Data.CreatedOn?.DateTime,
            UpdatedAt = cg.Data.UpdatedOn?.DateTime
        };
    }

    private static EventDataDto MapEventData(PartitionEvent partitionEvent)
    {
        return new EventDataDto
        {
            Body = partitionEvent.Data.EventBody.ToString(),
            ContentType = partitionEvent.Data.ContentType,
            Properties = partitionEvent.Data.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            SystemProperties = partitionEvent.Data.SystemProperties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
            SequenceNumber = partitionEvent.Data.SequenceNumber,
            Offset = partitionEvent.Data.Offset,
            EnqueuedTime = partitionEvent.Data.EnqueuedTime.DateTime,
            PartitionKey = partitionEvent.Data.PartitionKey,
            PartitionId = partitionEvent.Partition.PartitionId
        };
    }

    #endregion
}
