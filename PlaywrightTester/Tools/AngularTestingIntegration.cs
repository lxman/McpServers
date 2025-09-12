using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// Handles Angular testing integration for executing unit tests and parsing results
/// Implements ANG-011 Unit Test Integration - Basic
/// </summary>
[McpServerToolType]
public class AngularTestingIntegration(PlaywrightSessionManager sessionManager)
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
    /// Result structure for Angular unit test execution
    /// </summary>
    public class UnitTestResult
    {
        public bool Success { get; set; }
        public string Command { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TestFrameworkInfo TestFramework { get; set; } = new();
        public TestMetrics Metrics { get; set; } = new();
        public List<TestSuiteResult> TestSuites { get; set; } = [];
        public List<TestFailure> Failures { get; set; } = [];
        public CoverageReport Coverage { get; set; } = new();
        public List<string> GeneratedReports { get; set; } = [];
        public TestEnvironmentInfo Environment { get; set; } = new();
    }

    /// <summary>
    /// Information about the test framework being used
    /// </summary>
    public class TestFrameworkInfo
    {
        public string Framework { get; set; } = string.Empty; // karma, jest, web-test-runner
        public string TestRunner { get; set; } = string.Empty; // jasmine, mocha, etc.
        public string Browser { get; set; } = string.Empty; // chrome, firefox, etc.
        public bool HeadlessMode { get; set; }
        public bool WatchMode { get; set; }
        public bool CiMode { get; set; }
        public string ConfigFile { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test execution metrics
    /// </summary>
    public class TestMetrics
    {
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public int SkippedTests { get; set; }
        public int TotalSuites { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
        public bool AllTestsPassed => FailedTests == 0 && TotalTests > 0;
    }

    /// <summary>
    /// Individual test suite result
    /// </summary>
    public class TestSuiteResult
    {
        public string Name { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Tests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public List<IndividualTestResult> TestResults { get; set; } = [];
    }

    /// <summary>
    /// Individual test result
    /// </summary>
    public class IndividualTestResult
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // passed, failed, skipped
        public TimeSpan ExecutionTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test failure information
    /// </summary>
    public class TestFailure
    {
        public string TestName { get; set; } = string.Empty;
        public string SuiteName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public string ActualValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Code coverage report
    /// </summary>
    public class CoverageReport
    {
        public bool CoverageEnabled { get; set; }
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double FunctionCoverage { get; set; }
        public double StatementCoverage { get; set; }
        public List<FileCoverage> FileCoverages { get; set; } = [];
        public string CoverageReportPath { get; set; } = string.Empty;
        public CoverageThresholds Thresholds { get; set; } = new();
    }

    /// <summary>
    /// Coverage for individual files
    /// </summary>
    public class FileCoverage
    {
        public string FileName { get; set; } = string.Empty;
        public double LineCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double FunctionCoverage { get; set; }
        public double StatementCoverage { get; set; }
        public List<int> UncoveredLines { get; set; } = [];
    }

    /// <summary>
    /// Coverage thresholds configuration
    /// </summary>
    public class CoverageThresholds
    {
        public double Lines { get; set; }
        public double Branches { get; set; }
        public double Functions { get; set; }
        public double Statements { get; set; }
        public bool ThresholdsMet { get; set; }
    }

    /// <summary>
    /// Test environment information
    /// </summary>
    public class TestEnvironmentInfo
    {
        public string NodeVersion { get; set; } = string.Empty;
        public string AngularVersion { get; set; } = string.Empty;
        public string TestFrameworkVersion { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public bool ChromeInstalled { get; set; }
        public bool FirefoxInstalled { get; set; }
    }

    /// <summary>
    /// Configuration for test execution
    /// </summary>
    public class TestExecutionConfig
    {
        public string WorkingDirectory { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 300; // 5 minutes default
        public bool WatchMode { get; set; } = false;
        public bool HeadlessMode { get; set; } = true;
        public bool CiMode { get; set; } = false;
        public bool CodeCoverage { get; set; } = true;
        public string Browser { get; set; } = "chrome"; // chrome, firefox, edge
        public string TestPattern { get; set; } = string.Empty; // specific test files/patterns
        public string ConfigFile { get; set; } = string.Empty; // custom config file
        public bool GenerateReports { get; set; } = true;
        public string ReportFormat { get; set; } = "json"; // json, junit, lcov
        public bool ValidateAngularProject { get; set; } = true;
    }

    [McpServerTool]
    [Description("Execute Angular unit tests with comprehensive result parsing and analysis")]
    public async Task<string> ExecuteAngularUnitTests(
        [Description("Test execution configuration - 'watch' for watch mode, 'ci' for CI mode, or custom options")] string mode = "single-run",
        [Description("Specific test pattern or file to run (optional)")] string testPattern = "",
        [Description("Enable code coverage (default: true)")] bool codeCoverage = true,
        [Description("Browser to use for testing (chrome, firefox, edge)")] string browser = "chrome",
        [Description("Working directory (defaults to current directory)")] string workingDirectory = "",
        [Description("Session ID for context")] string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new UnitTestResult
                {
                    Success = false,
                    Command = "ng test",
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            var config = new TestExecutionConfig
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                WatchMode = mode.ToLower().Contains("watch"),
                CiMode = mode.ToLower().Contains("ci"),
                HeadlessMode = mode.ToLower().Contains("ci") || !mode.ToLower().Contains("watch"),
                CodeCoverage = codeCoverage,
                Browser = browser.ToLower(),
                TestPattern = testPattern,
                TimeoutSeconds = mode.ToLower().Contains("watch") ? 3600 : 300 // 1 hour for watch mode
            };

            // Validate Angular project
            if (config.ValidateAngularProject && !await IsAngularProject(config.WorkingDirectory))
            {
                return JsonSerializer.Serialize(new UnitTestResult
                {
                    Success = false,
                    Command = "ng test",
                    WorkingDirectory = config.WorkingDirectory,
                    ErrorMessage = "Not an Angular project - angular.json not found",
                    ExitCode = -1
                }, JsonOptions);
            }

            // Get environment information
            var envInfo = await GetTestEnvironmentInfo(config.WorkingDirectory);

            // Detect test framework
            var frameworkInfo = await DetectTestFramework(config.WorkingDirectory);

            // Build the test command
            var command = await BuildTestCommand(config, frameworkInfo);

            // Execute the test command
            var result = await ExecuteTestCommand(command, config);

            // Parse test results
            result.TestFramework = frameworkInfo;
            result.Environment = envInfo;

            await ParseTestResults(result, config);
            await ParseCoverageResults(result, config);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new UnitTestResult
            {
                Success = false,
                Command = "ng test",
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to execute Angular unit tests: {ex.Message}",
                ExitCode = -1
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    private async Task<bool> IsAngularProject(string directory)
    {
        try
        {
            var angularJsonPath = Path.Combine(directory, "angular.json");
            var packageJsonPath = Path.Combine(directory, "package.json");
            
            if (!File.Exists(angularJsonPath) || !File.Exists(packageJsonPath))
                return false;

            var packageJson = await File.ReadAllTextAsync(packageJsonPath);
            return packageJson.Contains("@angular/core") || packageJson.Contains("@angular/cli");
        }
        catch
        {
            return false;
        }
    }

    private async Task<TestFrameworkInfo> DetectTestFramework(string workingDirectory)
    {
        var frameworkInfo = new TestFrameworkInfo();
        
        try
        {
            // Check angular.json for test configuration
            var angularJsonPath = Path.Combine(workingDirectory, "angular.json");
            if (File.Exists(angularJsonPath))
            {
                var angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
                var angularConfig = JsonSerializer.Deserialize<JsonElement>(angularJsonContent);

                if (angularConfig.TryGetProperty("projects", out var projects))
                {
                    foreach (var project in projects.EnumerateObject())
                    {
                        if (project.Value.TryGetProperty("architect", out var architect) &&
                            architect.TryGetProperty("test", out var testConfig))
                        {
                            frameworkInfo.Framework = "karma"; // Default Angular framework
                            
                            if (testConfig.TryGetProperty("options", out var options))
                            {
                                if (options.TryGetProperty("karmaConfig", out var karmaConfig))
                                {
                                    frameworkInfo.ConfigFile = karmaConfig.GetString() ?? "";
                                }
                            }
                            break;
                        }
                    }
                }
            }

            // Check package.json for additional test frameworks
            var packageJsonPath = Path.Combine(workingDirectory, "package.json");
            if (File.Exists(packageJsonPath))
            {
                var packageJson = await File.ReadAllTextAsync(packageJsonPath);
                
                if (packageJson.Contains("jest"))
                {
                    frameworkInfo.Framework = "jest";
                    frameworkInfo.TestRunner = "jest";
                }
                else if (packageJson.Contains("@web/test-runner"))
                {
                    frameworkInfo.Framework = "web-test-runner";
                }
                else if (packageJson.Contains("karma"))
                {
                    frameworkInfo.Framework = "karma";
                    frameworkInfo.TestRunner = packageJson.Contains("jasmine") ? "jasmine" : "mocha";
                }
            }

            // Check for specific config files
            var configFiles = new[]
            {
                "karma.conf.js",
                "jest.config.js",
                "jest.config.json",
                "web-test-runner.config.js"
            };

            foreach (var configFile in configFiles)
            {
                if (File.Exists(Path.Combine(workingDirectory, configFile)))
                {
                    frameworkInfo.ConfigFile = configFile;
                    break;
                }
            }
        }
        catch
        {
            // Default fallback
            frameworkInfo.Framework = "karma";
            frameworkInfo.TestRunner = "jasmine";
        }

        return frameworkInfo;
    }

    private async Task<TestEnvironmentInfo> GetTestEnvironmentInfo(string workingDirectory)
    {
        var envInfo = new TestEnvironmentInfo
        {
            OperatingSystem = Environment.OSVersion.ToString()
        };

        try
        {
            // Get Node version
            envInfo.NodeVersion = await ExecuteCommand("node --version", workingDirectory);

            // Get Angular version
            try
            {
                var ngVersionOutput = await ExecuteCommand("ng version", workingDirectory);
                var angularVersionMatch = Regex.Match(ngVersionOutput, @"Angular CLI:\s*([^\r\n]+)");
                if (angularVersionMatch.Success)
                {
                    envInfo.AngularVersion = angularVersionMatch.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Try package.json fallback
                var packageJsonPath = Path.Combine(workingDirectory, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    var packageJson = await File.ReadAllTextAsync(packageJsonPath);
                    var versionMatch = Regex.Match(packageJson, @"""@angular/core"":\s*""([^""]+)""");
                    if (versionMatch.Success)
                    {
                        envInfo.AngularVersion = versionMatch.Groups[1].Value;
                    }
                }
            }

            // Check for browser installations
            envInfo.ChromeInstalled = await IsBrowserInstalled("chrome");
            envInfo.FirefoxInstalled = await IsBrowserInstalled("firefox");
        }
        catch
        {
            // Continue with partial info
        }

        return envInfo;
    }

    private async Task<bool> IsBrowserInstalled(string browser)
    {
        try
        {
            var command = browser.ToLower() switch
            {
                "chrome" => Environment.OSVersion.Platform == PlatformID.Win32NT 
                    ? "reg query \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe\"" 
                    : "which google-chrome",
                "firefox" => Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? "reg query \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\firefox.exe\""
                    : "which firefox",
                _ => ""
            };

            if (string.IsNullOrEmpty(command))
                return false;

            var result = await ExecuteCommand(command, "");
            return !string.IsNullOrEmpty(result) && !result.Contains("not found");
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> BuildTestCommand(TestExecutionConfig config, TestFrameworkInfo frameworkInfo)
    {
        var commandBuilder = new StringBuilder("ng test");

        // Add framework-specific options
        if (frameworkInfo.Framework == "karma")
        {
            if (config.HeadlessMode)
            {
                commandBuilder.Append(" --browsers=ChromeHeadless");
            }
            else
            {
                commandBuilder.Append($" --browsers={char.ToUpper(config.Browser[0])}{config.Browser[1..]}");
            }

            if (!config.WatchMode)
            {
                commandBuilder.Append(" --watch=false");
            }

            if (config.CiMode)
            {
                commandBuilder.Append(" --progress=false");
            }
        }

        // Code coverage
        if (config.CodeCoverage)
        {
            commandBuilder.Append(" --code-coverage");
        }

        // Test pattern
        if (!string.IsNullOrWhiteSpace(config.TestPattern))
        {
            commandBuilder.Append($" --include=\"{config.TestPattern}\"");
        }

        // Custom config file
        if (!string.IsNullOrWhiteSpace(config.ConfigFile))
        {
            commandBuilder.Append($" --karma-config=\"{config.ConfigFile}\"");
        }

        // Generate reports
        if (config.GenerateReports)
        {
            var reportsDir = Path.Combine(config.WorkingDirectory, "test-results");
            Directory.CreateDirectory(reportsDir);
            
            // Add reporters based on format
            if (config.ReportFormat.Contains("json"))
            {
                commandBuilder.Append($" --reporters=json");
            }
        }

        return commandBuilder.ToString();
    }

    private async Task<UnitTestResult> ExecuteTestCommand(string command, TestExecutionConfig config)
    {
        var result = new UnitTestResult
        {
            Command = command,
            WorkingDirectory = config.WorkingDirectory
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd" : "/bin/bash",
                Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
                WorkingDirectory = config.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(config.TimeoutSeconds * 1000));

            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;

            if (!completed)
            {
                process.Kill();
                result.ErrorMessage = $"Test execution timed out after {config.TimeoutSeconds} seconds";
                result.ExitCode = -1;
                return result;
            }

            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            result.StandardOutput = outputBuilder.ToString();
            result.StandardError = errorBuilder.ToString();

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
            result.ErrorMessage = $"Failed to execute test command: {ex.Message}";
            result.ExitCode = -1;
            return result;
        }
    }

    private async Task ParseTestResults(UnitTestResult result, TestExecutionConfig config)
    {
        try
        {
            var output = result.StandardOutput;
            
            // Parse basic metrics from output
            result.Metrics = ParseTestMetrics(output);
            
            // Parse test suites
            result.TestSuites = ParseTestSuites(output);
            
            // Parse failures
            result.Failures = ParseTestFailures(output);

            // Look for JSON report files
            var reportsDir = Path.Combine(config.WorkingDirectory, "test-results");
            if (Directory.Exists(reportsDir))
            {
                var jsonReports = Directory.GetFiles(reportsDir, "*.json", SearchOption.AllDirectories);
                result.GeneratedReports.AddRange(jsonReports);

                // Parse detailed results from JSON reports
                foreach (var jsonReport in jsonReports)
                {
                    await ParseJsonTestReport(result, jsonReport);
                }
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage += $"\nFailed to parse test results: {ex.Message}";
        }
    }

    private TestMetrics ParseTestMetrics(string output)
    {
        var metrics = new TestMetrics();

        try
        {
            // Parse Karma/Jasmine output patterns
            var karmaPattern = @"(\d+) specs?, (\d+) failures?";
            var karmaMatch = Regex.Match(output, karmaPattern);
            if (karmaMatch.Success)
            {
                metrics.TotalTests = int.Parse(karmaMatch.Groups[1].Value);
                metrics.FailedTests = int.Parse(karmaMatch.Groups[2].Value);
                metrics.PassedTests = metrics.TotalTests - metrics.FailedTests;
            }

            // Parse Jest output patterns
            var jestPattern = @"Tests:\s+(\d+) failed,\s+(\d+) passed,\s+(\d+) total";
            var jestMatch = Regex.Match(output, jestPattern);
            if (jestMatch.Success)
            {
                metrics.FailedTests = int.Parse(jestMatch.Groups[1].Value);
                metrics.PassedTests = int.Parse(jestMatch.Groups[2].Value);
                metrics.TotalTests = int.Parse(jestMatch.Groups[3].Value);
            }

            // Parse execution time
            var timePattern = @"Executed in (\d+\.?\d*) secs?";
            var timeMatch = Regex.Match(output, timePattern);
            if (timeMatch.Success && double.TryParse(timeMatch.Groups[1].Value, out var seconds))
            {
                metrics.TotalExecutionTime = TimeSpan.FromSeconds(seconds);
            }

            // Count test suites
            var suitePattern = @"(.*\.spec\.(ts|js))";
            var suiteMatches = Regex.Matches(output, suitePattern);
            metrics.TotalSuites = suiteMatches.Count;
        }
        catch
        {
            // Continue with partial metrics
        }

        return metrics;
    }

    private List<TestSuiteResult> ParseTestSuites(string output)
    {
        var suites = new List<TestSuiteResult>();

        try
        {
            // Parse test suite information from output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            TestSuiteResult? currentSuite = null;

            foreach (var line in lines)
            {
                // Look for suite start patterns
                if (line.Contains(".spec.") && (line.Contains("PASSED") || line.Contains("FAILED")))
                {
                    var suiteName = ExtractSuiteName(line);
                    if (!string.IsNullOrEmpty(suiteName))
                    {
                        currentSuite = new TestSuiteResult
                        {
                            Name = suiteName,
                            File = suiteName
                        };
                        suites.Add(currentSuite);
                    }
                }

                // Parse individual test results within suites
                if (currentSuite != null && (line.Contains("✓") || line.Contains("✗") || line.Contains("PASSED") || line.Contains("FAILED")))
                {
                    var testResult = ParseIndividualTest(line);
                    if (testResult != null)
                    {
                        currentSuite.TestResults.Add(testResult);
                        currentSuite.Tests++;
                        
                        if (testResult.Status == "passed")
                            currentSuite.Passed++;
                        else if (testResult.Status == "failed")
                            currentSuite.Failed++;
                        else if (testResult.Status == "skipped")
                            currentSuite.Skipped++;
                    }
                }
            }
        }
        catch
        {
            // Continue with partial results
        }

        return suites;
    }

    private List<TestFailure> ParseTestFailures(string output)
    {
        var failures = new List<TestFailure>();

        try
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            TestFailure? currentFailure = null;
            bool inFailureSection = false;

            foreach (var line in lines)
            {
                // Look for failure start patterns
                if (line.Contains("FAILED:") || (line.Contains("✗") && line.Contains("should")))
                {
                    if (currentFailure != null)
                    {
                        failures.Add(currentFailure);
                    }

                    currentFailure = new TestFailure
                    {
                        TestName = ExtractTestName(line)
                    };
                    inFailureSection = true;
                }
                else if (inFailureSection && currentFailure != null)
                {
                    // Parse error message and stack trace
                    if (line.Contains("Error:") || line.Contains("Expected:") || line.Contains("Actual:"))
                    {
                        if (string.IsNullOrEmpty(currentFailure.ErrorMessage))
                        {
                            currentFailure.ErrorMessage = line.Trim();
                        }
                        else
                        {
                            currentFailure.StackTrace += line + "\n";
                        }
                    }
                    else if (line.Contains("at ") && line.Contains("("))
                    {
                        currentFailure.StackTrace += line + "\n";
                    }
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        inFailureSection = false;
                        if (currentFailure != null)
                        {
                            failures.Add(currentFailure);
                            currentFailure = null;
                        }
                    }
                }
            }

            // Add final failure if exists
            if (currentFailure != null)
            {
                failures.Add(currentFailure);
            }
        }
        catch
        {
            // Continue with partial failures
        }

        return failures;
    }

    private async Task ParseCoverageResults(UnitTestResult result, TestExecutionConfig config)
    {
        if (!config.CodeCoverage)
        {
            result.Coverage.CoverageEnabled = false;
            return;
        }

        result.Coverage.CoverageEnabled = true;

        try
        {
            // Look for coverage directory
            var coverageDir = Path.Combine(config.WorkingDirectory, "coverage");
            if (Directory.Exists(coverageDir))
            {
                result.Coverage.CoverageReportPath = coverageDir;

                // Parse lcov.info file if it exists
                var lcovFile = Directory.GetFiles(coverageDir, "lcov.info", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(lcovFile))
                {
                    await ParseLcovFile(result.Coverage, lcovFile);
                }

                // Parse coverage-summary.json if it exists
                var summaryFile = Directory.GetFiles(coverageDir, "coverage-summary.json", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(summaryFile))
                {
                    await ParseCoverageSummary(result.Coverage, summaryFile);
                }
            }

            // Parse coverage from console output
            ParseCoverageFromOutput(result.Coverage, result.StandardOutput);
        }
        catch (Exception ex)
        {
            result.ErrorMessage += $"\nFailed to parse coverage results: {ex.Message}";
        }
    }

    private async Task ParseJsonTestReport(UnitTestResult result, string jsonReportPath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonReportPath);
            var reportData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Parse based on report format (Karma, Jest, etc.)
            if (reportData.TryGetProperty("browsers", out var browsers))
            {
                // Karma JSON format
                await ParseKarmaJsonReport(result, reportData);
            }
            else if (reportData.TryGetProperty("testResults", out var testResults))
            {
                // Jest JSON format
                await ParseJestJsonReport(result, reportData);
            }
        }
        catch
        {
            // Continue without detailed JSON parsing
        }
    }

    private async Task ParseKarmaJsonReport(UnitTestResult result, JsonElement reportData)
    {
        // Implementation for parsing Karma JSON reports
        // This would extract detailed test information from Karma's JSON output
        await Task.CompletedTask;
    }

    private async Task ParseJestJsonReport(UnitTestResult result, JsonElement reportData)
    {
        // Implementation for parsing Jest JSON reports
        // This would extract detailed test information from Jest's JSON output
        await Task.CompletedTask;
    }

    private async Task ParseLcovFile(CoverageReport coverage, string lcovFile)
    {
        try
        {
            var lcovContent = await File.ReadAllTextAsync(lcovFile);
            var lines = lcovContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            FileCoverage? currentFile = null;
            var totalLines = 0;
            var coveredLines = 0;
            var totalBranches = 0;
            var coveredBranches = 0;
            var totalFunctions = 0;
            var coveredFunctions = 0;

            // Track current file metrics
            var currentFileTotalLines = 0;
            var currentFileTotalBranches = 0;
            var currentFileTotalFunctions = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("SF:"))
                {
                    if (currentFile != null)
                    {
                        coverage.FileCoverages.Add(currentFile);
                    }
                    currentFile = new FileCoverage
                    {
                        FileName = line.Substring(3)
                    };
                    currentFileTotalLines = 0;
                    currentFileTotalBranches = 0;
                    currentFileTotalFunctions = 0;
                }
                else if (line.StartsWith("LF:") && int.TryParse(line.Substring(3), out var lf))
                {
                    totalLines += lf;
                    currentFileTotalLines = lf;
                }
                else if (line.StartsWith("LH:") && int.TryParse(line.Substring(3), out var lh))
                {
                    coveredLines += lh;
                    if (currentFile != null)
                    {
                        currentFile.LineCoverage = currentFileTotalLines > 0 ? (double)lh / currentFileTotalLines * 100 : 0;
                    }
                }
                else if (line.StartsWith("BRF:") && int.TryParse(line.Substring(4), out var brf))
                {
                    totalBranches += brf;
                    currentFileTotalBranches = brf;
                }
                else if (line.StartsWith("BRH:") && int.TryParse(line.Substring(4), out var brh))
                {
                    coveredBranches += brh;
                    if (currentFile != null)
                    {
                        currentFile.BranchCoverage = currentFileTotalBranches > 0 ? (double)brh / currentFileTotalBranches * 100 : 0;
                    }
                }
                else if (line.StartsWith("FNF:") && int.TryParse(line.Substring(4), out var fnf))
                {
                    totalFunctions += fnf;
                    currentFileTotalFunctions = fnf;
                }
                else if (line.StartsWith("FNH:") && int.TryParse(line.Substring(4), out var fnh))
                {
                    coveredFunctions += fnh;
                    if (currentFile != null)
                    {
                        currentFile.FunctionCoverage = currentFileTotalFunctions > 0 ? (double)fnh / currentFileTotalFunctions * 100 : 0;
                    }
                }
            }

            if (currentFile != null)
            {
                coverage.FileCoverages.Add(currentFile);
            }

            // Calculate overall coverage
            coverage.LineCoverage = totalLines > 0 ? (double)coveredLines / totalLines * 100 : 0;
            coverage.BranchCoverage = totalBranches > 0 ? (double)coveredBranches / totalBranches * 100 : 0;
            coverage.FunctionCoverage = totalFunctions > 0 ? (double)coveredFunctions / totalFunctions * 100 : 0;
        }
        catch
        {
            // Continue without LCOV parsing
        }
    }

    private async Task ParseCoverageSummary(CoverageReport coverage, string summaryFile)
    {
        try
        {
            var summaryContent = await File.ReadAllTextAsync(summaryFile);
            var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryContent);

            if (summaryData.TryGetProperty("total", out var total))
            {
                if (total.TryGetProperty("lines", out var lines) &&
                    lines.TryGetProperty("pct", out var linesPct))
                {
                    coverage.LineCoverage = linesPct.GetDouble();
                }

                if (total.TryGetProperty("branches", out var branches) &&
                    branches.TryGetProperty("pct", out var branchesPct))
                {
                    coverage.BranchCoverage = branchesPct.GetDouble();
                }

                if (total.TryGetProperty("functions", out var functions) &&
                    functions.TryGetProperty("pct", out var functionsPct))
                {
                    coverage.FunctionCoverage = functionsPct.GetDouble();
                }

                if (total.TryGetProperty("statements", out var statements) &&
                    statements.TryGetProperty("pct", out var statementsPct))
                {
                    coverage.StatementCoverage = statementsPct.GetDouble();
                }
            }
        }
        catch
        {
            // Continue without summary parsing
        }
    }

    private void ParseCoverageFromOutput(CoverageReport coverage, string output)
    {
        try
        {
            // Parse coverage percentages from console output
            var coveragePatterns = new[]
            {
                @"Statements\s*:\s*(\d+\.?\d*)%",
                @"Branches\s*:\s*(\d+\.?\d*)%",
                @"Functions\s*:\s*(\d+\.?\d*)%",
                @"Lines\s*:\s*(\d+\.?\d*)%"
            };

            foreach (var pattern in coveragePatterns)
            {
                var match = Regex.Match(output, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out var percentage))
                {
                    if (pattern.Contains("Statements"))
                        coverage.StatementCoverage = percentage;
                    else if (pattern.Contains("Branches"))
                        coverage.BranchCoverage = percentage;
                    else if (pattern.Contains("Functions"))
                        coverage.FunctionCoverage = percentage;
                    else if (pattern.Contains("Lines"))
                        coverage.LineCoverage = percentage;
                }
            }
        }
        catch
        {
            // Continue without output parsing
        }
    }

    private async Task<string> ExecuteCommand(string command, string workingDirectory)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd" : "/bin/bash",
                Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExtractSuiteName(string line)
    {
        var match = Regex.Match(line, @"([\w-]+\.spec\.(ts|js))");
        return match.Success ? match.Groups[1].Value : "";
    }

    private string ExtractTestName(string line)
    {
        var match = Regex.Match(line, @"should (.+?)(?:\s|$)");
        return match.Success ? $"should {match.Groups[1].Value}" : line.Trim();
    }

    private IndividualTestResult? ParseIndividualTest(string line)
    {
        if (line.Contains("✓") || line.Contains("PASSED"))
        {
            return new IndividualTestResult
            {
                Name = ExtractTestName(line),
                Status = "passed"
            };
        }
        else if (line.Contains("✗") || line.Contains("FAILED"))
        {
            return new IndividualTestResult
            {
                Name = ExtractTestName(line),
                Status = "failed"
            };
        }

        return null;
    }
}
