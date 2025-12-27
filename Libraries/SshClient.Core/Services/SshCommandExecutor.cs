using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using SshClient.Core.Models;

namespace SshClient.Core.Services;

/// <summary>
/// Executes commands on SSH connections with output management
/// </summary>
public sealed class SshCommandExecutor(
    SshConnectionManager connectionManager,
    ILogger<SshCommandExecutor> logger)
{
    private const int DefaultMaxOutputBytes = 100_000;
    private const int DefaultTimeoutSeconds = 300;

    /// <summary>
    /// Executes a command on a named connection
    /// </summary>
    public async Task<SshCommandResult> ExecuteAsync(
        string connectionName,
        string command,
        int? timeoutSeconds = null,
        int? maxOutputBytes = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var client = connectionManager.GetSshClient(connectionName);
        if (client is null || !client.IsConnected)
        {
            return new SshCommandResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = $"Connection '{connectionName}' not found or not connected"
            };
        }

        var timeout = timeoutSeconds ?? DefaultTimeoutSeconds;
        var maxOutput = maxOutputBytes ?? DefaultMaxOutputBytes;

        // Prepend cd command if working directory specified
        var fullCommand = string.IsNullOrEmpty(workingDirectory)
            ? command
            : $"cd {EscapeShellArg(workingDirectory)} && {command}";

        logger.LogInformation("Executing on {Connection}: {Command}", connectionName, command);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var sshCommand = client.CreateCommand(fullCommand);
            sshCommand.CommandTimeout = TimeSpan.FromSeconds(timeout);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var outputTruncated = false;
            var errorTruncated = false;

            // Execute asynchronously
            var result = await Task.Run(() =>
            {
                var asyncResult = sshCommand.BeginExecute();

                // Read output streams
                using var outputReader = new StreamReader(sshCommand.OutputStream, Encoding.UTF8, leaveOpen: true);
                using var errorReader = new StreamReader(sshCommand.ExtendedOutputStream, Encoding.UTF8, leaveOpen: true);

                while (!asyncResult.IsCompleted || !outputReader.EndOfStream || !errorReader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Read stdout
                    if (!outputReader.EndOfStream)
                    {
                        var line = outputReader.ReadLine();
                        if (line != null && outputBuilder.Length < maxOutput)
                        {
                            if (outputBuilder.Length + line.Length > maxOutput)
                            {
                                outputBuilder.AppendLine(line[..(maxOutput - outputBuilder.Length)]);
                                outputTruncated = true;
                            }
                            else
                            {
                                outputBuilder.AppendLine(line);
                            }
                        }
                        else if (line != null)
                        {
                            outputTruncated = true;
                        }
                    }

                    // Read stderr
                    if (!errorReader.EndOfStream)
                    {
                        var line = errorReader.ReadLine();
                        if (line != null && errorBuilder.Length < maxOutput)
                        {
                            if (errorBuilder.Length + line.Length > maxOutput)
                            {
                                errorBuilder.AppendLine(line[..(maxOutput - errorBuilder.Length)]);
                                errorTruncated = true;
                            }
                            else
                            {
                                errorBuilder.AppendLine(line);
                            }
                        }
                        else if (line != null)
                        {
                            errorTruncated = true;
                        }
                    }

                    Thread.Sleep(10);
                }

                sshCommand.EndExecute(asyncResult);
                return sshCommand.ExitStatus ?? -1;
            }, cancellationToken);

            stopwatch.Stop();

            var commandResult = new SshCommandResult
            {
                Success = result == 0,
                ExitCode = result,
                StandardOutput = outputBuilder.ToString().TrimEnd(),
                StandardError = errorBuilder.ToString().TrimEnd(),
                Duration = stopwatch.Elapsed,
                OutputTruncated = outputTruncated || errorTruncated
            };

            logger.LogInformation("Command completed with exit code {ExitCode} in {Duration}ms",
                result, stopwatch.ElapsedMilliseconds);

            return commandResult;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Command cancelled: {Command}", command);
            return new SshCommandResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = "Command was cancelled",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command execution failed: {Command}", command);
            return new SshCommandResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Executes multiple commands sequentially
    /// </summary>
    public async Task<IReadOnlyList<SshCommandResult>> ExecuteBatchAsync(
        string connectionName,
        IEnumerable<string> commands,
        bool stopOnError = true,
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SshCommandResult>();

        foreach (var command in commands)
        {
            var result = await ExecuteAsync(connectionName, command, timeoutSeconds, cancellationToken: cancellationToken);
            results.Add(result);

            if (stopOnError && !result.Success)
            {
                logger.LogWarning("Batch execution stopped due to error in command: {Command}", command);
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Executes a command and returns just the exit code (for simple checks)
    /// </summary>
    public async Task<int> ExecuteAndGetExitCodeAsync(
        string connectionName,
        string command,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(connectionName, command, maxOutputBytes: 1000, cancellationToken: cancellationToken);
        return result.ExitCode;
    }

    private static string EscapeShellArg(string arg)
    {
        // Simple shell argument escaping
        if (!arg.Contains('\''))
            return $"'{arg}'";

        return "'" + arg.Replace("'", "'\"'\"'") + "'";
    }
}