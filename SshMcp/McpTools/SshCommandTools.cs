using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SshClient.Core.Services;

namespace SshMcp.McpTools;

[McpServerToolType]
public sealed class SshCommandTools(
    SshCommandExecutor commandExecutor,
    OutputGuard outputGuard,
    ILogger<SshCommandTools> logger)
{
    [McpServerTool]
    [DisplayName("ssh_execute")]
    [Description("Execute a command on an SSH connection. Returns stdout, stderr, and exit code.")]
    public async Task<string> Execute(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Command to execute")] string command,
        [Description("Working directory (optional)")] string? workingDirectory = null,
        [Description("Timeout in seconds (default: 300)")] int timeoutSeconds = 300,
        [Description("Maximum output bytes to capture (default: 100000)")] int maxOutputBytes = 100000)
    {
        try
        {
            var result = await commandExecutor.ExecuteAsync(
                connectionName,
                command,
                timeoutSeconds,
                maxOutputBytes,
                workingDirectory);

            return result.ToGuardedResponse(outputGuard, "ssh_execute");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command execution failed");
            return ex.ToErrorResponse(outputGuard, "Check command syntax and connection status");
        }
    }

    [McpServerTool]
    [DisplayName("ssh_execute_batch")]
    [Description("Execute multiple commands sequentially on an SSH connection.")]
    public async Task<string> ExecuteBatch(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("JSON array of commands to execute")] string commandsJson,
        [Description("Stop on first error (default: true)")] bool stopOnError = true,
        [Description("Timeout per command in seconds (default: 300)")] int timeoutSeconds = 300)
    {
        try
        {
            var commands = JsonSerializer.Deserialize<string[]>(commandsJson);
            if (commands is null || commands.Length == 0)
            {
                return "No commands provided".ToErrorResponse(outputGuard);
            }

            var results = await commandExecutor.ExecuteBatchAsync(
                connectionName,
                commands,
                stopOnError,
                timeoutSeconds);

            return results.ToGuardedResponse(outputGuard, "ssh_execute_batch");
        }
        catch (JsonException ex)
        {
            return ex.ToErrorResponse(outputGuard, "Provide a valid JSON array of command strings");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch execution failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("ssh_check_exit_code")]
    [Description("Execute a command and return just the exit code. Useful for simple checks.")]
    public async Task<string> CheckExitCode(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Command to execute")] string command)
    {
        try
        {
            var exitCode = await commandExecutor.ExecuteAndGetExitCodeAsync(connectionName, command);
            return new { ExitCode = exitCode, Success = exitCode == 0 }.ToGuardedResponse(outputGuard, "ssh_check_exit_code");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exit code check failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }
}
