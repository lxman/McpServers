using System.ComponentModel;
using System.Text.Json;
using Amazon.QuickSight.Model;
using AwsMcp.QuickSight;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

/// <summary>
/// QuickSight embedding tools for generating embed URLs
/// </summary>
[McpServerToolType]
public class QuickSightEmbedTools(QuickSightService quickSightService)
{
    [McpServerTool]
    [Description("Generate an anonymous embed URL for a QuickSight dashboard")]
    public async Task<string> GenerateEmbedUrlForAnonymousUser(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("QuickSight namespace (default: 'default')")]
        string awsNamespace,
        [Description("Comma-separated list of authorized resource ARNs (e.g., dashboard ARNs)")]
        string authorizedResourceArns,
        [Description("Session lifetime in minutes (default: 600, max: 600)")]
        long sessionLifetimeInMinutes = 600)
    {
        try
        {
            var resourceArnsList = authorizedResourceArns
                .Split(',')
                .Select(arn => arn.Trim())
                .Where(arn => !string.IsNullOrEmpty(arn))
                .ToList();

            if (resourceArnsList.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "At least one authorized resource ARN must be provided"
                });
            }

            GenerateEmbedUrlForAnonymousUserResponse response = await quickSightService.GenerateEmbedUrlForAnonymousUserAsync(
                awsAccountId,
                awsNamespace,
                resourceArnsList,
                null,
                sessionLifetimeInMinutes);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                embedUrl = response.EmbedUrl,
                status = response.Status,
                requestId = response.RequestId,
                anonymousUserArn = response.AnonymousUserArn
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
