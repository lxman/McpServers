using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;
using System.Diagnostics;

namespace DesktopCommander.Controllers;

/// <summary>
/// Process management operations API
/// </summary>
[ApiController]
[Route("api/processes")]
public class ProcessController(
    ProcessManager processManager,
    ILogger<ProcessController> logger) : ControllerBase
{
    /// <summary>
    /// List all running processes
    /// </summary>
    [HttpGet("")]
    public IActionResult ListProcesses(
        [FromQuery] string? filter = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            Process[] processes = Process.GetProcesses();
            IEnumerable<Process> filteredProcesses = processes.AsEnumerable();

            if (!string.IsNullOrEmpty(filter))
            {
                filteredProcesses = processes.Where(p => 
                    p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            var sortedProcesses = filteredProcesses
                .OrderByDescending(GetSafeWorkingSet)
                .Take(limit)
                .Select(p => new
                {
                    processId = p.Id,
                    processName = p.ProcessName,
                    memoryMB = GetSafeWorkingSet(p) / (1024 * 1024),
                    cpuTime = GetSafeTotalProcessorTime(p),
                    threadCount = GetSafeThreadCount(p)
                })
                .ToList();

            return Ok(new { success = true, processes = sortedProcesses, count = sortedProcesses.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing processes");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed information about a specific process
    /// </summary>
    [HttpGet("{processId:int}")]
    public IActionResult GetProcessInfo(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var info = new
            {
                success = true,
                processId = process.Id,
                processName = process.ProcessName,
                startTime = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                totalCpuTime = process.TotalProcessorTime.ToString(),
                workingSetMB = process.WorkingSet64 / (1024 * 1024.0),
                peakWorkingSetMB = process.PeakWorkingSet64 / (1024 * 1024.0),
                privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024.0),
                virtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024.0),
                threadCount = process.Threads.Count,
                handleCount = process.HandleCount,
                mainWindowTitle = process.MainWindowTitle
            };
            return Ok(info);
        }
        catch (ArgumentException)
        {
            return NotFound(new { success = false, error = $"Process with ID {processId} not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting info for process: {ProcessId}", processId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Kill a process by process ID
    /// </summary>
    [HttpDelete("{processId:int}")]
    public IActionResult KillProcess(int processId, [FromQuery] bool force = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            string processName = process.ProcessName;

            // Basic safety check - don't kill critical system processes
            var criticalProcesses = new[] { "explorer", "winlogon", "csrss", "wininit", "services", "lsass", "System" };
            if (criticalProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, error = $"Refused to kill critical system process: {processName} (PID: {processId})" });
            }

            if (force)
            {
                process.Kill();
            }
            else
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000)) // Wait 5 seconds for graceful exit
                {
                    process.Kill(); // Force kill if graceful close failed
                }
            }

            return Ok(new { success = true, message = $"Process terminated: {processName} (PID: {processId})" });
        }
        catch (ArgumentException)
        {
            return NotFound(new { success = false, error = $"Process with ID {processId} not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing process: {ProcessId}", processId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Kill all processes with a specific name
    /// </summary>
    [HttpPost("kill-by-name")]
    public IActionResult KillProcessesByName([FromBody] KillProcessByNameRequest request)
    {
        try
        {
            if (request.Confirmation != "CONFIRM")
            {
                return BadRequest(new { success = false, error = "Confirmation must be 'CONFIRM'" });
            }

            // Safety check - don't kill critical system processes
            var criticalProcesses = new[] { "explorer", "winlogon", "csrss", "wininit", "services", "lsass", "System" };
            if (criticalProcesses.Contains(request.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, error = $"Refused to kill critical system process: {request.ProcessName}" });
            }

            Process[] processes = Process.GetProcessesByName(request.ProcessName);
            if (!processes.Any())
            {
                return NotFound(new { success = false, error = $"No processes found with name: {request.ProcessName}" });
            }

            var killedCount = 0;
            var errors = new List<string>();

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                    killedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"PID {process.Id}: {ex.Message}");
                }
            }

            var result = new
            {
                success = killedCount > 0,
                message = $"Terminated {killedCount} of {processes.Length} processes named '{request.ProcessName}'",
                killedCount,
                totalCount = processes.Length,
                errors = errors.Count > 0 ? errors : null
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing processes by name: {Name}", request.ProcessName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // Helper methods
    private static long GetSafeWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetSafeTotalProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime.ToString(@"hh\:mm\:ss");
        }
        catch
        {
            return "N/A";
        }
    }

    private static int GetSafeThreadCount(Process process)
    {
        try
        {
            return process.Threads.Count;
        }
        catch
        {
            return 0;
        }
    }
}

// Request models
public record KillProcessByNameRequest(string ProcessName, string Confirmation = "");
