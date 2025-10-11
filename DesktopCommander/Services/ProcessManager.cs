using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace DesktopCommander.Services;

public class ProcessManager(ILogger<ProcessManager> logger, AuditLogger auditLogger)
{
    private readonly ConcurrentDictionary<string, ManagedProcess> _processes = new();

    public async Task<ProcessResult> StartProcessAsync(string command, string sessionId = "default", 
        int timeoutMs = 30000, string? workingDirectory = null)
    {
        try
        {
            ProcessStartInfo processInfo = CreateProcessStartInfo(command, workingDirectory);
            var process = new Process { StartInfo = processInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var outputCompleted = new TaskCompletionSource<bool>();
            var errorCompleted = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
                else
                    outputCompleted.SetResult(true);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
                else
                    errorCompleted.SetResult(true);
            };

            var managedProcess = new ManagedProcess
            {
                Process = process,
                SessionId = sessionId,
                Command = command,
                StartTime = DateTime.UtcNow,
                OutputBuilder = outputBuilder,
                ErrorBuilder = errorBuilder
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _processes.TryAdd(sessionId, managedProcess);

            Task processTask = process.WaitForExitAsync();
            Task completedTask = await Task.WhenAny(processTask, Task.Delay(timeoutMs));

            if (completedTask == processTask)
            {
                // Process completed within timeout
                await Task.WhenAll(outputCompleted.Task, errorCompleted.Task);
                _processes.TryRemove(sessionId, out _);
                
                var result = new ProcessResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ProcessId = process.Id,
                    IsTimeout = false
                };

                auditLogger.LogCommandExecution(command, result.Success, result.Error);
                return result;
            }
            else
            {
                // Timeout - keep process running for interactive use
                var result = new ProcessResult
                {
                    Success = true,
                    ExitCode = null,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ProcessId = process.Id,
                    IsTimeout = true,
                    IsRunning = !process.HasExited
                };

                auditLogger.LogCommandExecution(command, true, "Process running (timeout)");
                return result;
            }
        }
        catch (Exception ex)
        {
            auditLogger.LogCommandExecution(command, false, ex.Message);
            return new ProcessResult
            {
                Success = false,
                Error = ex.Message,
                Output = string.Empty
            };
        }
    }

    public ProcessResult? GetProcessOutput(string sessionId)
    {
        if (_processes.TryGetValue(sessionId, out ManagedProcess? managedProcess))
        {
            return new ProcessResult
            {
                Success = !managedProcess.Process.HasExited,
                Output = managedProcess.OutputBuilder.ToString(),
                Error = managedProcess.ErrorBuilder.ToString(),
                ProcessId = managedProcess.Process.Id,
                IsRunning = !managedProcess.Process.HasExited,
                ExitCode = managedProcess.Process.HasExited ? managedProcess.Process.ExitCode : null
            };
        }
        return null;
    }

    public async Task<bool> SendInputAsync(string sessionId, string input)
    {
        if (!_processes.TryGetValue(sessionId, out ManagedProcess? managedProcess) ||
            managedProcess.Process.HasExited) return false;
        try
        {
            await managedProcess.Process.StandardInput.WriteLineAsync(input);
            await managedProcess.Process.StandardInput.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send input to process {SessionId}", sessionId);
            return false;
        }
    }

    public bool KillProcess(string sessionId)
    {
        if (!_processes.TryGetValue(sessionId, out ManagedProcess? managedProcess)) return false;
        try
        {
            if (!managedProcess.Process.HasExited)
            {
                managedProcess.Process.Kill(true);
            }
            _processes.TryRemove(sessionId, out _);
            auditLogger.LogProcessOperation("Kill", managedProcess.Process.Id, true);
            return true;
        }
        catch (Exception ex)
        {
            auditLogger.LogProcessOperation("Kill", managedProcess.Process.Id, false, ex.Message);
            return false;
        }
    }

    public List<ProcessInfo> ListActiveSessions()
    {
        return _processes.Values.Select(mp => new ProcessInfo
        {
            SessionId = mp.SessionId,
            ProcessId = mp.Process.Id,
            Command = mp.Command,
            StartTime = mp.StartTime,
            IsRunning = !mp.Process.HasExited,
            ExitCode = mp.Process.HasExited ? mp.Process.ExitCode : null
        }).ToList();
    }

    private static ProcessStartInfo CreateProcessStartInfo(string command, string? workingDirectory)
    {
        bool isWindows = OperatingSystem.IsWindows();
        
        return new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            Arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };
    }

    private class ManagedProcess
    {
        public Process Process { get; init; } = null!;
        public string SessionId { get; init; } = string.Empty;
        public string Command { get; init; } = string.Empty;
        public DateTime StartTime { get; init; }
        public StringBuilder OutputBuilder { get; init; } = null!;
        public StringBuilder ErrorBuilder { get; init; } = null!;
    }
}

public class ProcessResult
{
    public bool Success { get; set; }
    public int? ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public bool IsTimeout { get; set; }
    public bool IsRunning { get; set; }
}

public class ProcessInfo
{
    public string SessionId { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string Command { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool IsRunning { get; set; }
    public int? ExitCode { get; set; }
}
