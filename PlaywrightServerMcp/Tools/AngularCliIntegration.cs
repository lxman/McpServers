using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Playwright.Core.Services;
using PlaywrightServerMcp.Models;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Handles Angular CLI integration for executing Angular commands and capturing output
/// </summary>
[McpServerToolType]
public class AngularCliIntegration(PlaywrightSessionManager sessionManager)
{
    private readonly PlaywrightSessionManager _sessionManager = sessionManager;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Commands that can be executed without requiring an Angular project
    /// </summary>
    private static readonly string[] GlobalCommands = ["new", "version", "help", "--version", "completion", "analytics"
    ];

    /// <summary>
    /// Determines if a command requires Angular project validation
    /// </summary>
    private static bool ShouldValidateAngularProject(string command)
    {
        var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // If command starts with "ng", look at the second argument, otherwise look at the first
        var angularCommand = args.Length > 1 && args[0].ToLower() == "ng" ? args[1].ToLower() : args.FirstOrDefault()?.ToLower();
        return !GlobalCommands.Contains(angularCommand ?? string.Empty);
    }

    [McpServerTool]
    [Description("Execute Angular CLI commands and capture their output with comprehensive error handling. See skills/playwright-mcp/tools/angular/cli-integration.md.")]
    public async Task<string> ExecuteNgCommands(
        string command,
        string workingDirectory = "",
        int timeoutSeconds = 120,
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new CliCommandResult
                {
                    Success = false,
                    Command = command,
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            var config = new CliExecutionConfig
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                TimeoutSeconds = timeoutSeconds,
                CaptureOutput = true,
                ValidateAngularProject = ShouldValidateAngularProject(command) 
            };

            var result = await ExecuteAngularCliCommand(command, config);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new CliCommandResult
            {
                Success = false,
                Command = command,
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to execute Angular CLI command: {ex.Message}",
                ExitCode = -1
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    [McpServerTool]
    [Description("Check if Angular CLI is installed and get version information. See skills/playwright-mcp/tools/angular/cli-integration.md.")]
    public async Task<string> CheckAngularCliStatus(
        string workingDirectory = "",
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new
                {
                    AngularCliInstalled = false,
                    ErrorMessage = $"Session {sessionId} not found",
                    WorkingDirectory = workingDirectory
                }, JsonOptions);
            }

            var config = new CliExecutionConfig
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                TimeoutSeconds = 30,
                ValidateAngularProject = false
            };

            var versionResult = await ExecuteAngularCliCommand("ng version", config);
            var helpResult = await ExecuteAngularCliCommand("ng help", config);

            var status = new
            {
                AngularCliInstalled = versionResult.Success,
                versionResult.AngularCliVersion,
                config.WorkingDirectory,
                IsAngularProject = await IsAngularProject(config.WorkingDirectory),
                AvailableCommands = ExtractAvailableCommands(helpResult.StandardOutput),
                ProjectInfo = await GetAngularProjectInfo(config.WorkingDirectory),
                SystemInfo = new
                {
                    NodeVersion = await GetNodeVersion(),
                    NpmVersion = await GetNpmVersion(),
                    OperatingSystem = Environment.OSVersion.ToString(),
                    WorkingDirectoryExists = Directory.Exists(config.WorkingDirectory)
                }
            };

            return JsonSerializer.Serialize(status, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorStatus = new
            {
                AngularCliInstalled = false,
                ErrorMessage = $"Failed to check Angular CLI status: {ex.Message}",
                WorkingDirectory = workingDirectory
            };
            
            return JsonSerializer.Serialize(errorStatus, JsonOptions);
        }
    }

    [McpServerTool]
    [Description("Generate Angular components, services, or other artifacts using Angular CLI. See skills/playwright-mcp/tools/angular/cli-integration.md.")]
    public async Task<string> GenerateAngularArtifact(
        string artifactType,
        string artifactName,
        string options = "",
        string workingDirectory = "",
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new CliCommandResult
                {
                    Success = false,
                    Command = $"ng generate {artifactType} {artifactName}",
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            var config = new CliExecutionConfig
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                TimeoutSeconds = 180,
                ValidateAngularProject = true
            };

            // Validate that this is an Angular project
            if (!await IsAngularProject(config.WorkingDirectory))
            {
                return JsonSerializer.Serialize(new CliCommandResult
                {
                    Success = false,
                    Command = $"ng generate {artifactType} {artifactName}",
                    WorkingDirectory = config.WorkingDirectory,
                    ErrorMessage = "Not an Angular project - angular.json not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            // Capture files before generation
            var filesBefore = await GetProjectFiles(config.WorkingDirectory);

            // Build the command
            var command = $"ng generate {artifactType} {artifactName}";
            if (!string.IsNullOrWhiteSpace(options))
            {
                command += $" {options}";
            }

            var result = await ExecuteAngularCliCommand(command, config);

            // Capture files after generation and determine what was generated/modified
            if (result.Success)
            {
                var filesAfter = await GetProjectFiles(config.WorkingDirectory);
                result.GeneratedFiles = filesAfter.Except(filesBefore).ToList();
                result.ModifiedFiles = await GetModifiedFiles(filesBefore, filesAfter, config.WorkingDirectory);
            }

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new CliCommandResult
            {
                Success = false,
                Command = $"ng generate {artifactType} {artifactName}",
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to generate Angular artifact: {ex.Message}",
                ExitCode = -1
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    [McpServerTool]
    [Description("Build Angular project with specified configuration. See skills/playwright-mcp/tools/angular/cli-integration.md.")]
    public async Task<string> BuildAngularProject(
        string configuration = "development",
        string options = "",
        string workingDirectory = "",
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new CliCommandResult
                {
                    Success = false,
                    Command = $"ng build --configuration={configuration}",
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            var config = new CliExecutionConfig
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                TimeoutSeconds = 600, // Longer timeout for builds
                ValidateAngularProject = true
            };

            // Validate Angular project
            if (!await IsAngularProject(config.WorkingDirectory))
            {
                return JsonSerializer.Serialize(new CliCommandResult
                {
                    Success = false,
                    Command = $"ng build --configuration={configuration}",
                    WorkingDirectory = config.WorkingDirectory,
                    ErrorMessage = "Not an Angular project - angular.json not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            // Build the command
            var command = $"ng build --configuration={configuration}";
            if (!string.IsNullOrWhiteSpace(options))
            {
                command += $" {options}";
            }

            var result = await ExecuteAngularCliCommand(command, config);

            // Add build-specific analysis
            if (result.Success)
            {
                result.GeneratedFiles = await GetBuildOutputFiles(config.WorkingDirectory, configuration);
                
                // Try to extract build statistics from output
                var buildStats = ExtractBuildStatistics(result.StandardOutput);
                if (buildStats.Count > 0)
                {
                    result.StandardOutput += $"\n\nExtracted Build Statistics:\n{JsonSerializer.Serialize(buildStats, JsonOptions)}";
                }
            }

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new CliCommandResult
            {
                Success = false,
                Command = $"ng build --configuration={configuration}",
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to build Angular project: {ex.Message}",
                ExitCode = -1
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    /// <summary>
    /// Core method to execute Angular CLI commands with comprehensive error handling
    /// </summary>
    private async Task<CliCommandResult> ExecuteAngularCliCommand(string command, CliExecutionConfig config)
    {
        var result = new CliCommandResult
        {
            Command = command,
            WorkingDirectory = config.WorkingDirectory
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate working directory exists
            if (!Directory.Exists(config.WorkingDirectory))
            {
                result.ErrorMessage = $"Working directory does not exist: {config.WorkingDirectory}";
                result.ExitCode = -1;
                return result;
            }

            // Validate Angular project if required
            if (config.ValidateAngularProject && !await IsAngularProject(config.WorkingDirectory))
            {
                result.ErrorMessage = "Not an Angular project - angular.json not found";
                result.ExitCode = -1;
                return result;
            }

            // Prepare process start info
            var processStartInfo = new ProcessStartInfo
            {
                FileName = GetNgExecutablePath(),
                Arguments = command.StartsWith("ng ") ? command.Substring(3) : command,
                WorkingDirectory = config.WorkingDirectory,
                RedirectStandardOutput = config.CaptureOutput,
                RedirectStandardError = config.CaptureOutput,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Add environment variables
            foreach (var envVar in config.EnvironmentVariables)
            {
                processStartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
            }

            // Execute the process
            using var process = new Process { StartInfo = processStartInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            if (config.CaptureOutput)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };
            }

            process.Start();

            if (config.CaptureOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            // Wait for completion with timeout
            var completed = await Task.Run(() => process.WaitForExit(config.TimeoutSeconds * 1000));

            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;

            if (!completed)
            {
                process.Kill();
                result.ErrorMessage = $"Command timed out after {config.TimeoutSeconds} seconds";
                result.ExitCode = -1;
                return result;
            }

            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            result.StandardOutput = outputBuilder.ToString();
            result.StandardError = errorBuilder.ToString();

            // Try to extract Angular CLI version from output
            result.AngularCliVersion = ExtractAngularCliVersion(result.StandardOutput);
            result.AngularCliDetected = !string.IsNullOrEmpty(result.AngularCliVersion);

            // If command failed, include error details
            if (!result.Success)
            {
                result.ErrorMessage = $"Command failed with exit code {result.ExitCode}";
                if (!string.IsNullOrEmpty(result.StandardError))
                {
                    result.ErrorMessage += $"\nError details: {result.StandardError}";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
            result.Success = false;
            result.ErrorMessage = $"Exception occurred: {ex.Message}";
            result.ExitCode = -1;
            return result;
        }
    }

    /// <summary>
    /// Helper methods for Angular CLI integration
    /// </summary>
    private static string GetNgExecutablePath()
    {
        // Try to find ng executable in various locations
        var possiblePaths = new[]
        {
            "ng",
            "ng.cmd",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "ng"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "ng.cmd"),
            "/usr/local/bin/ng",
            "/usr/bin/ng"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path) || IsExecutableInPath(path))
            {
                return path;
            }
        }

        return "ng"; // Default fallback
    }

    private static bool IsExecutableInPath(string executable)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> IsAngularProject(string directory)
    {
        var angularJsonPath = Path.Combine(directory, "angular.json");
        return Task.FromResult(File.Exists(angularJsonPath));
    }

    private static async Task<object> GetAngularProjectInfo(string directory)
    {
        try
        {
            var angularJsonPath = Path.Combine(directory, "angular.json");
            if (!File.Exists(angularJsonPath))
            {
                return new { IsAngularProject = false };
            }

            var angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
            var angularConfig = JsonSerializer.Deserialize<JsonElement>(angularJsonContent);

            return new
            {
                IsAngularProject = true,
                Version = angularConfig.TryGetProperty("version", out var version) ? version.GetInt32() : 0,
                DefaultProject = angularConfig.TryGetProperty("defaultProject", out var defaultProject) ? defaultProject.GetString() : null,
                ProjectCount = angularConfig.TryGetProperty("projects", out var projects) ? projects.EnumerateObject().Count() : 0,
                AngularJsonExists = true
            };
        }
        catch
        {
            return new { IsAngularProject = false, Error = "Failed to parse angular.json" };
        }
    }

    private static async Task<string> GetNodeVersion()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
            
            return process.ExitCode == 0 ? output.Trim() : "Not installed";
        }
        catch
        {
            return "Not available";
        }
    }

    private static async Task<string> GetNpmVersion()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
            
            return process.ExitCode == 0 ? output.Trim() : "Not installed";
        }
        catch
        {
            return "Not available";
        }
    }

    private static string ExtractAngularCliVersion(string output)
    {
        // Look for Angular CLI version in output
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("Angular CLI:") || line.Contains("@angular/cli"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (Regex.IsMatch(part, @"\d+\.\d+\.\d+"))
                    {
                        return part;
                    }
                }
            }
        }
        return string.Empty;
    }

    private static List<string> ExtractAvailableCommands(string helpOutput)
    {
        var commands = new List<string>();
        var lines = helpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var inCommandsSection = false;
        foreach (var line in lines)
        {
            if (line.Contains("Available Commands:") || line.Contains("Commands:"))
            {
                inCommandsSection = true;
                continue;
            }

            if (inCommandsSection && line.Trim().StartsWith("ng "))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    commands.Add(parts[1]); // The command name after 'ng'
                }
            }
        }

        return commands;
    }

    private static async Task<List<string>> GetProjectFiles(string directory)
    {
        try
        {
            return await Task.Run(() => 
                Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("node_modules") && !f.Contains(".git") && !f.Contains("dist"))
                    .ToList());
        }
        catch
        {
            return [];
        }
    }

    private static async Task<List<string>> GetModifiedFiles(List<string> filesBefore, List<string> filesAfter, string workingDirectory)
    {
        return await Task.Run(() =>
        {
            var modified = new List<string>();
            
            // Check for files that existed before and might have been modified
            foreach (var file in filesBefore.Intersect(filesAfter))
            {
                // This is a simplified check - in a real implementation, you might want to check file timestamps or content hashes
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime > DateTime.Now.AddMinutes(-5)) // Modified in last 5 minutes
                    {
                        modified.Add(file);
                    }
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }

            return modified;
        });
    }

    private static async Task<List<string>> GetBuildOutputFiles(string workingDirectory, string configuration)
    {
        return await Task.Run(() =>
        {
            try
            {
                var distPath = Path.Combine(workingDirectory, "dist");
                if (Directory.Exists(distPath))
                {
                    return Directory.GetFiles(distPath, "*", SearchOption.AllDirectories).ToList();
                }
            }
            catch
            {
                // Ignore errors
            }

            return [];
        });
    }

    private static Dictionary<string, object> ExtractBuildStatistics(string buildOutput)
    {
        var stats = new Dictionary<string, object>();
        
        try
        {
            var lines = buildOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Look for bundle size information
                if (line.Contains("main.") && (line.Contains("kB") || line.Contains("MB")))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    stats["mainBundleSize"] = string.Join(" ", parts.Where(p => p.Contains("kB") || p.Contains("MB")));
                }
                
                // Look for warnings and errors
                if (line.Contains("Warning"))
                {
                    if (!stats.ContainsKey("warnings"))
                        stats["warnings"] = new List<string>();
                    ((List<string>)stats["warnings"]).Add(line.Trim());
                }
                
                if (line.Contains("Error"))
                {
                    if (!stats.ContainsKey("errors"))
                        stats["errors"] = new List<string>();
                    ((List<string>)stats["errors"]).Add(line.Trim());
                }

                // Look for build time
                if (line.Contains("Build at:"))
                {
                    stats["buildTime"] = line.Substring(line.IndexOf("Build at:") + 9).Trim();
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return stats;
    }
}
