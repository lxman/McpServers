using Jering.Javascript.NodeJS;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service that uses Node.js to run the actual TypeScript compiler for proper AST parsing
/// This provides accurate parsing that preserves all context, scope, and type information
/// </summary>
public class TypeScriptAstParserService
{
    private readonly ILogger<TypeScriptAstParserService> _logger;
    private readonly INodeJSService _nodeJSService;
    private readonly string _scriptPath;
    
    public TypeScriptAstParserService(
        ILogger<TypeScriptAstParserService> logger,
        INodeJSService nodeJSService)
    {
        _logger = logger;
        _nodeJSService = nodeJSService;
        _scriptPath = GetScriptPath();
    }

    /// <summary>
    /// Parse TypeScript code and return a complete AST with all context preserved
    /// </summary>
    public async Task<TypeScriptAst> ParseAsync(string sourceCode, string fileName = "temp.ts")
    {
        try
        {
            _logger.LogDebug("Parsing TypeScript file: {FileName}", fileName);
            
            // Invoke the TypeScript parser from file (works better with CommonJS modules)
            var result = await _nodeJSService.InvokeFromFileAsync<TypeScriptAst>(
                _scriptPath,
                "parseTypeScript",
                args: [sourceCode, fileName]);
            
            if (result == null)
            {
                throw new InvalidOperationException("Parser returned null result");
            }
            
            _logger.LogDebug("Successfully parsed TypeScript with {NodeCount} nodes", result.Nodes?.Count ?? 0);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse TypeScript code");
            throw new InvalidOperationException($"TypeScript parsing failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extract detailed information about a specific method or function
    /// </summary>
    public async Task<MethodAstInfo> ExtractMethodInfoAsync(
        string sourceCode, 
        int startLine, 
        int endLine,
        string fileName = "temp.ts")
    {
        try
        {
            _logger.LogDebug("Extracting method info from lines {Start}-{End}", startLine, endLine);
            
            var result = await _nodeJSService.InvokeFromFileAsync<MethodAstInfo>(
                _scriptPath,
                "extractMethodInfo",
                args: [sourceCode, startLine, endLine, fileName]);
            
            if (result == null)
            {
                throw new InvalidOperationException("Method extraction returned null");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract method info");
            throw new InvalidOperationException($"Method info extraction failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Analyze scope and context for a code region
    /// </summary>
    public async Task<ScopeAnalysis> AnalyzeScopeAsync(
        string sourceCode,
        int startLine,
        int endLine,
        string fileName = "temp.ts")
    {
        try
        {
            _logger.LogDebug("Analyzing scope for lines {Start}-{End}", startLine, endLine);
            
            var result = await _nodeJSService.InvokeFromFileAsync<ScopeAnalysis>(
                _scriptPath,
                "analyzeScope",
                args: [sourceCode, startLine, endLine, fileName]);
            
            if (result == null)
            {
                throw new InvalidOperationException("Scope analysis returned null");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze scope");
            throw new InvalidOperationException($"Scope analysis failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get the path to the TypeScript parser script
    /// </summary>
    private string GetScriptPath()
    {
        // Try to load from Scripts directory
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "typescript-parser.js");
        
        if (File.Exists(scriptPath))
        {
            _logger.LogInformation("Using TypeScript parser script at: {Path}", scriptPath);
            return scriptPath;
        }
        
        // Fallback - create a temporary script file
        _logger.LogWarning("TypeScript parser script not found at: {Path}. Creating fallback.", scriptPath);
        string fallbackPath = Path.Combine(Path.GetTempPath(), "typescript-parser-fallback.js");
        File.WriteAllText(fallbackPath, GetFallbackParserScript());
        return fallbackPath;
    }
    
    /// <summary>
    /// Fallback parser script if file is not found
    /// </summary>
    private static string GetFallbackParserScript()
    {
        // Minimal fallback script
        return @"
const ts = require('typescript');

module.exports.parseTypeScript = function(sourceCode, fileName) {
    const sourceFile = ts.createSourceFile(
        fileName,
        sourceCode,
        ts.ScriptTarget.Latest,
        true
    );
    
    return {
        fileName: fileName,
        nodes: [],
        imports: [],
        exports: [],
        classes: [],
        functions: [],
        variables: [],
        diagnostics: sourceFile.parseDiagnostics || []
    };
};

module.exports.extractMethodInfo = function(sourceCode, startLine, endLine, fileName) {
    return {
        name: '',
        parameters: [],
        returnType: 'any',
        isAsync: false,
        isStatic: false,
        isPrivate: false,
        isProtected: false,
        isPublic: false,
        usedVariables: [],
        modifiedVariables: [],
        thisReferences: [],
        hasReturnStatement: false,
        hasAwait: false,
        complexity: 0
    };
};

module.exports.analyzeScope = function(sourceCode, startLine, endLine, fileName) {
    return {
        parentClass: null,
        parentFunction: null,
        localVariables: [],
        closureVariables: [],
        thisContext: null,
        imports: [],
        exports: []
    };
};
";
    }

    public void Dispose()
    {
        _nodeJSService?.Dispose();
    }
}

// AST Data Models
public class TypeScriptAst
{
    public string FileName { get; set; } = string.Empty;
    public List<AstNode> Nodes { get; set; } = [];
    public List<ImportInfo> Imports { get; set; } = [];
    public List<ExportInfo> Exports { get; set; } = [];
    public List<ClassInfo> Classes { get; set; } = [];
    public List<FunctionInfo> Functions { get; set; } = [];
    public List<VariableInfo> Variables { get; set; } = [];
    public List<DiagnosticInfo> Diagnostics { get; set; } = [];
}

public class AstNode
{
    public string Kind { get; set; } = string.Empty;
    public int KindValue { get; set; }
    public string Text { get; set; } = string.Empty;
    public Position Start { get; set; } = new();
    public Position End { get; set; } = new();
    public List<AstNode> Children { get; set; } = [];
}

public class Position
{
    public int Line { get; set; }
    public int Character { get; set; }
}

public class ImportInfo
{
    public string Module { get; set; } = string.Empty;
    public List<NamedImport> NamedImports { get; set; } = [];
    public string? DefaultImport { get; set; }
    public bool IsTypeOnly { get; set; }
}

public class NamedImport
{
    public string Name { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class ExportInfo
{
    public bool IsDefault { get; set; }
    public string? Name { get; set; }
}

public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsAbstract { get; set; }
    public List<ClassMember> Members { get; set; } = [];
}

public class ClassMember
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // "property" or "method"
    public bool IsStatic { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsProtected { get; set; }
}

public class FunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = [];
    public bool IsAsync { get; set; }
    public bool IsExported { get; set; }
}

public class VariableInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsConst { get; set; }
    public bool IsLet { get; set; }
    public bool HasInitializer { get; set; }
}

public class DiagnosticInfo
{
    public string Message { get; set; } = string.Empty;
    public int Code { get; set; }
    public Position Start { get; set; } = new();
}

public class MethodAstInfo
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = [];
    public string ReturnType { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public bool IsStatic { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsProtected { get; set; }
    public bool IsPublic { get; set; }
    public List<string> UsedVariables { get; set; } = [];
    public List<string> ModifiedVariables { get; set; } = [];
    public List<ThisReference> ThisReferences { get; set; } = [];
    public bool HasReturnStatement { get; set; }
    public bool HasAwait { get; set; }
    public int Complexity { get; set; }
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public bool IsOptional { get; set; }
    public bool HasDefault { get; set; }
    public bool IsRest { get; set; }
}

public class ThisReference
{
    public string Property { get; set; } = string.Empty;
    public bool IsMethodCall { get; set; }
}

public class ScopeAnalysis
{
    public ClassContext? ParentClass { get; set; }
    public FunctionContext? ParentFunction { get; set; }
    public List<LocalVariable> LocalVariables { get; set; } = [];
    public List<string> ClosureVariables { get; set; } = [];
    public string? ThisContext { get; set; } // "class", "method", or null
    public List<ImportInfo> Imports { get; set; } = [];
    public List<ExportInfo> Exports { get; set; } = [];
}

public class ClassContext
{
    public string Name { get; set; } = string.Empty;
    public List<ClassMember> Members { get; set; } = [];
    public List<string>? Extends { get; set; }
}

public class FunctionContext
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = [];
    public bool IsAsync { get; set; }
}

public class LocalVariable
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // "const", "let", or "var"
}
