using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DesktopCommander.Core.Common;
using DesktopCommander.Core.Models;
using DesktopCommander.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for managing the lifecycle of MCP services
/// </summary>
[McpServerToolType]
public class ServiceManagementTools(
    ServerRegistry registry,
    IMemoryCache cache,
    ILogger<ServiceManagementTools> logger)
{
    private const string CACHE_KEY_PREFIX = "ManagedService_";

    [McpServerTool, DisplayName("start_service")]
    [Description("Start MCP service. See service-management/SKILL.md")]
    public async Task<string> StartService(
        string serviceName,
        bool forceInit = false,
        string? workingDirectory = null)
    {
        try
        {
            ServerInfo? serverInfo = registry.GetServer(serviceName);
            if (serverInfo == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = $"Service '{serviceName}' not found in registry" 
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check if the service is already running in the managed service cache
            ManagedService? existingService = FindRunningService(serviceName);
            if (existingService != null)
            {
                try
                {
                    // Verify the process is still running
                    var existingProcess = Process.GetProcessById(existingService.ProcessId);
                    if (!existingProcess.HasExited)
                    {
                        TimeSpan uptime = DateTime.UtcNow - existingService.StartedAt;
                        logger.LogInformation(
                            "Service {ServiceName} is already running with ID {ServiceId}", 
                            serviceName, 
                            existingService.ServiceId);

                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            serviceId = existingService.ServiceId,
                            serviceName = existingService.ServiceName,
                            processId = existingService.ProcessId,
                            port = existingService.Port,
                            url = serverInfo.Url,
                            startedAt = existingService.StartedAt,
                            uptime = uptime.ToString(@"hh\:mm\:ss"),
                            message = "Service was already running"
                        }, SerializerOptions.JsonOptionsIndented);
                    }
                }
                catch (ArgumentException)
                {
                    // Process no longer exists, clean up the cache entry
                    cache.Remove(CACHE_KEY_PREFIX + existingService.ServiceId);
                    logger.LogInformation(
                        "Cleaned up stale cache entry for service {ServiceName}", 
                        serviceName);
                }
            }

            // Check if the port is already in use
            if (serverInfo.Port.HasValue && IsPortInUse(serverInfo.Port.Value))
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = $"Port {serverInfo.Port} is already in use. Service may already be running." 
                }, SerializerOptions.JsonOptionsIndented);
            }

            string workDir = workingDirectory ?? serverInfo.ProjectPath ?? AppContext.BaseDirectory;

            // Run initialization commands if needed
            if (serverInfo.RequiresInit)
            {
                bool needsInit = forceInit;

                switch (needsInit)
                {
                    // Check if initialization already ran
                    case false when !string.IsNullOrEmpty(serverInfo.InitCheckPath):
                    {
                        string checkPath = Path.Combine(workDir, serverInfo.InitCheckPath);
                        needsInit = !File.Exists(checkPath) && !Directory.Exists(checkPath);
                        break;
                    }
                    case false when serverInfo.InitCommands?.Count > 0:
                        // If no check path, assume we need to init on the first start
                        needsInit = true;
                        break;
                }

                if (needsInit && serverInfo.InitCommands?.Count > 0)
                {
                    logger.LogInformation("Running initialization for {ServiceName}", serviceName);
                    
                    foreach (string initCommand in serverInfo.InitCommands)
                    {
                        (bool Success, string? Error) initResult = await ExecuteCommand(initCommand, workDir);
                        if (!initResult.Success)
                        {
                            return JsonSerializer.Serialize(new { 
                                success = false, 
                                error = $"Initialization failed at command: {initCommand}",
                                details = initResult.Error
                            }, SerializerOptions.JsonOptionsIndented);
                        }
                        logger.LogInformation("Init step completed: {Command}", initCommand);
                    }
                }
            }

            // Parse and execute the start command
            (string executable, string arguments) = ParseCommand(serverInfo.StartCommand ?? "dotnet run");

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process? newProcess = Process.Start(startInfo);
            if (newProcess == null)
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = "Failed to start process" 
                }, SerializerOptions.JsonOptionsIndented);
            }

            var serviceId = Guid.NewGuid().ToString();
            var managedService = new ManagedService
            {
                ServiceId = serviceId,
                ServiceName = serviceName,
                ProcessId = newProcess.Id,
                Port = serverInfo.Port,
                StartedAt = DateTime.UtcNow
            };

            // Cache with 24-hour expiration
            cache.Set(CACHE_KEY_PREFIX + serviceId, managedService, TimeSpan.FromHours(24));

            // Wait a bit to see if it crashes immediately
            await Task.Delay(1500);
            
            if (newProcess.HasExited)
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = $"Process exited immediately with code {newProcess.ExitCode}" 
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Started service {ServiceName} with ID {ServiceId}", serviceName, serviceId);

            // Perform health check if URL is available

            if (string.IsNullOrEmpty(serverInfo.Url))
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    serviceId,
                    serviceName,
                    processId = newProcess.Id,
                    port = serverInfo.Port,
                    url = serverInfo.Url,
                    startedAt = managedService.StartedAt
                }, SerializerOptions.JsonOptionsIndented);
            logger.LogInformation("Performing health check for {ServiceName} at {Url}", serviceName, serverInfo.Url);
            (bool isHealthy, string healthCheckMessage) = await WaitForServiceHealthy(serverInfo.Url, newProcess, timeoutSeconds: 30);
                
            if (isHealthy)
            {
                logger.LogInformation("Service {ServiceName} is healthy and ready", serviceName);
            }
            else
            {
                logger.LogWarning("Service {ServiceName} started but health check failed: {Message}", 
                    serviceName, healthCheckMessage);
            }


            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceId,
                serviceName,
                processId = newProcess.Id,
                port = serverInfo.Port,
                url = serverInfo.Url,
                startedAt = managedService.StartedAt
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start service {ServiceName}", serviceName);
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = ex.Message 
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("stop_service")]
    [Description("Stop MCP service. See service-management/SKILL.md")]
    public string StopService(
        string serviceId)
    {
        string cacheKey = CACHE_KEY_PREFIX + serviceId;

        if (!cache.TryGetValue(cacheKey, out ManagedService? service) || service == null)
        {
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = $"Service {serviceId} not found or already stopped" 
            }, SerializerOptions.JsonOptionsIndented);
        }

        try
        {
            var process = Process.GetProcessById(service.ProcessId);
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            cache.Remove(cacheKey);

            TimeSpan uptime = DateTime.UtcNow - service.StartedAt;
            logger.LogInformation("Stopped service {ServiceName} (ID: {ServiceId})", service.ServiceName, serviceId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceId,
                serviceName = service.ServiceName,
                uptime = uptime.ToString(@"hh\:mm\:ss")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (ArgumentException)
        {
            cache.Remove(cacheKey);
            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceId,
                serviceName = service.ServiceName,
                message = "Process already stopped"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop service {ServiceId}", serviceId);
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = ex.Message 
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("service_status")]
    [Description("Get service status and metrics. See service-management/SKILL.md")]
    public string GetServiceStatus(
        string serviceId)
    {
        string cacheKey = CACHE_KEY_PREFIX + serviceId;

        if (!cache.TryGetValue(cacheKey, out ManagedService? service) || service == null)
        {
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = "Service not found" 
            }, SerializerOptions.JsonOptionsIndented);
        }

        try
        {
            var process = Process.GetProcessById(service.ProcessId);
            TimeSpan uptime = DateTime.UtcNow - service.StartedAt;

            return JsonSerializer.Serialize(new
            {
                success = true,
                serviceId,
                serviceName = service.ServiceName,
                processId = service.ProcessId,
                port = service.Port,
                status = "running",
                uptime = uptime.ToString(@"hh\:mm\:ss"),
                memoryMB = process.WorkingSet64 / 1024.0 / 1024.0,
                threadCount = process.Threads.Count
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (ArgumentException)
        {
            cache.Remove(cacheKey);
            return JsonSerializer.Serialize(new
            {
                success = false,
                serviceId,
                serviceName = service.ServiceName,
                status = "stopped"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting service status for {ServiceId}", serviceId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }


    [McpServerTool, DisplayName("reload_server_registry")]
    [Description("Reload server configuration. See service-management/SKILL.md")]
    public string ReloadServerRegistry()
    {
        try
        {
            registry.Reload();
            ServerConfiguration config = registry.GetConfiguration();
            
            logger.LogInformation("Server registry reloaded successfully");
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Server configuration reloaded successfully",
                serverCount = config.Servers.Count,
                servers = config.Servers.Keys.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload server registry");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private static async Task<(bool Success, string? Error)> ExecuteCommand(string command, string workingDirectory)
    {
        try
        {
            (string executable, string arguments) = ParseCommand(command);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start process");
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                return (false, $"Command failed with exit code {process.ExitCode}: {error}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = $"-ano | findstr :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null) return false;

            string output = process.StandardOutput.ReadToEnd();
            return output.Contains("LISTENING");
        }
        catch
        {
            return false;
        }
    }

    private static (string executable, string arguments) ParseCommand(string command)
    {
        string[] parts = command.Split(' ', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }

    /// <summary>
    /// Wait for service to become healthy by polling the health endpoint
    /// </summary>
    private async Task<(bool isHealthy, string message)> WaitForServiceHealthy(
        string serviceUrl, 
        Process process, 
        int timeoutSeconds = 30)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        // Skip TLS certificate validation for localhost development
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        using var httpsClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        HttpClient client = serviceUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? httpsClient : httpClient;
        
        int maxAttempts = timeoutSeconds * 2; // Poll every 500ms
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            // Check if process is still running
            if (process.HasExited)
            {
                return (false, $"Process exited with code {process.ExitCode}");
            }
            
            try
            {
                // Try to connect to the service
                HttpResponseMessage response = await client.GetAsync(serviceUrl);
                
                // Any response (even 404, 500, etc.) means the server is listening
                logger.LogDebug("Health check attempt {Attempt}: HTTP {StatusCode}", 
                    attempts + 1, (int)response.StatusCode);
                    
                return (true, $"Service responding with HTTP {(int)response.StatusCode}");
            }
            catch (HttpRequestException)
            {
                // Connection refused or other HTTP error - service not ready yet
                logger.LogDebug("Health check attempt {Attempt}: Connection refused", attempts + 1);
            }
            catch (TaskCanceledException)
            {
                // Timeout - service not responding yet
                logger.LogDebug("Health check attempt {Attempt}: Timeout", attempts + 1);
            }
            
            attempts++;
            await Task.Delay(500); // Wait 500ms between attempts
        }
        
        return (false, $"Service did not become healthy after {timeoutSeconds} seconds ({attempts} attempts)");
    }

    /// <summary>
    /// Find a running service by name in the cache
    /// </summary>
    private ManagedService? FindRunningService(string serviceName)
    {
        // Get all cache entries
        if (cache is not MemoryCache memCache) return null;
        // Use reflection to access the internal EntriesCollection
        object? coherentState = memCache.GetType()
            .GetField("_coherentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(memCache);

        if (coherentState?.GetType()
                .GetProperty("EntriesCollection")
                ?.GetValue(coherentState) is not ICollection entries) return null;
        foreach (object? entry in entries)
        {
            var key = entry.GetType().GetProperty("Key")?.GetValue(entry) as string;
            if (key?.StartsWith(CACHE_KEY_PREFIX) != true) continue;
            if (cache.TryGetValue(key, out ManagedService? service) && 
                service is not null && 
                service.ServiceName == serviceName)
            {
                return service;
            }
        }

        return null;
    }
}