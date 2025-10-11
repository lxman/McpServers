namespace AzureServer.Services.ServiceBus.Models;

public class ServiceBusNamespaceDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Status { get; set; }
    public string? Endpoint { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ServiceBusQueueDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public long? MessageCount { get; set; }
    public long? SizeInBytes { get; set; }
    public string? Status { get; set; }
    public int? MaxDeliveryCount { get; set; }
    public TimeSpan? LockDuration { get; set; }
    public TimeSpan? DefaultMessageTimeToLive { get; set; }
    public bool? RequiresDuplicateDetection { get; set; }
    public bool? RequiresSession { get; set; }
    public bool? DeadLetteringOnMessageExpiration { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AccessedAt { get; set; }
}

public class ServiceBusTopicDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public long? SizeInBytes { get; set; }
    public string? Status { get; set; }
    public int? SubscriptionCount { get; set; }
    public TimeSpan? DefaultMessageTimeToLive { get; set; }
    public bool? RequiresDuplicateDetection { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AccessedAt { get; set; }
}

public class ServiceBusSubscriptionDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public long? MessageCount { get; set; }
    public string? Status { get; set; }
    public int? MaxDeliveryCount { get; set; }
    public TimeSpan? LockDuration { get; set; }
    public TimeSpan? DefaultMessageTimeToLive { get; set; }
    public bool? RequiresSession { get; set; }
    public bool? DeadLetteringOnMessageExpiration { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AccessedAt { get; set; }
}

public class ServiceBusMessageDto
{
    public string MessageId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public int DeliveryCount { get; set; }
    public DateTime? EnqueuedTime { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? LockToken { get; set; }
    public long SequenceNumber { get; set; }
}
