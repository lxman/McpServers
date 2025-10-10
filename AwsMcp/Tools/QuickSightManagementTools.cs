using System.ComponentModel;
using System.Text.Json;
using Amazon.QuickSight.Model;
using AwsMcp.Configuration;
using AwsMcp.QuickSight;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

/// <summary>
/// QuickSight management tools for initialization, account settings, users, themes, and folders
/// </summary>
[McpServerToolType]
public class QuickSightManagementTools(QuickSightService quickSightService)
{
    [McpServerTool]
    [Description("Initialize QuickSight service with AWS credentials and configuration")]
    public async Task<string> InitializeQuickSight(
        [Description("AWS region (default: us-east-1)")]
        string region = "us-east-1",
        [Description("AWS Access Key ID (optional if using profile or environment)")]
        string? accessKeyId = null,
        [Description("AWS Secret Access Key (optional if using profile or environment)")]
        string? secretAccessKey = null,
        [Description("AWS Profile name (optional)")]
        string? profileName = null,
        [Description("Custom service URL for LocalStack or other endpoints (optional)")]
        string? serviceUrl = null)
    {
        try
        {
            var config = new AwsConfiguration
            {
                Region = region,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                ProfileName = profileName,
                ServiceUrl = serviceUrl
            };

            bool success = await quickSightService.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "QuickSight service initialized successfully" : "Failed to initialize QuickSight service",
                region = config.Region
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

    [McpServerTool]
    [Description("Get QuickSight account settings for an AWS account")]
    public async Task<string> DescribeAccountSettings(
        [Description("AWS Account ID")]
        string awsAccountId)
    {
        try
        {
            DescribeAccountSettingsResponse response = await quickSightService.DescribeAccountSettingsAsync(awsAccountId);
            
            var settings = response.AccountSettings;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                accountSettings = new
                {
                    settings.AccountName,
                    Edition = settings.Edition?.Value,
                    settings.DefaultNamespace,
                    settings.NotificationEmail,
                    settings.PublicSharingEnabled,
                    settings.TerminationProtectionEnabled
                }
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

    [McpServerTool]
    [Description("List QuickSight users in an AWS account")]
    public async Task<string> ListUsers(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("QuickSight namespace (default: 'default')")]
        string awsNamespace = "default",
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListUsersResponse response = await quickSightService.ListUsersAsync(awsAccountId, awsNamespace, maxResults);
            
            var users = response.UserList.Select(u => new
            {
                u.UserName,
                u.Email,
                Role = u.Role?.Value,
                IdentityType = u.IdentityType?.Value,
                u.Active,
                u.PrincipalId,
                u.Arn
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                users,
                count = response.UserList.Count,
                nextToken = response.NextToken
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

    [McpServerTool]
    [Description("Get detailed information about a specific QuickSight user")]
    public async Task<string> DescribeUser(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("User name")]
        string userName,
        [Description("QuickSight namespace (default: 'default')")]
        string awsNamespace = "default")
    {
        try
        {
            DescribeUserResponse response = await quickSightService.DescribeUserAsync(awsAccountId, userName, awsNamespace);
            
            var user = response.User;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                user = new
                {
                    user.UserName,
                    user.Email,
                    Role = user.Role?.Value,
                    IdentityType = user.IdentityType?.Value,
                    user.Active,
                    user.PrincipalId,
                    user.Arn,
                    user.CustomPermissionsName
                }
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

    [McpServerTool]
    [Description("List QuickSight themes in an AWS account")]
    public async Task<string> ListThemes(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListThemesResponse response = await quickSightService.ListThemesAsync(awsAccountId, maxResults);
            
            var themes = response.ThemeSummaryList.Select(t => new
            {
                t.ThemeId,
                t.Name,
                t.Arn,
                t.LatestVersionNumber,
                t.CreatedTime,
                t.LastUpdatedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                themes,
                count = response.ThemeSummaryList.Count,
                nextToken = response.NextToken
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

    [McpServerTool]
    [Description("List QuickSight folders in an AWS account")]
    public async Task<string> ListFolders(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListFoldersResponse response = await quickSightService.ListFoldersAsync(awsAccountId, maxResults);
            
            var folders = response.FolderSummaryList.Select(f => new
            {
                f.FolderId,
                f.Name,
                f.Arn,
                FolderType = f.FolderType?.Value,
                f.CreatedTime,
                f.LastUpdatedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                folders,
                count = response.FolderSummaryList.Count,
                nextToken = response.NextToken
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

    [McpServerTool]
    [Description("Get detailed information about a specific QuickSight folder")]
    public async Task<string> DescribeFolder(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Folder ID")]
        string folderId)
    {
        try
        {
            DescribeFolderResponse response = await quickSightService.DescribeFolderAsync(awsAccountId, folderId);
            
            var folder = response.Folder;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                folder = new
                {
                    folder.FolderId,
                    folder.Name,
                    folder.Arn,
                    FolderType = folder.FolderType?.Value,
                    folder.FolderPath,
                    folder.CreatedTime,
                    folder.LastUpdatedTime
                }
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
