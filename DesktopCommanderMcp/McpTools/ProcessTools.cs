using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for process management
/// </summary>
[McpServerToolType]
public class ProcessTools(
    ProcessManager processManager,
    AuditLogger auditLogger,
    ILogger<ProcessTools> logger)
{
    [McpServerTool, DisplayName("list_processes")]
    [Description("List running processes with optional filtering")]
    public Task<string> ListProcesses(
        [Description("Filter by process name (optional)")] string? filter = null,
        [Description("Maximum number of processes to return (default: 50)")] int limit = 50)
    {
        try
        {
            Process[] processes = Process.GetProcesses();
            
            if (!string.IsNullOrEmpty(filter))
            {
                processes = processes.Where(p => 
                    p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            var processList = processes
                .Take(limit)
                .Select(p => new
                {
                    processId = p.Id,
                    processName = p.ProcessName,
                    startTime = GetSafeStartTime(p),
                    workingSetMB = p.WorkingSet64 / 1024.0 / 1024.0,
                    threadCount = p.Threads.Count,
                    handleCount = p.HandleCount
                })
                .OrderByDescending(p => p.workingSetMB)
                .ToArray();

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                processCount = processList.Length,
                totalProcesses = processes.Length,
                filter,
                processes = processList
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing processes");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_process_info")]
    [Description("Get detailed information about a specific process")]
    public Task<string> GetProcessInfo(
        [Description("Process ID")] int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);

            var info = new
            {
                success = true,
                processId = process.Id,
                processName = process.ProcessName,
                startTime = GetSafeStartTime(process),
                totalCpuTime = process.TotalProcessorTime.ToString(),
                workingSetMB = process.WorkingSet64 / 1024.0 / 1024.0,
                peakWorkingSetMB = process.PeakWorkingSet64 / 1024.0 / 1024.0,
                privateMemoryMB = process.PrivateMemorySize64 / 1024.0 / 1024.0,
                virtualMemoryMB = process.VirtualMemorySize64 / 1024.0 / 1024.0,
                threadCount = process.Threads.Count,
                handleCount = process.HandleCount,
                mainWindowTitle = process.MainWindowTitle
            };

            return Task.FromResult(JsonSerializer.Serialize(info, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = "Process not found" }, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting process info for PID {ProcessId}", processId);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("kill_process")]
    [Description("Terminate a process by ID")]
    public Task<string> KillProcess(
        [Description("Process ID")] int processId,
        [Description("Force kill (kill entire process tree)")] bool force = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            string processName = process.ProcessName;

            if (force)
            {
                process.Kill(entireProcessTree: true);
            }
            else
            {
                process.Kill();
            }

            bool result = process.WaitForExit(5000);
            auditLogger.LogOperation("kill_process", $"PID:{processId} Name:{processName}", result);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                processId,
                processName,
                killed = true,
                force
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = "Process not found" }, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing process {ProcessId}", processId);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("kill_process_by_name")]
    [Description("Terminate all processes matching a name")]
    public Task<string> KillProcessByName(
        [Description("Process name (without .exe)")] string processName,
        [Description("Confirmation string (must match process name exactly)")] string confirmation = "")
    {
        try
        {
            if (confirmation != processName)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Confirmation required", 
                          message = $"Set confirmation parameter to '{processName}' to proceed" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            Process[] processes = Process.GetProcessesByName(processName);
            
            if (processes.Length == 0)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "No matching processes found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            var killedPids = new List<int>();

            foreach (Process process in processes)
            {
                try
                {
                    int pid = process.Id;
                    process.Kill(entireProcessTree: true);
                    killedPids.Add(pid);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kill process {ProcessId}", process.Id);
                }
            }

            auditLogger.LogOperation("kill_process_by_name", $"Name:{processName} Count:{killedPids.Count}", true);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                processName,
                killedCount = killedPids.Count,
                killedPids
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing processes by name: {ProcessName}", processName);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    private static string? GetSafeStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return null;
        }
    }
}