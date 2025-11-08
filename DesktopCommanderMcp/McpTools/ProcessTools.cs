using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DesktopCommander.Core.Common;
using DesktopCommander.Core.Services;
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
    [Description("List running processes with filtering. See process-management/SKILL.md")]
    public Task<string> ListProcesses(
        string? filter = null,
        int limit = 50)
    {
        try
        {
            var processes = Process.GetProcesses();
            
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
    [Description("Get detailed process information. See process-management/SKILL.md")]
    public Task<string> GetProcessInfo(
        int processId)
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
    [Description("Terminate process by ID. See process-management/SKILL.md")]
    public Task<string> KillProcess(
        int processId,
        bool force = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var processName = process.ProcessName;

            if (force)
            {
                process.Kill(entireProcessTree: true);
            }
            else
            {
                process.Kill();
            }

            var result = process.WaitForExit(5000);
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
    [Description("Terminate all processes by name. See process-management/SKILL.md")]
    public Task<string> KillProcessByName(
        string processName,
        string confirmation = "")
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

            var processes = Process.GetProcessesByName(processName);
            
            if (processes.Length == 0)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "No matching processes found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            var killedPids = new List<int>();

            foreach (var process in processes)
            {
                try
                {
                    var pid = process.Id;
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