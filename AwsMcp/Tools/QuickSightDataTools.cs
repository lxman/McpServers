using System.ComponentModel;
using System.Text.Json;
using Amazon.QuickSight.Model;
using AwsMcp.QuickSight;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class QuickSightDataTools(QuickSightService quickSightService)
{
    [McpServerTool]
    [Description("List all QuickSight data sets in an AWS account")]
    public async Task<string> ListDataSets(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListDataSetsResponse response = await quickSightService.ListDataSetsAsync(awsAccountId, maxResults);
            
            var dataSets = response.DataSetSummaries.Select(ds => new
            {
                ds.DataSetId,
                ds.Name,
                ds.Arn,
                ds.CreatedTime,
                ds.LastUpdatedTime,
                ImportMode = ds.ImportMode?.Value,
                RowLevelPermissionDataSet = ds.RowLevelPermissionDataSet != null ? new
                {
                    ds.RowLevelPermissionDataSet.Arn,
                    PermissionPolicy = ds.RowLevelPermissionDataSet.PermissionPolicy?.Value
                } : null
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSets,
                count = response.DataSetSummaries.Count,
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
    [Description("Get detailed information about a specific QuickSight data set")]
    public async Task<string> DescribeDataSet(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Data Set ID")]
        string dataSetId)
    {
        try
        {
            DescribeDataSetResponse response = await quickSightService.DescribeDataSetAsync(awsAccountId, dataSetId);
            
            var dataSet = response.DataSet;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSet = new
                {
                    dataSet.DataSetId,
                    dataSet.Name,
                    dataSet.Arn,
                    dataSet.CreatedTime,
                    dataSet.LastUpdatedTime,
                    ImportMode = dataSet.ImportMode?.Value,
                    dataSet.ConsumedSpiceCapacityInBytes,
                    PhysicalTableMap = dataSet.PhysicalTableMap?.Keys,
                    LogicalTableMap = dataSet.LogicalTableMap?.Keys,
                    OutputColumns = dataSet.OutputColumns?.Select(c => new
                    {
                        c.Name,
                        Type = c.Type?.Value,
                        c.Description
                    }),
                    ColumnGroups = dataSet.ColumnGroups?.Select(cg => new
                    {
                        cg.GeoSpatialColumnGroup?.Name, cg.GeoSpatialColumnGroup?.Columns
                    })
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
    [Description("List all QuickSight data sources in an AWS account")]
    public async Task<string> ListDataSources(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListDataSourcesResponse response = await quickSightService.ListDataSourcesAsync(awsAccountId, maxResults);
            
            var dataSources = response.DataSources.Select(ds => new
            {
                ds.DataSourceId,
                ds.Name,
                ds.Arn,
                Type = ds.Type?.Value,
                ds.CreatedTime,
                ds.LastUpdatedTime,
                Status = ds.Status?.Value
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSources,
                count = response.DataSources.Count,
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
    [Description("Get detailed information about a specific QuickSight data source")]
    public async Task<string> DescribeDataSource(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Data Source ID")]
        string dataSourceId)
    {
        try
        {
            DescribeDataSourceResponse response = await quickSightService.DescribeDataSourceAsync(awsAccountId, dataSourceId);
            
            var dataSource = response.DataSource;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSource = new
                {
                    dataSource.DataSourceId,
                    dataSource.Name,
                    dataSource.Arn,
                    Type = dataSource.Type?.Value,
                    Status = dataSource.Status?.Value,
                    dataSource.CreatedTime,
                    dataSource.LastUpdatedTime,
                    VpcConnectionProperties = dataSource.VpcConnectionProperties != null ? new
                    {
                        dataSource.VpcConnectionProperties.VpcConnectionArn
                    } : null,
                    SslProperties = dataSource.SslProperties != null ? new
                    {
                        dataSource.SslProperties.DisableSsl
                    } : null,
                    ErrorInfo = dataSource.ErrorInfo != null ? new
                    {
                        Type = dataSource.ErrorInfo.Type?.Value, dataSource.ErrorInfo.Message
                    } : null
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
