using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
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
        Renci.SshNet.SshClient? client = connectionManager.GetSshClient(connectionName);
        if (client is null || !client.IsConnected)
        {
            bool connected = await connectionManager.EnsureConnectedAsync(connectionName, cancellationToken);
            client = connected ? connectionManager.GetSshClient(connectionName) : null;

            if (client is null || !client.IsConnected)
                return CreateConnectionRecoveryResult(connectionName, command);
        }

        int timeout = timeoutSeconds ?? DefaultTimeoutSeconds;
        int maxOutput = maxOutputBytes ?? DefaultMaxOutputBytes;

        // Prepend cd command if working directory specified
        string fullCommand = string.IsNullOrEmpty(workingDirectory)
            ? command
            : $"cd {EscapeShellArg(workingDirectory)} && {command}";

        logger.LogInformation("Executing on {Connection}: {Command}", connectionName, command);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using SshCommand sshCommand = client.CreateCommand(fullCommand);
            sshCommand.CommandTimeout = TimeSpan.FromSeconds(timeout);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var outputTruncated = false;
            var errorTruncated = false;

            // Execute asynchronously
            int result = await Task.Run(() =>
            {
                IAsyncResult asyncResult = sshCommand.BeginExecute();

                // Read output streams
                using var outputReader = new StreamReader(sshCommand.OutputStream, Encoding.UTF8, leaveOpen: true);
                using var errorReader = new StreamReader(sshCommand.ExtendedOutputStream, Encoding.UTF8, leaveOpen: true);

                while (!asyncResult.IsCompleted || !outputReader.EndOfStream || !errorReader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Read stdout
                    if (!outputReader.EndOfStream)
                    {
                        string? line = outputReader.ReadLine();
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
                        string? line = errorReader.ReadLine();
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

        foreach (string command in commands)
        {
            SshCommandResult result = await ExecuteAsync(connectionName, command, timeoutSeconds, cancellationToken: cancellationToken);
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
        SshCommandResult result = await ExecuteAsync(connectionName, command, maxOutputBytes: 1000, cancellationToken: cancellationToken);
        return result.ExitCode;
    }

    private static string EscapeShellArg(string arg)
    {
        // Simple shell argument escaping
        if (!arg.Contains('\''))
            return $"'{arg}'";

        return "'" + arg.Replace("'", "'\"'\"'") + "'";
    }

    private SshCommandResult CreateConnectionRecoveryResult(string connectionName, string command)
    {
        bool hasProfile = connectionManager.HasProfile(connectionName);
        string errorCode = hasProfile
            ? SshRecoveryCodes.ConnectionNotConnected
            : SshRecoveryCodes.NoMatchingProfile;

        return new SshCommandResult
        {
            Success = false,
            ExitCode = -1,
            StandardError = hasProfile
                ? $"Connection '{connectionName}' is not active. A matching saved profile exists, but automatic reconnect did not succeed."
                : $"Connection '{connectionName}' is not active and no saved profile matched that name.",
            Command = command,
            ConnectionName = connectionName,
            ErrorCode = errorCode,
            Recoverable = true,
            Recovery = CreateRecoveryGuidance(connectionName, hasProfile)
        };
    }

    private static SshRecoveryGuidance CreateRecoveryGuidance(string connectionName, bool hasProfile)
    {
        if (hasProfile)
        {
            return new SshRecoveryGuidance
            {
                Message = $"Reconnect with the saved profile named '{connectionName}', then retry the original operation.",
                Steps =
                [
                    "Call ssh_connect_profile with the requested connection/profile name.",
                    "If the connection succeeds, retry the original SSH or SFTP operation.",
                    "If reconnect fails, inspect the returned connection error before choosing another transport."
                ],
                Tools = ["ssh_connect_profile"]
            };
        }

        return new SshRecoveryGuidance
        {
            Message = $"No active connection or saved profile matched '{connectionName}'. Establish an MCP SSH connection before retrying.",
            Steps =
            [
                "Call ssh_list_profiles to check for a differently named saved profile that matches the intended host or user.",
                "If a matching profile is found, call ssh_connect_profile with that profile name and retry the original operation.",
                "If no matching profile exists and host, username, and authentication are known from context, call ssh_connect.",
                "If required connection details are missing, ask the user for host, username, and authentication method instead of switching to another SSH mechanism.",
                "After a successful ssh_connect, call ssh_save_profile when this target should be reused."
            ],
            Tools = ["ssh_list_profiles", "ssh_connect_profile", "ssh_connect", "ssh_save_profile"],
            AskUserWhenMissing = ["host", "username", "privateKeyPath or password"]
        };
    }
}
