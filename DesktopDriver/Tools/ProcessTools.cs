using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class ProcessTools(AuditLogger auditLogger)
{
    [McpServerTool]
    [Description("List all running processes on the system")]
    public string ListProcesses(
        [Description("Filter processes by name (optional)")] string? filter = null,
        [Description("Maximum number of processes to return")] int limit = 50)
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

            List<Process> sortedProcesses = filteredProcesses
                .OrderByDescending(p => GetSafeWorkingSet(p))
                .Take(limit)
                .ToList();

            var result = new StringBuilder();
            result.AppendLine($"Running Processes (Top {sortedProcesses.Count}):\n");
            result.AppendLine($"{"PID",-8} {"Name",-25} {"Memory (MB)",-12} {"CPU Time",-15} {"Threads",-8}");
            result.AppendLine(new string('-', 75));

            foreach (Process process in sortedProcesses)
            {
                try
                {
                    long memoryMB = GetSafeWorkingSet(process) / (1024 * 1024);
                    string cpuTime = GetSafeTotalProcessorTime(process);
                    int threadCount = GetSafeThreadCount(process);

                    result.AppendLine($"{process.Id,-8} {process.ProcessName,-25} {memoryMB,-12:F1} {cpuTime,-15} {threadCount,-8}");
                }
                catch
                {
                    // Skip processes we can't access
                    result.AppendLine($"{process.Id,-8} {process.ProcessName,-25} {"N/A",-12} {"N/A",-15} {"N/A",-8}");
                }
            }

            auditLogger.LogOperation("List_Processes", $"Filter: {filter ?? "none"}, Count: {sortedProcesses.Count}", true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogOperation("List_Processes", $"Filter: {filter ?? "none"}", false, ex.Message);
            return $"Error listing processes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get detailed information about a specific process")]
    public string GetProcessInfo(
        [Description("Process ID to examine")] int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var result = new StringBuilder();
            
            result.AppendLine($"Process Information (PID: {processId}):\n");
            result.AppendLine($"Name: {process.ProcessName}");
            result.AppendLine($"ID: {process.Id}");
            
            try
            {
                result.AppendLine($"Start Time: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
                result.AppendLine($"Total CPU Time: {process.TotalProcessorTime}");
                result.AppendLine($"Working Set: {process.WorkingSet64 / (1024 * 1024):F1} MB");
                result.AppendLine($"Peak Working Set: {process.PeakWorkingSet64 / (1024 * 1024):F1} MB");
                result.AppendLine($"Private Memory: {process.PrivateMemorySize64 / (1024 * 1024):F1} MB");
                result.AppendLine($"Virtual Memory: {process.VirtualMemorySize64 / (1024 * 1024):F1} MB");
                result.AppendLine($"Threads: {process.Threads.Count}");
                result.AppendLine($"Handles: {process.HandleCount}");
                
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    result.AppendLine($"Main Window: {process.MainWindowTitle}");
                }

                try
                {
                    result.AppendLine($"File Name: {process.MainModule?.FileName ?? "N/A"}");
                }
                catch
                {
                    result.AppendLine("File Name: Access denied");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"Some details unavailable: {ex.Message}");
            }

            auditLogger.LogProcessOperation("Get_Info", processId, true);
            return result.ToString();
        }
        catch (ArgumentException)
        {
            var error = $"Process with ID {processId} not found";
            auditLogger.LogProcessOperation("Get_Info", processId, false, error);
            return error;
        }
        catch (Exception ex)
        {
            auditLogger.LogProcessOperation("Get_Info", processId, false, ex.Message);
            return $"Error getting process info: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Kill a process by process ID (use with caution)")]
    public string KillProcessById(
        [Description("Process ID to terminate")] int processId,
        [Description("Force kill (terminates immediately)")] bool force = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            string processName = process.ProcessName;

            // Basic safety check - don't kill critical system processes
            var criticalProcesses = new[] { "explorer", "winlogon", "csrss", "wininit", "services", "lsass", "System" };
            if (criticalProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                var error = $"Refused to kill critical system process: {processName} (PID: {processId})";
                auditLogger.LogProcessOperation("Kill", processId, false, error);
                return error;
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

            auditLogger.LogProcessOperation("Kill", processId, true);
            return $"Process terminated: {processName} (PID: {processId})";
        }
        catch (ArgumentException)
        {
            var error = $"Process with ID {processId} not found";
            auditLogger.LogProcessOperation("Kill", processId, false, error);
            return error;
        }
        catch (Exception ex)
        {
            auditLogger.LogProcessOperation("Kill", processId, false, ex.Message);
            return $"Error killing process: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Kill all processes with a specific name (use with extreme caution)")]
    public string KillProcessesByName(
        [Description("Process name to terminate")] string processName,
        [Description("Confirm termination by typing 'CONFIRM'")] string confirmation = "")
    {
        if (confirmation != "CONFIRM")
        {
            return "This operation requires confirmation. Set confirmation parameter to 'CONFIRM' to proceed.";
        }

        try
        {
            // Safety check - don't kill critical system processes
            var criticalProcesses = new[] { "explorer", "winlogon", "csrss", "wininit", "services", "lsass", "System" };
            if (criticalProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                var error = $"Refused to kill critical system process: {processName}";
                auditLogger.LogOperation("Kill_By_Name", processName, false, error);
                return error;
            }

            Process[] processes = Process.GetProcessesByName(processName);
            if (!processes.Any())
            {
                auditLogger.LogOperation("Kill_By_Name", processName, false, "No processes found");
                return $"No processes found with name: {processName}";
            }

            var killedCount = 0;
            var errors = new List<string>();

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                    killedCount++;
                    auditLogger.LogProcessOperation("Kill", process.Id, true);
                }
                catch (Exception ex)
                {
                    errors.Add($"PID {process.Id}: {ex.Message}");
                    auditLogger.LogProcessOperation("Kill", process.Id, false, ex.Message);
                }
            }

            var result = $"Terminated {killedCount} of {processes.Length} processes named '{processName}'";
            if (errors.Count != 0)
            {
                result += $"\nErrors:\n{string.Join("\n", errors)}";
            }

            auditLogger.LogOperation("Kill_By_Name", processName, killedCount > 0);
            return result;
        }
        catch (Exception ex)
        {
            auditLogger.LogOperation("Kill_By_Name", processName, false, ex.Message);
            return $"Error killing processes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get system performance information")]
    public string GetSystemInfo()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("System Information:\n");
            
            result.AppendLine($"Machine Name: {Environment.MachineName}");
            result.AppendLine($"OS Version: {Environment.OSVersion}");
            result.AppendLine($"Platform: {Environment.OSVersion.Platform}");
            result.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            result.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
            result.AppendLine($"Processor Count: {Environment.ProcessorCount}");
            result.AppendLine($"System Directory: {Environment.SystemDirectory}");
            result.AppendLine($"Working Set: {Environment.WorkingSet / (1024 * 1024):F1} MB");
            result.AppendLine($"Current User: {Environment.UserName}");
            result.AppendLine($"User Domain: {Environment.UserDomainName}");
            result.AppendLine($"System Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64)}");

            // Get process counts
            int totalProcesses = Process.GetProcesses().Length;
            result.AppendLine($"Total Processes: {totalProcesses}");

            auditLogger.LogOperation("Get_System_Info", "System information retrieved", true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            auditLogger.LogOperation("Get_System_Info", "System information retrieval", false, ex.Message);
            return $"Error getting system info: {ex.Message}";
        }
    }

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
