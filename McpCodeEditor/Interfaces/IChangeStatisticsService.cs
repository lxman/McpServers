namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for generating change tracking statistics and analytics
/// </summary>
public interface IChangeStatisticsService
{
    /// <summary>
    /// Get comprehensive change statistics
    /// </summary>
    /// <param name="timeRange">Optional time range to filter statistics</param>
    /// <returns>Statistics object with various metrics</returns>
    Task<object> GetChangeStatsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Get daily activity statistics
    /// </summary>
    /// <param name="days">Number of days to include (default: 7)</param>
    /// <returns>Dictionary of daily activity counts</returns>
    Task<Dictionary<string, int>> GetDailyActivityAsync(int days = 7);

    /// <summary>
    /// Get operation statistics (counts by operation type)
    /// </summary>
    /// <param name="timeRange">Optional time range to filter statistics</param>
    /// <returns>Dictionary of operation counts</returns>
    Task<Dictionary<string, int>> GetOperationStatsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Get file modification statistics
    /// </summary>
    /// <param name="timeRange">Optional time range to filter statistics</param>
    /// <returns>Statistics about modified files</returns>
    Task<object> GetFileModificationStatsAsync(TimeSpan? timeRange = null);
}
