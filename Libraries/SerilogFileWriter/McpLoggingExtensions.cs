using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SerilogFileWriter;

/// <summary>
/// Extension methods for configuring MCP server logging with complete STDOUT/STDERR isolation.
/// </summary>
public static class McpLoggingExtensions
{
    /// <summary>
    /// Redirects Console.Out and Console.Error to Serilog, ensuring complete isolation
    /// from STDOUT/STDERR for MCP protocol compatibility.
    /// </summary>
    /// <param name="logger">The Serilog logger instance</param>
    /// <param name="consoleOutLevel">Log level for Console.Out writes (default: Information)</param>
    /// <param name="consoleErrorLevel">Log level for Console.Error writes (default: Error)</param>
    /// <remarks>
    /// This method should be called AFTER configuring Serilog but BEFORE starting the MCP server.
    /// All Console.WriteLine and Console.Error.WriteLine calls will be routed through Serilog
    /// with a [Console] prefix to identify their source.
    /// </remarks>
    /// <example>
    /// <code>
    /// Log.Logger = new LoggerConfiguration()
    ///     .WriteTo.File("logs/mcp-server.log")
    ///     .CreateLogger();
    ///
    /// Log.Logger.RedirectConsoleToSerilog();
    ///
    /// // Now Console.WriteLine goes through Serilog
    /// Console.WriteLine("This is logged via Serilog");
    /// </code>
    /// </example>
    public static void RedirectConsoleToSerilog(
        this ILogger logger,
        LogEventLevel consoleOutLevel = LogEventLevel.Information,
        LogEventLevel consoleErrorLevel = LogEventLevel.Error)
    {
        ArgumentNullException.ThrowIfNull(logger);

        Console.SetOut(new SerilogTextWriter(logger, consoleOutLevel));
        Console.SetError(new SerilogTextWriter(logger, consoleErrorLevel));
    }

    /// <summary>
    /// Creates a standard Serilog configuration optimized for MCP servers.
    /// Includes file sink, appropriate log levels, and timestamp formatting.
    /// </summary>
    /// <param name="logFilePath">Path to the log file (supports rolling file patterns)</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <param name="rollingInterval">Rolling interval for log files (default: Day)</param>
    /// <returns>A LoggerConfiguration ready to call .CreateLogger()</returns>
    /// <example>
    /// <code>
    /// Log.Logger = McpLoggingExtensions
    ///     .CreateMcpLoggerConfiguration("logs/mcp-server-.log")
    ///     .CreateLogger();
    ///
    /// Log.Logger.RedirectConsoleToSerilog();
    /// </code>
    /// </example>
    public static LoggerConfiguration CreateMcpLoggerConfiguration(
        string logFilePath,
        LogEventLevel minimumLevel = LogEventLevel.Debug,
        RollingInterval rollingInterval = RollingInterval.Day)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                shared: true,
                rollingInterval: rollingInterval,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                flushToDiskInterval: TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Complete one-line setup for MCP server logging.
    /// Creates Serilog logger, redirects console, and returns the logger instance.
    /// </summary>
    /// <param name="logFilePath">Path to the log file</param>
    /// <param name="minimumLevel">Minimum log level (default: Debug)</param>
    /// <param name="rollingInterval">Rolling interval for log files (default: Day)</param>
    /// <returns>The configured Serilog ILogger instance</returns>
    /// <example>
    /// <code>
    /// // One-line setup for MCP server logging
    /// Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-server-.log");
    ///
    /// // Console.WriteLine now goes through Serilog automatically
    /// Console.WriteLine("MCP server starting...");
    /// </code>
    /// </example>
    public static ILogger SetupMcpLogging(
        string logFilePath,
        LogEventLevel minimumLevel = LogEventLevel.Debug,
        RollingInterval rollingInterval = RollingInterval.Day)
    {
        Logger logger = CreateMcpLoggerConfiguration(logFilePath, minimumLevel, rollingInterval)
            .CreateLogger();

        logger.RedirectConsoleToSerilog();

        return logger;
    }
}
