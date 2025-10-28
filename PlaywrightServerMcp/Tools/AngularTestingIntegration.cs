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

    [McpServerTool]
    [Description("Execute Angular unit tests with comprehensive result parsing and analysis. See skills/playwright-mcp/tools/angular/testing-integration.md.")]
    public async Task<string> ExecuteAngularUnitTests(
        string mode = "single-run",
        string testPattern = "",
        bool codeCoverage = true,
        string browser = "chrome",
        string workingDirectory = "",
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            PlaywrightSessionManager.SessionContext? session = _sessionManager.GetSession(sessionId);
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
            TestEnvironmentInfo envInfo = await GetTestEnvironmentInfo(config.WorkingDirectory);

            // Detect test framework
            TestFrameworkInfo frameworkInfo = await DetectTestFramework(config.WorkingDirectory);

            // Build the test command
            string command = await BuildTestCommand(config, frameworkInfo);

            // Execute the test command
            UnitTestResult result = await ExecuteTestCommand(command, config);

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
            string angularJsonPath = Path.Combine(directory, "angular.json");
            string packageJsonPath = Path.Combine(directory, "package.json");
            
            if (!File.Exists(angularJsonPath) || !File.Exists(packageJsonPath))
                return false;

            string packageJson = await File.ReadAllTextAsync(packageJsonPath);
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
            string angularJsonPath = Path.Combine(workingDirectory, "angular.json");
            if (File.Exists(angularJsonPath))
            {
                string angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
                var angularConfig = JsonSerializer.Deserialize<JsonElement>(angularJsonContent);

                if (angularConfig.TryGetProperty("projects", out JsonElement projects))
                {
                    foreach (JsonProperty project in projects.EnumerateObject())
                    {
                        if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                            architect.TryGetProperty("test", out JsonElement testConfig))
                        {
                            frameworkInfo.Framework = "karma"; // Default Angular framework
                            
                            if (testConfig.TryGetProperty("options", out JsonElement options))
                            {
                                if (options.TryGetProperty("karmaConfig", out JsonElement karmaConfig))
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
            string packageJsonPath = Path.Combine(workingDirectory, "package.json");
            if (File.Exists(packageJsonPath))
            {
                string packageJson = await File.ReadAllTextAsync(packageJsonPath);
                
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

            foreach (string configFile in configFiles)
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
                string ngVersionOutput = await ExecuteCommand("ng version", workingDirectory);
                Match angularVersionMatch = Regex.Match(ngVersionOutput, @"Angular CLI:\s*([^\r\n]+)");
                if (angularVersionMatch.Success)
                {
                    envInfo.AngularVersion = angularVersionMatch.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Try package.json fallback
                string packageJsonPath = Path.Combine(workingDirectory, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    string packageJson = await File.ReadAllTextAsync(packageJsonPath);
                    Match versionMatch = Regex.Match(packageJson, """
                                                                  "@angular/core":\s*"([^"]+)"
                                                                  """);
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
            string command = browser.ToLower() switch
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

            string result = await ExecuteCommand(command, "");
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
            string reportsDir = Path.Combine(config.WorkingDirectory, "test-results");
            Directory.CreateDirectory(reportsDir);
            
            // Add reporters based on format
            if (config.ReportFormat.Contains("json"))
            {
                commandBuilder.Append(" --reporters=json");
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

            bool completed = await Task.Run(() => process.WaitForExit(config.TimeoutSeconds * 1000));

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
            string output = result.StandardOutput;
            
            // Parse basic metrics from output
            result.Metrics = ParseTestMetrics(output);
            
            // Parse test suites
            result.TestSuites = ParseTestSuites(output);
            
            // Parse failures
            result.Failures = ParseTestFailures(output);

            // Look for JSON report files
            string reportsDir = Path.Combine(config.WorkingDirectory, "test-results");
            if (Directory.Exists(reportsDir))
            {
                string[] jsonReports = Directory.GetFiles(reportsDir, "*.json", SearchOption.AllDirectories);
                result.GeneratedReports.AddRange(jsonReports);

                // Parse detailed results from JSON reports
                foreach (string jsonReport in jsonReports)
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
            Match karmaMatch = Regex.Match(output, karmaPattern);
            if (karmaMatch.Success)
            {
                metrics.TotalTests = int.Parse(karmaMatch.Groups[1].Value);
                metrics.FailedTests = int.Parse(karmaMatch.Groups[2].Value);
                metrics.PassedTests = metrics.TotalTests - metrics.FailedTests;
            }

            // Parse Jest output patterns
            var jestPattern = @"Tests:\s+(\d+) failed,\s+(\d+) passed,\s+(\d+) total";
            Match jestMatch = Regex.Match(output, jestPattern);
            if (jestMatch.Success)
            {
                metrics.FailedTests = int.Parse(jestMatch.Groups[1].Value);
                metrics.PassedTests = int.Parse(jestMatch.Groups[2].Value);
                metrics.TotalTests = int.Parse(jestMatch.Groups[3].Value);
            }

            // Parse execution time
            var timePattern = @"Executed in (\d+\.?\d*) secs?";
            Match timeMatch = Regex.Match(output, timePattern);
            if (timeMatch.Success && double.TryParse(timeMatch.Groups[1].Value, out double seconds))
            {
                metrics.TotalExecutionTime = TimeSpan.FromSeconds(seconds);
            }

            // Count test suites
            var suitePattern = @"(.*\.spec\.(ts|js))";
            MatchCollection suiteMatches = Regex.Matches(output, suitePattern);
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
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            TestSuiteResult? currentSuite = null;

            foreach (string line in lines)
            {
                // Look for suite start patterns
                if (line.Contains(".spec.") && (line.Contains("PASSED") || line.Contains("FAILED")))
                {
                    string suiteName = ExtractSuiteName(line);
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
                    IndividualTestResult? testResult = ParseIndividualTest(line);
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
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            TestFailure? currentFailure = null;
            var inFailureSection = false;

            foreach (string line in lines)
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
            string coverageDir = Path.Combine(config.WorkingDirectory, "coverage");
            if (Directory.Exists(coverageDir))
            {
                result.Coverage.CoverageReportPath = coverageDir;

                // Parse lcov.info file if it exists
                string? lcovFile = Directory.GetFiles(coverageDir, "lcov.info", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(lcovFile))
                {
                    await ParseLcovFile(result.Coverage, lcovFile);
                }

                // Parse coverage-summary.json if it exists
                string? summaryFile = Directory.GetFiles(coverageDir, "coverage-summary.json", SearchOption.AllDirectories).FirstOrDefault();
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
            string jsonContent = await File.ReadAllTextAsync(jsonReportPath);
            var reportData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Parse based on report format (Karma, Jest, etc.)
            if (reportData.TryGetProperty("browsers", out JsonElement browsers))
            {
                // Karma JSON format
                await ParseKarmaJsonReport(result, reportData);
            }
            else if (reportData.TryGetProperty("testResults", out JsonElement testResults))
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
            string lcovContent = await File.ReadAllTextAsync(lcovFile);
            string[] lines = lcovContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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

            foreach (string line in lines)
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
                else if (line.StartsWith("LF:") && int.TryParse(line.Substring(3), out int lf))
                {
                    totalLines += lf;
                    currentFileTotalLines = lf;
                }
                else if (line.StartsWith("LH:") && int.TryParse(line.Substring(3), out int lh))
                {
                    coveredLines += lh;
                    if (currentFile != null)
                    {
                        currentFile.LineCoverage = currentFileTotalLines > 0 ? (double)lh / currentFileTotalLines * 100 : 0;
                    }
                }
                else if (line.StartsWith("BRF:") && int.TryParse(line.Substring(4), out int brf))
                {
                    totalBranches += brf;
                    currentFileTotalBranches = brf;
                }
                else if (line.StartsWith("BRH:") && int.TryParse(line.Substring(4), out int brh))
                {
                    coveredBranches += brh;
                    if (currentFile != null)
                    {
                        currentFile.BranchCoverage = currentFileTotalBranches > 0 ? (double)brh / currentFileTotalBranches * 100 : 0;
                    }
                }
                else if (line.StartsWith("FNF:") && int.TryParse(line.Substring(4), out int fnf))
                {
                    totalFunctions += fnf;
                    currentFileTotalFunctions = fnf;
                }
                else if (line.StartsWith("FNH:") && int.TryParse(line.Substring(4), out int fnh))
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
            string summaryContent = await File.ReadAllTextAsync(summaryFile);
            var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryContent);

            if (summaryData.TryGetProperty("total", out JsonElement total))
            {
                if (total.TryGetProperty("lines", out JsonElement lines) &&
                    lines.TryGetProperty("pct", out JsonElement linesPct))
                {
                    coverage.LineCoverage = linesPct.GetDouble();
                }

                if (total.TryGetProperty("branches", out JsonElement branches) &&
                    branches.TryGetProperty("pct", out JsonElement branchesPct))
                {
                    coverage.BranchCoverage = branchesPct.GetDouble();
                }

                if (total.TryGetProperty("functions", out JsonElement functions) &&
                    functions.TryGetProperty("pct", out JsonElement functionsPct))
                {
                    coverage.FunctionCoverage = functionsPct.GetDouble();
                }

                if (total.TryGetProperty("statements", out JsonElement statements) &&
                    statements.TryGetProperty("pct", out JsonElement statementsPct))
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

            foreach (string pattern in coveragePatterns)
            {
                Match match = Regex.Match(output, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double percentage))
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
            
            string output = await process.StandardOutput.ReadToEndAsync();
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
        Match match = Regex.Match(line, @"([\w-]+\.spec\.(ts|js))");
        return match.Success ? match.Groups[1].Value : "";
    }

    private string ExtractTestName(string line)
    {
        Match match = Regex.Match(line, @"should (.+?)(?:\s|$)");
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

        if (line.Contains("✗") || line.Contains("FAILED"))
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
