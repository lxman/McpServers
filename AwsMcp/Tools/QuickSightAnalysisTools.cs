using System.ComponentModel;
using System.Text.Json;
using Amazon.QuickSight.Model;
using AwsMcp.QuickSight;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class QuickSightAnalysisTools(QuickSightService quickSightService)
{
    [McpServerTool]
    [Description("List all QuickSight analyses in an AWS account")]
    public async Task<string> ListAnalyses(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListAnalysesResponse response = await quickSightService.ListAnalysesAsync(awsAccountId, maxResults);
            
            var analyses = response.AnalysisSummaryList.Select(a => new
            {
                a.AnalysisId,
                a.Name,
                a.Arn,
                Status = a.Status?.Value,
                a.CreatedTime,
                a.LastUpdatedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                analyses,
                count = response.AnalysisSummaryList.Count,
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
    [Description("Get detailed information about a specific QuickSight analysis")]
    public async Task<string> DescribeAnalysis(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Analysis ID")]
        string analysisId)
    {
        try
        {
            DescribeAnalysisResponse response = await quickSightService.DescribeAnalysisAsync(awsAccountId, analysisId);
            
            var analysis = response.Analysis;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = new
                {
                    analysis.AnalysisId,
                    analysis.Name,
                    analysis.Arn,
                    Status = analysis.Status?.Value,
                    analysis.CreatedTime,
                    analysis.LastUpdatedTime,
                    Errors = analysis.Errors?.Select(e => new
                    {
                        Type = e.Type?.Value, e.Message
                    }),
                    analysis.DataSetArns,
                    analysis.ThemeArn
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
    [Description("Search QuickSight analyses with filters")]
    public async Task<string> SearchAnalyses(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Search filters as JSON array (e.g., '[{\"Operator\":\"StringEquals\",\"Name\":\"QUICKSIGHT_USER\",\"Value\":\"arn:aws:quicksight:us-east-1:123456789012:user/default/username\"}]')")]
        string filtersJson,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            var filters = JsonSerializer.Deserialize<List<AnalysisSearchFilter>>(filtersJson);
            if (filters == null || filters.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "At least one search filter must be provided"
                });
            }

            SearchAnalysesResponse response = await quickSightService.SearchAnalysesAsync(
                awsAccountId,
                filters,
                maxResults);
            
            var analyses = response.AnalysisSummaryList.Select(a => new
            {
                a.AnalysisId,
                a.Name,
                a.Arn,
                Status = a.Status?.Value,
                a.CreatedTime,
                a.LastUpdatedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                analyses,
                count = response.AnalysisSummaryList.Count,
                nextToken = response.NextToken
            });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Invalid JSON format for filters: {ex.Message}"
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
