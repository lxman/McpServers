using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services.Advanced;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools.Advanced
{
    [McpServerToolType]
    public class AdvancedFileReaderTools(IAdvancedFileReaderService fileReader) : BaseToolClass
    {
        #region Line Range Reading Tools

        [McpServerTool]
        [Description("Read specific line range from a file with rich metadata - no more truncation!")]
        public async Task<string> AdvancedFileReadRangeAsync(string filePath, int startLine, int endLine)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadRangeAsync(filePath, startLine, endLine);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    startLine = result.StartLine,
                    endLine = result.EndLine,
                    totalLines = result.TotalLines,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        [McpServerTool]
        [Description("Read lines around a specific line number with context")]
        public async Task<string> AdvancedFileReadAroundLineAsync(string filePath, int lineNumber, int contextLines = 10)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadAroundLineAsync(filePath, lineNumber, contextLines);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    startLine = result.StartLine,
                    endLine = result.EndLine,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        [McpServerTool]
        [Description("Read next chunk of lines from a file for incremental processing")]
        public async Task<string> AdvancedFileReadNextChunkAsync(string filePath, int startLine, int maxLines = 100)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadNextChunkAsync(filePath, startLine, maxLines);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    startLine = result.StartLine,
                    endLine = result.EndLine,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        #endregion

        #region Roslyn-Powered Code Analysis Tools

        [McpServerTool]
        [Description("Read a specific method from a C# file with context using Roslyn analysis")]
        public async Task<string> AdvancedFileReadMethodAsync(string filePath, string methodName, int contextLines = 5)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadMethodAsync(filePath, methodName, contextLines);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    startLine = result.StartLine,
                    endLine = result.EndLine,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        [McpServerTool]
        [Description("Read a complete class from a C# file using Roslyn analysis")]
        public async Task<string> AdvancedFileReadClassAsync(string filePath, string className)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadClassAsync(filePath, className);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    startLine = result.StartLine,
                    endLine = result.EndLine,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        [McpServerTool]
        [Description("Get comprehensive structural outline of a C# file using Roslyn analysis")]
        public async Task<string> AdvancedFileGetOutlineAsync(string filePath)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.GetFileOutlineAsync(filePath);
                
                return new
                {
                    success = result.Success,
                    filePath = result.FilePath,
                    classes = result.Classes.Select(c => new
                    {
                        name = c.Name,
                        startLine = c.StartLine,
                        endLine = c.EndLine,
                        accessModifier = c.AccessModifier,
                        methodCount = c.Methods.Count,
                        methods = c.Methods.Select(m => new
                        {
                            name = m.Name,
                            startLine = m.StartLine,
                            endLine = m.EndLine,
                            accessModifier = m.AccessModifier,
                            returnType = m.ReturnType,
                            isAsync = m.IsAsync,
                            complexityScore = m.ComplexityScore,
                            signature = m.Signature
                        })
                    }),
                    allMethods = result.Methods.Select(m => new
                    {
                        name = m.Name,
                        className = m.ClassName,
                        startLine = m.StartLine,
                        endLine = m.EndLine,
                        signature = m.Signature,
                        complexityScore = m.ComplexityScore
                    }),
                    usingStatements = result.UsingStatements,
                    warnings = result.Warnings,
                    summary = new
                    {
                        totalClasses = result.Classes.Count,
                        totalMethods = result.Methods.Count,
                        averageMethodComplexity = result.Methods.Count != 0 ? 
                            Math.Round(result.Methods.Average(m => m.ComplexityScore), 1) : 0,
                        mostComplexMethod = result.Methods
                            .OrderByDescending(m => m.ComplexityScore)
                            .FirstOrDefault()?.Name ?? "None"
                    }
                };
            });
        }

        [McpServerTool]
        [Description("Get all method signatures from a C# file with line numbers")]
        public async Task<string> AdvancedFileReadMethodSignaturesAsync(string filePath)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadMethodSignaturesAsync(filePath);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        [McpServerTool]
        [Description("Read using statements and class headers from a C# file")]
        public async Task<string> AdvancedFileReadImportsHeaderAsync(string filePath)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadImportsAndHeaderAsync(filePath);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        #endregion

        #region Search-Based Reading Tools

        [McpServerTool]
        [Description("Search for text patterns in a file and return matching lines with context")]
        public async Task<string> AdvancedFileSearchAsync(string filePath, string pattern, int contextLines = 3, bool useRegex = false)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await fileReader.ReadSearchAsync(filePath, pattern, contextLines, useRegex);
                
                return new
                {
                    success = result.Success,
                    content = result.Content,
                    filePath = result.FilePath,
                    readMethod = result.ReadMethod,
                    warnings = result.Warnings,
                    metadata = result.Metadata
                };
            });
        }

        #endregion
    }
}
