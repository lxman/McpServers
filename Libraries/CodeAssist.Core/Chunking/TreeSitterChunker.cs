using System.Security.Cryptography;
using System.Text;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeSitter;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Code chunker that uses tree-sitter for semantic AST-based chunking.
/// Extracts meaningful code units (classes, functions, methods) with their full context.
/// </summary>
public sealed class TreeSitterChunker : ICodeChunker, IDisposable
{
    private readonly CodeAssistOptions _options;
    private readonly ILogger<TreeSitterChunker> _logger;
    private readonly Dictionary<string, Language> _languages = new();
    private readonly object _languageLock = new();
    private bool _disposed;

    /// <summary>
    /// Maps our language identifiers to tree-sitter language names.
    /// </summary>
    private static readonly Dictionary<string, string> LanguageMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "C#",
        ["python"] = "Python",
        ["javascript"] = "JavaScript",
        ["typescript"] = "TypeScript",
        ["go"] = "Go",
        ["rust"] = "Rust",
        ["java"] = "Java",
        ["c"] = "C",
        ["cpp"] = "C++",
        ["ruby"] = "Ruby",
        ["php"] = "PHP",
        ["swift"] = "Swift",
        ["bash"] = "Bash",
        ["shell"] = "Bash",
        ["json"] = "JSON",
        ["html"] = "HTML",
        ["css"] = "CSS"
    };

    /// <summary>
    /// Query patterns for extracting semantic chunks from each language.
    /// These patterns target top-level declarations and nested members.
    /// </summary>
    private static readonly Dictionary<string, string[]> LanguageQueries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = [
            "(class_declaration name: (identifier) @name) @class",
            "(struct_declaration name: (identifier) @name) @struct",
            "(interface_declaration name: (identifier) @name) @interface",
            "(record_declaration name: (identifier) @name) @record",
            "(enum_declaration name: (identifier) @name) @enum",
            "(method_declaration name: (identifier) @name) @method",
            "(constructor_declaration name: (identifier) @name) @constructor",
            "(property_declaration name: (identifier) @name) @property"
        ],
        ["python"] = [
            "(class_definition name: (identifier) @name) @class",
            "(function_definition name: (identifier) @name) @function"
        ],
        ["javascript"] = [
            "(class_declaration name: (identifier) @name) @class",
            "(function_declaration name: (identifier) @name) @function",
            "(method_definition name: (property_identifier) @name) @method",
            "(arrow_function) @arrow",
            "(variable_declaration) @variable"
        ],
        ["typescript"] = [
            "(class_declaration name: (type_identifier) @name) @class",
            "(function_declaration name: (identifier) @name) @function",
            "(method_definition name: (property_identifier) @name) @method",
            "(interface_declaration name: (type_identifier) @name) @interface",
            "(type_alias_declaration name: (type_identifier) @name) @type"
        ],
        ["go"] = [
            "(function_declaration name: (identifier) @name) @function",
            "(method_declaration name: (field_identifier) @name) @method",
            "(type_declaration (type_spec name: (type_identifier) @name)) @type"
        ],
        ["rust"] = [
            "(function_item name: (identifier) @name) @function",
            "(impl_item) @impl",
            "(struct_item name: (type_identifier) @name) @struct",
            "(enum_item name: (type_identifier) @name) @enum",
            "(trait_item name: (type_identifier) @name) @trait"
        ],
        ["java"] = [
            "(class_declaration name: (identifier) @name) @class",
            "(interface_declaration name: (identifier) @name) @interface",
            "(method_declaration name: (identifier) @name) @method",
            "(constructor_declaration name: (identifier) @name) @constructor",
            "(enum_declaration name: (identifier) @name) @enum"
        ],
        ["c"] = [
            "(function_definition declarator: (function_declarator declarator: (identifier) @name)) @function",
            "(struct_specifier name: (type_identifier) @name) @struct",
            "(enum_specifier name: (type_identifier) @name) @enum"
        ],
        ["cpp"] = [
            "(function_definition declarator: (function_declarator declarator: (identifier) @name)) @function",
            "(class_specifier name: (type_identifier) @name) @class",
            "(struct_specifier name: (type_identifier) @name) @struct"
        ],
        ["ruby"] = [
            "(class name: (constant) @name) @class",
            "(method name: (identifier) @name) @method",
            "(module name: (constant) @name) @module"
        ],
        ["php"] = [
            "(class_declaration name: (name) @name) @class",
            "(function_definition name: (name) @name) @function",
            "(method_declaration name: (name) @name) @method"
        ]
    };

    private static readonly HashSet<string> SupportedLangs = new(LanguageMapping.Keys, StringComparer.OrdinalIgnoreCase);

    public TreeSitterChunker(IOptions<CodeAssistOptions> options, ILogger<TreeSitterChunker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlySet<string> SupportedLanguages => SupportedLangs;

    public bool SupportsLanguage(string language)
    {
        return LanguageMapping.ContainsKey(language);
    }

    public IReadOnlyList<CodeChunk> ChunkCode(string content, string filePath, string relativePath, string language)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        if (!LanguageMapping.TryGetValue(language, out var treeSitterLang))
        {
            _logger.LogDebug("Language {Language} not supported by tree-sitter, returning empty", language);
            return [];
        }

        try
        {
            var chunks = ParseWithTreeSitter(content, filePath, relativePath, language, treeSitterLang);

            if (chunks.Count == 0)
            {
                // Fall back to file-level chunk if no semantic chunks found
                _logger.LogDebug("No semantic chunks found for {File}, creating file-level chunk", relativePath);
                return [CreateFileChunk(content, filePath, relativePath, language)];
            }

            _logger.LogDebug("Created {Count} semantic chunks for {File}", chunks.Count, relativePath);
            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tree-sitter parsing failed for {File}, creating file-level chunk", relativePath);
            return [CreateFileChunk(content, filePath, relativePath, language)];
        }
    }

    private List<CodeChunk> ParseWithTreeSitter(
        string content,
        string filePath,
        string relativePath,
        string language,
        string treeSitterLang)
    {
        var chunks = new List<CodeChunk>();
        var lang = GetOrCreateLanguage(treeSitterLang);

        using var parser = new Parser(lang);
        using var tree = parser.Parse(content);

        if (tree == null)
        {
            _logger.LogWarning("Failed to parse {File} with tree-sitter", relativePath);
            return chunks;
        }

        var lines = content.Split('\n');

        // Try query-based extraction first
        if (LanguageQueries.TryGetValue(language, out var queries))
        {
            foreach (var queryPattern in queries)
            {
                try
                {
                    ExtractChunksWithQuery(tree, lang, queryPattern, content, lines, filePath, relativePath, language, chunks);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Query failed for pattern {Pattern} in {File}", queryPattern, relativePath);
                }
            }
        }

        // If no query results, fall back to walking top-level nodes
        if (chunks.Count == 0)
        {
            ExtractTopLevelNodes(tree.RootNode, content, lines, filePath, relativePath, language, chunks);
        }

        // Deduplicate overlapping chunks (keep larger ones)
        return DeduplicateChunks(chunks);
    }

    private void ExtractChunksWithQuery(
        Tree tree,
        Language lang,
        string queryPattern,
        string content,
        string[] lines,
        string filePath,
        string relativePath,
        string language,
        List<CodeChunk> chunks)
    {
        using var query = new Query(lang, queryPattern);
        var result = query.Execute(tree.RootNode);

        foreach (var capture in result.Captures)
        {
            var captureName = capture.Name;
            var node = capture.Node;

            // Skip if this is just the name capture
            if (captureName == "name")
                continue;

            var startLine = (int)node.StartPosition.Row + 1;
            var endLine = (int)node.EndPosition.Row + 1;
            var nodeContent = node.Text;
            var chunkType = captureName;

            // Try to get symbol name from children
            string? symbolName = null;
            using var cursor = node.Walk();
            if (cursor.GotoFirstChild())
            {
                do
                {
                    var childNode = cursor.CurrentNode;
                    if (childNode.Type == "identifier" ||
                        childNode.Type == "type_identifier" ||
                        childNode.Type == "name" ||
                        childNode.Type == "property_identifier" ||
                        childNode.Type == "constant")
                    {
                        symbolName = childNode.Text;
                        break;
                    }
                } while (cursor.GotoNextSibling());
            }

            // Skip if chunk is too small
            if (string.IsNullOrWhiteSpace(nodeContent) || nodeContent.Length < 10)
                continue;

            // Skip if chunk exceeds max size - we'll need to split it
            if (nodeContent.Length > _options.MaxChunkSize * 2)
            {
                // For very large chunks, create sub-chunks
                var subChunks = SplitLargeChunk(nodeContent, filePath, relativePath, startLine, language, symbolName, chunkType);
                chunks.AddRange(subChunks);
            }
            else
            {
                chunks.Add(new CodeChunk
                {
                    Id = Guid.NewGuid(),
                    FilePath = filePath,
                    RelativePath = relativePath,
                    Content = nodeContent,
                    StartLine = startLine,
                    EndLine = endLine,
                    ChunkType = chunkType,
                    SymbolName = symbolName,
                    ParentSymbol = null, // Could be enhanced to track parent
                    Language = language.ToLowerInvariant(),
                    ContentHash = ComputeHash(nodeContent)
                });
            }
        }
    }

    private void ExtractTopLevelNodes(
        Node rootNode,
        string content,
        string[] lines,
        string filePath,
        string relativePath,
        string language,
        List<CodeChunk> chunks)
    {
        // Walk immediate children of root for top-level declarations
        using var cursor = rootNode.Walk();

        if (!cursor.GotoFirstChild())
            return;

        do
        {
            var node = cursor.CurrentNode;
            var nodeType = node.Type;

            // Skip comments, whitespace, etc.
            if (IsSkippableNode(nodeType))
                continue;

            var nodeContent = node.Text;
            if (string.IsNullOrWhiteSpace(nodeContent) || nodeContent.Length < 20)
                continue;

            var startLine = (int)node.StartPosition.Row + 1;
            var endLine = (int)node.EndPosition.Row + 1;

            chunks.Add(new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                RelativePath = relativePath,
                Content = nodeContent,
                StartLine = startLine,
                EndLine = endLine,
                ChunkType = NormalizeNodeType(nodeType),
                SymbolName = null,
                ParentSymbol = null,
                Language = language.ToLowerInvariant(),
                ContentHash = ComputeHash(nodeContent)
            });
        } while (cursor.GotoNextSibling());
    }

    private List<CodeChunk> SplitLargeChunk(
        string content,
        string filePath,
        string relativePath,
        int baseStartLine,
        string language,
        string? symbolName,
        string? chunkType)
    {
        var chunks = new List<CodeChunk>();
        var lines = content.Split('\n');
        var linesPerChunk = Math.Max(10, _options.MaxChunkSize / 60);
        var overlapLines = Math.Max(2, _options.ChunkOverlap / 60);

        var startLine = 0;
        var partNumber = 1;

        while (startLine < lines.Length)
        {
            var endLine = Math.Min(startLine + linesPerChunk, lines.Length);
            var chunkLines = lines[startLine..endLine];
            var chunkContent = string.Join('\n', chunkLines);

            chunks.Add(new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                RelativePath = relativePath,
                Content = chunkContent,
                StartLine = baseStartLine + startLine,
                EndLine = baseStartLine + endLine - 1,
                ChunkType = $"{chunkType ?? "segment"}_part{partNumber}",
                SymbolName = symbolName != null ? $"{symbolName} (part {partNumber})" : null,
                ParentSymbol = symbolName,
                Language = language.ToLowerInvariant(),
                ContentHash = ComputeHash(chunkContent)
            });

            var nextStart = endLine - overlapLines;
            if (nextStart <= startLine)
                nextStart = startLine + 1;

            startLine = nextStart;
            partNumber++;
        }

        return chunks;
    }

    private List<CodeChunk> DeduplicateChunks(List<CodeChunk> chunks)
    {
        if (chunks.Count <= 1)
            return chunks;

        // Sort by start line, then by size (larger first)
        var sorted = chunks
            .OrderBy(c => c.StartLine)
            .ThenByDescending(c => c.EndLine - c.StartLine)
            .ToList();

        var result = new List<CodeChunk>();
        var coveredRanges = new List<(int Start, int End)>();

        foreach (var chunk in sorted)
        {
            // Check if this chunk is fully contained within an existing chunk
            var isContained = coveredRanges.Any(r =>
                chunk.StartLine >= r.Start && chunk.EndLine <= r.End);

            if (!isContained)
            {
                result.Add(chunk);
                coveredRanges.Add((chunk.StartLine, chunk.EndLine));
            }
        }

        return result;
    }

    private Language GetOrCreateLanguage(string treeSitterLang)
    {
        lock (_languageLock)
        {
            if (_languages.TryGetValue(treeSitterLang, out var existing))
                return existing;

            var lang = new Language(treeSitterLang);
            _languages[treeSitterLang] = lang;
            return lang;
        }
    }

    private CodeChunk CreateFileChunk(string content, string filePath, string relativePath, string language)
    {
        var lines = content.Split('\n');
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            RelativePath = relativePath,
            Content = content.Length > _options.MaxChunkSize
                ? content[.._options.MaxChunkSize]
                : content,
            StartLine = 1,
            EndLine = lines.Length,
            ChunkType = "file",
            SymbolName = Path.GetFileName(filePath),
            ParentSymbol = null,
            Language = language.ToLowerInvariant(),
            ContentHash = ComputeHash(content)
        };
    }

    private static bool IsSkippableNode(string nodeType)
    {
        return nodeType is "comment" or "line_comment" or "block_comment"
            or "whitespace" or "newline" or "ERROR";
    }

    private static string NormalizeNodeType(string nodeType)
    {
        // Normalize common node types to consistent names
        return nodeType switch
        {
            "function_definition" or "function_declaration" or "function_item" => "function",
            "class_definition" or "class_declaration" or "class_specifier" => "class",
            "method_definition" or "method_declaration" => "method",
            "struct_specifier" or "struct_item" or "struct_declaration" => "struct",
            "interface_declaration" => "interface",
            "enum_specifier" or "enum_item" or "enum_declaration" => "enum",
            "impl_item" => "impl",
            "trait_item" => "trait",
            "module" => "module",
            _ => nodeType.Replace("_", " ")
        };
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_languageLock)
        {
            foreach (var lang in _languages.Values)
            {
                lang.Dispose();
            }
            _languages.Clear();
        }

        _disposed = true;
    }
}
