using MongoDB.Bson.Serialization.Attributes;

namespace SeleniumChrome.Core.Models;

public class AntiDetectionConfig
{
    public List<string> UserAgents { get; set; } = [];
    public bool RequiresCookieAccept { get; set; }
    public bool RequiresLogin { get; set; }

    [BsonIgnoreIfDefault]
    public bool RequiresAuthentication { get; set; }

    public bool UsesCloudflare { get; set; }
    public List<string> ProxyList { get; set; } = [];
    public string ChromeBinaryPath { get; set; } = string.Empty;

    // Legacy fields from older schema - ignore if not present
    [BsonIgnoreIfDefault]
    public bool StealthModeRequired { get; set; }

    [BsonIgnoreIfDefault]
    public bool RandomizeUserAgent { get; set; }

    [BsonIgnoreIfDefault]
    public bool ApiCallsOnly { get; set; }

    [BsonIgnoreIfDefault]
    public bool RequiresAPIKey { get; set; }

    [BsonIgnoreIfDefault]
    public string CloudflareProtectionLevel { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public string BypassDifficulty { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public string Notes { get; set; } = string.Empty;
}