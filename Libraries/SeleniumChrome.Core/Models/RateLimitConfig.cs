using MongoDB.Bson.Serialization.Attributes;

namespace SeleniumChrome.Core.Models;

public class RateLimitConfig
{
    public int RequestsPerMinute { get; set; } = 10;
    public int DelayBetweenRequests { get; set; } = 3000;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelay { get; set; } = 5000;

    [BsonIgnoreIfDefault]
    public string Notes { get; set; } = string.Empty;
}