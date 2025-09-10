using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services;

/// <summary>
/// Service to detect and initialize developer tool environments (VS, MSBuild, etc.)
/// This solves the issue of MCP servers running in generic command prompts without developer tools in PATH
/// </summary>
public class DeveloperEnvironmentService
{
    private readonly ILogger<DeveloperEnvironmentService>? _logger;
    private bool _initialized = false;
    private readonly Dictionary<string, string> _originalEnvironment = new();

    public DeveloperEnvironmentService(ILogger<DeveloperEnvironmentService>? logger = null)
    {
        _logger = logger;
    }

    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initialize the developer environment by finding and setting up VS/MSBuild paths
    /// </summary>
    public bool Initialize()
    {
        if (_initialized)
            return true;

        try
        {
            _logger?.LogInformation("Initializing developer environment...");

            // Save original environment
            foreach (object? key in Environment.GetEnvironmentVariables().Keys)
            {
                _originalEnvironment[key.ToString()!] = Environment.GetEnvironmentVariable(key.ToString()!)!;
            }

            // Try different initialization strategies
            if (InitializeFromVisualStudio2022())
            {
                _initialized = true;
                _logger?.LogInformation("Successfully initialized VS2022 environment");
                return true;
            }

            if (InitializeFromVisualStudio2019())
            {
                _initialized = true;
                _logger?.LogInformation("Successfully initialized VS2019 environment");
                return true;
            }

            if (InitializeFromDotNetSdk())
            {
                _initialized = true;
                _logger?.LogInformation("Successfully initialized .NET SDK environment");
                return true;
            }

            _logger?.LogWarning("Could not initialize any developer environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize developer environment");
            return false;
        }
    }

    private bool InitializeFromVisualStudio2022()
    {
        try
        {
            // Common VS2022 installation paths
            var possiblePaths = new[]
            {
                @"C:\Program Files\Microsoft Visual Studio\2022\Professional",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
                @"C:\Program Files\Microsoft Visual Studio\2022\Community",
                @"C:\Program Files\Microsoft Visual Studio\2022\Preview"
            };

            foreach (string vsPath in possiblePaths.Where(Directory.Exists))
            {
                _logger?.LogInformation($"Found VS2022 at: {vsPath}");

                // Set up MSBuild path
                string msbuildPath = Path.Combine(vsPath, @"MSBuild\Current\Bin");
                if (Directory.Exists(msbuildPath))
                {
                    AddToPath(msbuildPath);
                    Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(msbuildPath, "MSBuild.exe"));
                    _logger?.LogInformation($"Added MSBuild to PATH: {msbuildPath}");
                }

                // Set up Roslyn compilers
                string roslyPath = Path.Combine(vsPath, @"MSBuild\Current\Bin\Roslyn");
                if (Directory.Exists(roslyPath))
                {
                    AddToPath(roslyPath);
                    _logger?.LogInformation($"Added Roslyn to PATH: {roslyPath}");
                }

                // Set VS installation path
                Environment.SetEnvironmentVariable("VSINSTALLDIR", vsPath);
                Environment.SetEnvironmentVariable("VisualStudioVersion", "17.0");

                // Set up .NET Framework paths
                var dotnetFrameworkPath = @"C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.net.compilers.toolset\4.8.0\tasks\net472";
                if (Directory.Exists(dotnetFrameworkPath))
                {
                    AddToPath(dotnetFrameworkPath);
                }

                // Try to run VsDevCmd.bat to get all environment variables
                string vsDevCmdPath = Path.Combine(vsPath, @"Common7\Tools\VsDevCmd.bat");
                if (File.Exists(vsDevCmdPath))
                {
                    _logger?.LogInformation($"Found VsDevCmd.bat at: {vsDevCmdPath}");
                    return RunAndCaptureEnvironment(vsDevCmdPath);
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize from VS2022");
        }

        return false;
    }

    private bool InitializeFromVisualStudio2019()
    {
        try
        {
            // Common VS2019 installation paths
            var possiblePaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community"
            };

            foreach (string vsPath in possiblePaths.Where(Directory.Exists))
            {
                _logger?.LogInformation($"Found VS2019 at: {vsPath}");

                string msbuildPath = Path.Combine(vsPath, @"MSBuild\Current\Bin");
                if (Directory.Exists(msbuildPath))
                {
                    AddToPath(msbuildPath);
                    Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(msbuildPath, "MSBuild.exe"));
                }

                Environment.SetEnvironmentVariable("VSINSTALLDIR", vsPath);
                Environment.SetEnvironmentVariable("VisualStudioVersion", "16.0");

                string vsDevCmdPath = Path.Combine(vsPath, @"Common7\Tools\VsDevCmd.bat");
                if (File.Exists(vsDevCmdPath))
                {
                    return RunAndCaptureEnvironment(vsDevCmdPath);
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize from VS2019");
        }

        return false;
    }

    private bool InitializeFromDotNetSdk()
    {
        try
        {
            // Find dotnet.exe
            string? dotnetPath = FindInPath("dotnet.exe");
            if (string.IsNullOrEmpty(dotnetPath))
            {
                dotnetPath = @"C:\Program Files\dotnet\dotnet.exe";
            }

            if (File.Exists(dotnetPath))
            {
                _logger?.LogInformation($"Found dotnet at: {dotnetPath}");
                
                string dotnetDir = Path.GetDirectoryName(dotnetPath)!;
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetDir);
                
                // Find the latest SDK version
                string sdkPath = Path.Combine(dotnetDir, "sdk");
                if (Directory.Exists(sdkPath))
                {
                    string? latestSdk = Directory.GetDirectories(sdkPath)
                        .OrderByDescending(d => d)
                        .FirstOrDefault();
                    
                    if (latestSdk != null)
                    {
                        _logger?.LogInformation($"Found .NET SDK: {latestSdk}");
                        AddToPath(latestSdk);
                        
                        // MSBuild is included in the SDK
                        string msbuildDll = Path.Combine(latestSdk, "MSBuild.dll");
                        if (File.Exists(msbuildDll))
                        {
                            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildDll);
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize from .NET SDK");
        }

        return false;
    }

    private bool RunAndCaptureEnvironment(string batchFile)
    {
        try
        {
            _logger?.LogInformation($"Running batch file to capture environment: {batchFile}");

            // Create a temporary batch file that runs VsDevCmd and then outputs all environment variables
            string tempBatch = Path.GetTempFileName() + ".bat";
            string tempOutput = Path.GetTempFileName();

            File.WriteAllText(tempBatch, $@"
@echo off
call ""{batchFile}"" >nul 2>&1
set > ""{tempOutput}""
");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{tempBatch}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000);

            if (File.Exists(tempOutput))
            {
                string[] lines = File.ReadAllLines(tempOutput);
                foreach (string line in lines)
                {
                    string[] parts = line.Split(['='], 2);
                    if (parts.Length == 2)
                    {
                        Environment.SetEnvironmentVariable(parts[0], parts[1]);
                    }
                }

                // Clean up
                try
                {
                    File.Delete(tempBatch);
                    File.Delete(tempOutput);
                }
                catch { }

                _logger?.LogInformation("Successfully captured and applied environment variables");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to run and capture environment");
        }

        return false;
    }

    private static void AddToPath(string path)
    {
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!currentPath.Contains(path, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{path};{currentPath}");
        }
    }

    private static string? FindInPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (string dir in path.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    /// <summary>
    /// Get information about the current environment setup
    /// </summary>
    public Dictionary<string, string> GetEnvironmentInfo()
    {
        var info = new Dictionary<string, string>();

        info["Initialized"] = _initialized.ToString();
        info["MSBUILD_EXE_PATH"] = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH") ?? "Not set";
        info["VSINSTALLDIR"] = Environment.GetEnvironmentVariable("VSINSTALLDIR") ?? "Not set";
        info["VisualStudioVersion"] = Environment.GetEnvironmentVariable("VisualStudioVersion") ?? "Not set";
        info["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "Not set";
        
        // Check if MSBuild is accessible
        string? msbuildInPath = FindInPath("MSBuild.exe");
        info["MSBuild in PATH"] = msbuildInPath ?? "Not found";

        // Check if dotnet is accessible
        string? dotnetInPath = FindInPath("dotnet.exe");
        info["Dotnet in PATH"] = dotnetInPath ?? "Not found";

        return info;
    }
}
