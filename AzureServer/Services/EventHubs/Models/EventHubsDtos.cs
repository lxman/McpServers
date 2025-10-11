namespace AzureServer.Services.EventHubs.Models;

public class EventHubsNamespaceDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Status { get; set; }
    public string? ServiceBusEndpoint { get; set; }
    public bool? IsAutoInflateEnabled { get; set; }
    public int? MaximumThroughputUnits { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EventHubDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int? PartitionCount { get; set; }
    public long? MessageRetentionInDays { get; set; }
    public string? Status { get; set; }
    public IEnumerable<string>? PartitionIds { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ConsumerGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string? UserMetadata { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EventDataDto
{
    public string Body { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public Dictionary<string, object>? SystemProperties { get; set; }
    public long SequenceNumber { get; set; }
    public long Offset { get; set; }
    public DateTime EnqueuedTime { get; set; }
    public string? PartitionKey { get; set; }
    public string PartitionId { get; set; } = string.Empty;
}

public class EventHubPropertiesDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IEnumerable<string> PartitionIds { get; set; } = Array.Empty<string>();
}

public class PartitionPropertiesDto
{
    public string EventHubName { get; set; } = string.Empty;
    public string PartitionId { get; set; } = string.Empty;
    public long BeginningSequenceNumber { get; set; }
    public long LastEnqueuedSequenceNumber { get; set; }
    public long LastEnqueuedOffset { get; set; }
    public DateTime LastEnqueuedTime { get; set; }
    public bool IsEmpty { get; set; }
}
