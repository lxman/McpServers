namespace Mcp.Common.Core.Extensions;

/// <summary>
/// Extension methods for DateTime operations commonly used across MCP servers
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a DateTime to ISO 8601 format string with milliseconds
    /// </summary>
    public static string ToIso8601Format(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    /// <summary>
    /// Converts a nullable DateTime to ISO 8601 format string with milliseconds
    /// </summary>
    public static string ToIso8601Format(this DateTime? dateTime)
    {
        return dateTime?.ToIso8601Format() ?? string.Empty;
    }

    /// <summary>
    /// Gets a human-readable time ago string (e.g., "2 hours ago")
    /// </summary>
    public static string ToTimeAgo(this DateTime dateTime)
    {
        TimeSpan timeSpan = DateTime.UtcNow - dateTime.ToUniversalTime();

        return timeSpan.TotalDays switch
        {
            >= 365 => $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago",
            >= 30 => $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago",
            >= 7 => $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) != 1 ? "s" : "")} ago",
            >= 1 => $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago",
            _ => timeSpan.TotalHours switch
            {
                >= 1 => $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago",
                _ => timeSpan.TotalMinutes switch
                {
                    >= 1 => $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago",
                    _ => "Just now"
                }
            }
        };
    }

    /// <summary>
    /// Gets a human-readable time ago string for nullable DateTime
    /// </summary>
    public static string ToTimeAgo(this DateTime? dateTime)
    {
        return dateTime?.ToTimeAgo() ?? "Unknown";
    }

    /// <summary>
    /// Checks if a DateTime is within the last specified number of days
    /// </summary>
    public static bool IsWithinLastDays(this DateTime dateTime, int days)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);
        return dateTime.ToUniversalTime() >= cutoff;
    }

    /// <summary>
    /// Checks if a nullable DateTime is within the last specified number of days
    /// </summary>
    public static bool IsWithinLastDays(this DateTime? dateTime, int days)
    {
        return dateTime?.IsWithinLastDays(days) ?? false;
    }

    /// <summary>
    /// Converts DateTime to simple date format (yyyy-MM-dd)
    /// </summary>
    public static string ToSimpleDateFormat(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Checks if a DateTime is today (UTC)
    /// </summary>
    public static bool IsToday(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().Date == DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Checks if a DateTime is within a specified number of hours
    /// </summary>
    public static bool IsWithinLastHours(this DateTime dateTime, int hours)
    {
        DateTime cutoff = DateTime.UtcNow.AddHours(-hours);
        return dateTime.ToUniversalTime() >= cutoff;
    }
}
