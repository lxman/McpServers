using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpUtilitiesServer;

/// <summary>
/// Provides time-related utilities through MCP.
/// </summary>
[McpServerToolType]
public class TimeUtilities(ILogger<TimeUtilities> logger)
{
    /// <summary>
    /// Gets the current system time in various formats.
    /// </summary>
    [McpServerTool, DisplayName("get_current_time")]
    [Description("Get the current system time")]
    public string GetCurrentTime()
    {
        logger.LogInformation("GetCurrentTime called");
        
        DateTime now = DateTime.Now;
        DateTime utcNow = DateTime.UtcNow;
        
        var result = new
        {
            iso = now.ToString("o"),
            utcIso = utcNow.ToString("o"),
            localTime = now.ToString("F"),
            unixTimestampMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            unixTimestampSec = DateTimeOffset.Now.ToUnixTimeSeconds(),
            timeZone = TimeZoneInfo.Local.DisplayName,
            timeZoneOffset = TimeZoneInfo.Local.BaseUtcOffset.ToString()
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Calculates elapsed time between now and a given timestamp or between two timestamps.
    /// </summary>
    [McpServerTool, DisplayName("calculate_elapsed_time")]
    [Description("Calculate elapsed time from a timestamp")]
    public string CalculateElapsedTime(
        [Description("Start timestamp (ISO 8601 format)")] string startTimestamp,
        [Description("End timestamp (leave empty to use current time)")] string? endTimestamp = null)
    {
        logger.LogInformation("CalculateElapsedTime called with start: {Start}, end: {End}", 
            startTimestamp, endTimestamp ?? "now");
        
        try
        {
            // Parse start timestamp with proper timezone handling
            DateTimeOffset start = DateTimeOffset.Parse(startTimestamp, null, DateTimeStyles.RoundtripKind);
            
            // Parse end timestamp or use current UTC time, ensuring both are in UTC for calculation
            DateTimeOffset end = string.IsNullOrEmpty(endTimestamp) 
                ? DateTimeOffset.UtcNow 
                : DateTimeOffset.Parse(endTimestamp, null, DateTimeStyles.RoundtripKind);
            
            // Convert both to UTC for accurate calculation
            DateTimeOffset startUtc = start.ToUniversalTime();
            DateTimeOffset endUtc = end.ToUniversalTime();
            
            TimeSpan elapsed = endUtc - startUtc;
            
            var result = new
            {
                start = startUtc.ToString("o"),
                end = endUtc.ToString("o"),
                elapsedTotalSeconds = elapsed.TotalSeconds,
                elapsedTotalMilliseconds = elapsed.TotalMilliseconds,
                elapsedFormatted = FormatTimeSpan(elapsed),
                isPositive = elapsed.TotalMilliseconds >= 0
            };
            
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating elapsed time");
            return JsonSerializer.Serialize(new 
            { 
                error = ex.Message,
                startTimestamp,
                endTimestamp
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
    
    /// <summary>
    /// Gets the current timestamp in various formats.
    /// </summary>
    [McpServerTool, DisplayName("get_timestamp")]
    [Description("Get timestamp in multiple formats")]
    public string GetTimestamp()
    {
        logger.LogInformation("GetTimestamp called");
        
        DateTimeOffset now = DateTimeOffset.Now;
        
        var result = new
        {
            iso8601 = now.ToString("o"),
            rfc1123 = now.ToString("r"),
            rfc3339 = now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
            unixTimestampMs = now.ToUnixTimeMilliseconds(),
            unixTimestampSec = now.ToUnixTimeSeconds(),
            localDateTime = now.ToString("F"),
            utcDateTime = now.ToUniversalTime().ToString("F")
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Starts a timer and returns the start timestamp. Use with StopTimer to measure elapsed time.
    /// </summary>
    [McpServerTool, DisplayName("start_timer")]
    [Description("Start a named timer")]
    public string StartTimer([Description("Name of the timer")] string timerName)
    {
        logger.LogInformation("StartTimer called for timer: {Timer}", timerName);
        
        // Validate timer name
        if (string.IsNullOrWhiteSpace(timerName))
        {
            var errorResult = new
            {
                error = "Timer name cannot be null or empty",
                timerName
            };
            
            return JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsIndented);
        }
        
        var timestampKey = $"timer_{timerName}";
        
        // Check if timer already exists and warn about collision
        string? existingTimer = Environment.GetEnvironmentVariable(timestampKey);
        bool isOverwriting = !string.IsNullOrEmpty(existingTimer);
        
        // Store the UTC timestamp for consistent timezone handling
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        Environment.SetEnvironmentVariable(timestampKey, startTime.ToString("o"));
        
        var result = new
        {
            timerName,
            startTime = startTime.ToString("o"),
            unixTimestampMs = startTime.ToUnixTimeMilliseconds(),
            message = isOverwriting 
                ? $"Timer '{timerName}' restarted (overwrote existing timer)"
                : $"Timer '{timerName}' started",
            wasOverwritten = isOverwriting
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Stops a timer and returns the elapsed time.
    /// </summary>
    [McpServerTool, DisplayName("stop_timer")]
    [Description("Stop a named timer and get elapsed time")]
    public string StopTimer([Description("Name of the timer")] string timerName)
    {
        logger.LogInformation("StopTimer called for timer: {Timer}", timerName);
        
        var timestampKey = $"timer_{timerName}";
        string? startTimeStr = Environment.GetEnvironmentVariable(timestampKey);
        
        if (string.IsNullOrEmpty(startTimeStr))
        {
            var errorResult = new
            {
                error = $"Timer '{timerName}' not found or not started",
                timerName
            };
            
            return JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsIndented);
        }
        
        // Parse start time as UTC and get current UTC time for consistent calculation
        DateTimeOffset startTime = DateTimeOffset.Parse(startTimeStr, null, DateTimeStyles.RoundtripKind);
        DateTimeOffset endTime = DateTimeOffset.UtcNow;
        
        // Ensure both times are in UTC for accurate calculation
        DateTimeOffset startUtc = startTime.ToUniversalTime();
        DateTimeOffset endUtc = endTime.ToUniversalTime();
        
        TimeSpan elapsed = endUtc - startUtc;
        
        // Clear the timer
        Environment.SetEnvironmentVariable(timestampKey, null);
        
        var result = new
        {
            timerName,
            startTime = startUtc.ToString("o"),
            endTime = endUtc.ToString("o"),
            elapsedSeconds = elapsed.TotalSeconds,
            elapsedMilliseconds = elapsed.TotalMilliseconds,
            elapsedFormatted = FormatTimeSpan(elapsed),
            message = $"Timer '{timerName}' stopped after {elapsed.TotalSeconds:F3} seconds"
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Gets all active timers.
    /// </summary>
    [McpServerTool, DisplayName("list_timers")]
    [Description("List all active timers")]
    public string ListTimers()
    {
        logger.LogInformation("ListTimers called");
        
        var timers = new List<object>();
        DateTimeOffset currentUtc = DateTimeOffset.UtcNow;
        
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string key = entry.Key.ToString() ?? "";
            
            if (key.StartsWith("timer_"))
            {
                string timerName = key[6..]; // Remove "timer_" prefix
                string startTimeStr = entry.Value?.ToString() ?? "";
                
                try
                {
                    // Parse the stored UTC timestamp properly
                    DateTimeOffset startTime = DateTimeOffset.Parse(startTimeStr, null, DateTimeStyles.RoundtripKind);
                    
                    // Ensure both times are in UTC for accurate calculation
                    DateTimeOffset startUtc = startTime.ToUniversalTime();
                    
                    TimeSpan elapsed = currentUtc - startUtc;
                    
                    timers.Add(new
                    {
                        timerName,
                        startTime = startUtc.ToString("o"),
                        currentElapsedSeconds = elapsed.TotalSeconds,
                        currentElapsedMilliseconds = elapsed.TotalMilliseconds
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse timer {TimerName} with value {Value}", timerName, startTimeStr);
                    // Skip invalid timer entries
                }
            }
        }
        
        var result = new
        {
            activeTimers = timers,
            count = timers.Count,
            currentTime = currentUtc.ToString("o")
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Performs a time-consuming operation to test timing accuracy.
    /// </summary>
    [McpServerTool, DisplayName("simulate_delay")]
    [Description("Simulate a process that takes the specified number of seconds")]
    public string SimulateDelay(
        [Description("Duration in seconds")] double durationSeconds = 5.0,
        [Description("Return timestamps for the operation")] bool includeTimestamps = true)
    {
        logger.LogInformation("SimulateDelay called with duration: {Duration}s", durationSeconds);
        
        // Bound the duration to reasonable values (0.1 to 30 seconds)
        durationSeconds = Math.Max(0.1, Math.Min(30, durationSeconds));
        
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        
        // Perform actual waiting
        var milliseconds = (int)(durationSeconds * 1000);
        Thread.Sleep(milliseconds);
        
        DateTimeOffset endTime = DateTimeOffset.UtcNow;
        TimeSpan actualDuration = endTime - startTime;
        
        var result = new
        {
            requestedDurationSeconds = durationSeconds,
            actualDurationSeconds = actualDuration.TotalSeconds,
            actualDurationMilliseconds = actualDuration.TotalMilliseconds,
            startTime = includeTimestamps ? startTime.ToString("o") : null,
            endTime = includeTimestamps ? endTime.ToString("o") : null,
            precisionDifference = actualDuration.TotalSeconds - durationSeconds
        };
        
        return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
    }
    
    /// <summary>
    /// Formats a TimeSpan in a human-readable format.
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        // Handle negative times
        string sign = timeSpan.TotalMilliseconds < 0 ? "-" : "";
        TimeSpan abs = timeSpan.Duration();
        
        return $"{sign}{abs.Days}d {abs.Hours}h {abs.Minutes}m {abs.Seconds}.{abs.Milliseconds:D3}s";
    }
}
