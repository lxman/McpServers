using Microsoft.Extensions.Logging;
using Jering.Javascript.NodeJS;

namespace McpCodeEditor.Services.Diagnostics;

/// <summary>
/// Diagnostic service to test TypeScript AST parser functionality
/// </summary>
public class TypeScriptDiagnosticService
{
    private readonly ILogger<TypeScriptDiagnosticService> _logger;
    private readonly INodeJSService _nodeJSService;
    
    public TypeScriptDiagnosticService(
        ILogger<TypeScriptDiagnosticService> logger,
        INodeJSService nodeJSService)
    {
        _logger = logger;
        _nodeJSService = nodeJSService;
    }
    
    /// <summary>
    /// Test the TypeScript parser with a simple function
    /// </summary>
    public async Task<string> TestParserAsync()
    {
        try
        {
            _logger.LogInformation("Starting TypeScript parser diagnostic test");
            
            // Test 1: Simple inline JavaScript test
            _logger.LogInformation("Test 1: Testing inline JavaScript execution");
            var simpleResult = await _nodeJSService.InvokeFromStringAsync<string>(
                "module.exports = (callback, x) => callback(null, 'Hello ' + x);",
                args: ["World"]);
            _logger.LogInformation($"Simple test result: {simpleResult}");
            
            // Test 2: Test if we can access the TypeScript module
            _logger.LogInformation("Test 2: Testing TypeScript module availability");
            var moduleTest = await _nodeJSService.InvokeFromStringAsync<bool>(
                @"module.exports = (callback) => {
                    try {
                        const ts = require('typescript');
                        callback(null, ts !== undefined);
                    } catch (e) {
                        callback(e.toString());
                    }
                };");
            _logger.LogInformation($"TypeScript module available: {moduleTest}");
            
            // Test 3: Test the actual parser script
            _logger.LogInformation("Test 3: Testing parser script");
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "typescript-parser.js");
            
            if (!File.Exists(scriptPath))
            {
                return $"Script not found at: {scriptPath}";
            }
            
            // Try with a timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            var testCode = "function test() { return 42; }";
            var result = await _nodeJSService.InvokeFromFileAsync<dynamic>(
                scriptPath,
                "parseTypeScript",
                args: [testCode, "test.ts"],
                cancellationToken: cts.Token);
            
            return $"Parser test successful! Result type: {result?.GetType().Name}";
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Parser test timed out after 10 seconds");
            return "ERROR: Parser test timed out";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parser test failed");
            return $"ERROR: {ex.Message}";
        }
    }
}
