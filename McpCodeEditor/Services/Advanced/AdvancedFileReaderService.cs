using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Advanced
{
    public class AdvancedFileReaderService : IAdvancedFileReaderService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdvancedFileReaderService> _logger;
        private readonly CodeEditorConfigurationService _config;
        
        public AdvancedFileReaderService(
            IMemoryCache cache,
            ILogger<AdvancedFileReaderService> logger,
            CodeEditorConfigurationService config)
        {
            _cache = cache;
            _logger = logger;
            _config = config;
        }

        public async Task<FileReadResult> ReadRangeAsync(string filePath, int startLine, int endLine)
        {
            try
            {
                if (!File.Exists(ValidateAndResolvePath(filePath)))
                    return FileReadResult.Error($"File not found: {filePath}");

                string[] allLines = await File.ReadAllLinesAsync(ValidateAndResolvePath(filePath));
                
                if (startLine < 1 || endLine > allLines.Length || startLine > endLine)
                    return FileReadResult.Error($"Invalid line range: {startLine}-{endLine} (file has {allLines.Length} lines)");

                string[] selectedLines = allLines
                    .Skip(startLine - 1)
                    .Take(endLine - startLine + 1)
                    .ToArray();

                return new FileReadResult
                {
                    Success = true,
                    Content = string.Join("\n", selectedLines),
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    TotalLines = allLines.Length,
                    ReadMethod = "Line Range"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file range {StartLine}-{EndLine} from {FilePath}", startLine, endLine, filePath);
                return FileReadResult.Error($"Error reading file: {ex.Message}");
            }
        }
        
        public async Task<FileReadResult> ReadAroundLineAsync(string filePath, int lineNumber, int contextLines = 10)
        {
            int startLine = Math.Max(1, lineNumber - contextLines);
            int endLine = lineNumber + contextLines;
            
            FileReadResult result = await ReadRangeAsync(ValidateAndResolvePath(filePath), startLine, endLine);
            if (result.Success)
            {
                result.ReadMethod = $"Around Line {lineNumber}";
                result.Metadata["CenterLine"] = lineNumber;
                result.Metadata["ContextLines"] = contextLines;
            }
            
            return result;
        }

        public async Task<FileReadResult> ReadMethodAsync(string filePath, string methodName, int contextLines = 5)
        {
            try
            {
                SyntaxTree? syntaxTree = await GetSyntaxTreeAsync(ValidateAndResolvePath(filePath));
                if (syntaxTree == null)
                    return FileReadResult.Error($"Unable to parse {filePath} as C# code");

                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                MethodDeclarationSyntax? method = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == methodName);

                if (method == null)
                    return FileReadResult.Error($"Method '{methodName}' not found in {filePath}");

                FileLinePositionSpan lineSpan = method.GetLocation().GetLineSpan();
                int startLine = Math.Max(1, lineSpan.StartLinePosition.Line + 1 - contextLines);
                int endLine = lineSpan.EndLinePosition.Line + 1 + contextLines;

                FileReadResult result = await ReadRangeAsync(filePath, startLine, endLine);
                if (result.Success)
                {
                    result.ReadMethod = $"Method '{methodName}' with context";
                    result.Metadata["MethodName"] = methodName;
                    result.Metadata["MethodStartLine"] = lineSpan.StartLinePosition.Line + 1;
                    result.Metadata["MethodEndLine"] = lineSpan.EndLinePosition.Line + 1;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading method {MethodName} from {FilePath}", methodName, filePath);
                return FileReadResult.Error($"Error reading method: {ex.Message}");
            }
        }
        
        public async Task<FileReadResult> ReadClassAsync(string filePath, string className)
        {
            try
            {
                SyntaxTree? syntaxTree = await GetSyntaxTreeAsync(ValidateAndResolvePath(filePath));
                if (syntaxTree == null)
                    return FileReadResult.Error($"Unable to parse {filePath} as C# code");

                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                ClassDeclarationSyntax? classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == className);

                if (classDeclaration == null)
                    return FileReadResult.Error($"Class '{className}' not found in {filePath}");

                FileLinePositionSpan lineSpan = classDeclaration.GetLocation().GetLineSpan();
                int startLine = lineSpan.StartLinePosition.Line + 1;
                int endLine = lineSpan.EndLinePosition.Line + 1;

                FileReadResult result = await ReadRangeAsync(filePath, startLine, endLine);
                if (result.Success)
                {
                    result.ReadMethod = $"Complete Class '{className}'";
                    result.Metadata["ClassName"] = className;
                    result.Metadata["MethodCount"] = classDeclaration.Members.OfType<MethodDeclarationSyntax>().Count();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading class {ClassName} from {FilePath}", className, filePath);
                return FileReadResult.Error($"Error reading class: {ex.Message}");
            }
        }

        public async Task<FileStructureResult> GetFileOutlineAsync(string filePath)
        {
            try
            {
                SyntaxTree? syntaxTree = await GetSyntaxTreeAsync(ValidateAndResolvePath(filePath));
                if (syntaxTree == null)
                    return new FileStructureResult { Success = false, Warnings = { $"Unable to parse {filePath} as C# code" } };

                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                
                List<string> usingStatements = root.Usings
                    .Select(u => u.ToString().Trim())
                    .ToList();

                List<ClassInfo> classes = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Select(ExtractClassInfo)
                    .ToList();

                List<MethodInfo> methods = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Select(ExtractMethodInfo)
                    .ToList();

                return new FileStructureResult
                {
                    Success = true,
                    FilePath = filePath,
                    Classes = classes,
                    Methods = methods,
                    UsingStatements = usingStatements
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file structure for {FilePath}", filePath);
                return new FileStructureResult { Success = false, Warnings = { $"Error analyzing structure: {ex.Message}" } };
            }
        }
        
        public async Task<FileReadResult> ReadMethodSignaturesAsync(string filePath)
        {
            FileStructureResult structure = await GetFileOutlineAsync(ValidateAndResolvePath(filePath));
            if (!structure.Success)
                return FileReadResult.Error("Unable to extract method signatures");
                
            IEnumerable<string> signatures = structure.Methods.Select(m => $"Line {m.StartLine}: {m.Signature}");
            return new FileReadResult
            {
                Success = true,
                Content = string.Join("\n", signatures),
                FilePath = filePath,
                ReadMethod = "Method Signatures",
                Metadata = new Dictionary<string, object> { ["MethodCount"] = structure.Methods.Count }
            };
        }
        
        public async Task<FileReadResult> ReadImportsAndHeaderAsync(string filePath)
        {
            FileStructureResult structure = await GetFileOutlineAsync(ValidateAndResolvePath(filePath));
            if (!structure.Success)
                return FileReadResult.Error("Unable to extract imports and header");
                
            var content = new List<string>();
            content.AddRange(structure.UsingStatements);
            content.Add("");
            content.AddRange(structure.Classes.Select(c => 
                $"class {c.Name} (Lines {c.StartLine}-{c.EndLine}) - {c.Methods.Count} methods"));
                
            return new FileReadResult
            {
                Success = true,
                Content = string.Join("\n", content),
                FilePath = filePath,
                ReadMethod = "Imports and Header"
            };
        }

        public async Task<FileReadResult> ReadSearchAsync(string filePath, string pattern, int contextLines = 3, bool useRegex = false)
        {
            try
            {
                string[] allLines = await File.ReadAllLinesAsync(ValidateAndResolvePath(filePath));
                var matchingLines = new List<(int lineNumber, string line)>();

                for (var i = 0; i < allLines.Length; i++)
                {
                    bool matches = useRegex 
                        ? Regex.IsMatch(allLines[i], pattern, RegexOptions.IgnoreCase)
                        : allLines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);

                    if (matches)
                        matchingLines.Add((i + 1, allLines[i]));
                }

                if (matchingLines.Count == 0)
                    return FileReadResult.Error($"Pattern '{pattern}' not found in {filePath}");

                var contentBuilder = new List<string>();
                var processedLines = new HashSet<int>();

                foreach ((int lineNumber, string line) in matchingLines)
                {
                    int startLine = Math.Max(1, lineNumber - contextLines);
                    int endLine = Math.Min(allLines.Length, lineNumber + contextLines);

                    for (int i = startLine; i <= endLine; i++)
                    {
                        if (!processedLines.Contains(i))
                        {
                            string prefix = i == lineNumber ? ">>> " : "    ";
                            contentBuilder.Add($"{prefix}{i,4}: {allLines[i - 1]}");
                            processedLines.Add(i);
                        }
                    }
                    
                    if (lineNumber < matchingLines.Last().lineNumber)
                        contentBuilder.Add("    ----");
                }

                return new FileReadResult
                {
                    Success = true,
                    Content = string.Join("\n", contentBuilder),
                    FilePath = filePath,
                    ReadMethod = $"Search: '{pattern}'",
                    Metadata = new Dictionary<string, object>
                    {
                        ["SearchPattern"] = pattern,
                        ["UseRegex"] = useRegex,
                        ["MatchCount"] = matchingLines.Count
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching pattern {Pattern} in {FilePath}", pattern, filePath);
                return FileReadResult.Error($"Error searching: {ex.Message}");
            }
        }

        public async Task<FileReadResult> ReadNextChunkAsync(string filePath, int startLine, int maxLines = 100)
        {
            int endLine = startLine + maxLines - 1;
            FileReadResult result = await ReadRangeAsync(filePath, startLine, endLine);
            
            if (result.Success)
            {
                result.ReadMethod = $"Chunk starting at line {startLine}";
                result.Metadata["ChunkSize"] = maxLines;
                result.Metadata["HasMoreLines"] = result.EndLine < result.TotalLines;
                result.Metadata["NextStartLine"] = result.EndLine + 1;
            }
            
            return result;
        }

        private async Task<SyntaxTree?> GetSyntaxTreeAsync(string filePath)
        {
            try
            {
                var cacheKey = $"syntax_tree_{filePath}_{File.GetLastWriteTime(filePath).Ticks}";
                
                if (_cache.TryGetValue(cacheKey, out SyntaxTree? cached))
                    return cached;

                if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return null;

                string sourceCode = await File.ReadAllTextAsync(filePath);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
                
                _cache.Set(cacheKey, syntaxTree, TimeSpan.FromMinutes(10));
                return syntaxTree;
            }
            catch
            {
                return null;
            }
        }
        
        private ClassInfo ExtractClassInfo(ClassDeclarationSyntax classDeclaration)
        {
            FileLinePositionSpan lineSpan = classDeclaration.GetLocation().GetLineSpan();
            return new ClassInfo
            {
                Name = classDeclaration.Identifier.ValueText,
                StartLine = lineSpan.StartLinePosition.Line + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                AccessModifier = GetAccessModifier(classDeclaration.Modifiers),
                Methods = classDeclaration.Members.OfType<MethodDeclarationSyntax>().Select(ExtractMethodInfo).ToList()
            };
        }
        
        private MethodInfo ExtractMethodInfo(MethodDeclarationSyntax method)
        {
            FileLinePositionSpan lineSpan = method.GetLocation().GetLineSpan();
            string className = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";
            
            return new MethodInfo
            {
                Name = method.Identifier.ValueText,
                ClassName = className,
                StartLine = lineSpan.StartLinePosition.Line + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                AccessModifier = GetAccessModifier(method.Modifiers),
                ReturnType = method.ReturnType.ToString(),
                IsAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                Signature = method.ToString().Split('\n').First().Trim(),
                ComplexityScore = CalculateComplexity(method)
            };
        }
        
        private static string GetAccessModifier(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return "public";
            if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) return "private";
            if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return "protected";
            if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) return "internal";
            return "private";
        }
        
        private static int CalculateComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1;
            List<SyntaxNode> descendants = method.DescendantNodes().ToList();
            
            complexity += descendants.OfType<IfStatementSyntax>().Count();
            complexity += descendants.OfType<WhileStatementSyntax>().Count();
            complexity += descendants.OfType<ForStatementSyntax>().Count();
            complexity += descendants.OfType<ForEachStatementSyntax>().Count();
            complexity += descendants.OfType<SwitchStatementSyntax>().Count();
            
            return complexity;
        }
        
        private string ValidateAndResolvePath(string path)
        {
            // Convert to absolute path - THIS IS THE KEY LINE
            string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_config.DefaultWorkspace, path);
            fullPath = Path.GetFullPath(fullPath);

            // Security checks can be added here if needed
            return fullPath;
        }    }
}
