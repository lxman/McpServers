using System.Security.Cryptography;
using System.Text;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Roslyn-based chunker for C# code.
/// Extracts classes, methods, properties, and other meaningful syntax nodes.
/// </summary>
public sealed class CSharpChunker : ICodeChunker
{
    private readonly CodeAssistOptions _options;
    private readonly ILogger<CSharpChunker> _logger;

    private static readonly HashSet<string> SupportedLangs = ["csharp", "cs", "c#"];

    public CSharpChunker(
        IOptions<CodeAssistOptions> options,
        ILogger<CSharpChunker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlySet<string> SupportedLanguages => SupportedLangs;

    public bool SupportsLanguage(string language) =>
        SupportedLangs.Contains(language.ToLowerInvariant());

    public IReadOnlyList<CodeChunk> ChunkCode(string content, string filePath, string relativePath, string language)
    {
        var chunks = new List<CodeChunk>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Extract namespace-level members
            foreach (var member in root.Members)
            {
                ExtractChunks(member, filePath, relativePath, content, chunks, null);
            }

            // If no chunks were extracted (e.g., script file), create a file-level chunk
            if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(CreateChunk(content, filePath, relativePath, 1, CountLines(content), "file", null, null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse C# file {FilePath}, falling back to file-level chunk", filePath);

            // Fallback to file-level chunk
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(CreateChunk(content, filePath, relativePath, 1, CountLines(content), "file", null, null));
            }
        }

        return chunks;
    }

    private void ExtractChunks(
        SyntaxNode node,
        string filePath,
        string relativePath,
        string fullContent,
        List<CodeChunk> chunks,
        string? parentSymbol)
    {
        switch (node)
        {
            case NamespaceDeclarationSyntax ns:
                foreach (var member in ns.Members)
                {
                    ExtractChunks(member, filePath, relativePath, fullContent, chunks, ns.Name.ToString());
                }
                break;

            case FileScopedNamespaceDeclarationSyntax fsns:
                foreach (var member in fsns.Members)
                {
                    ExtractChunks(member, filePath, relativePath, fullContent, chunks, fsns.Name.ToString());
                }
                break;

            case ClassDeclarationSyntax cls:
                ExtractTypeDeclaration(cls, filePath, relativePath, fullContent, chunks, parentSymbol, "class");
                break;

            case RecordDeclarationSyntax rec:
                ExtractTypeDeclaration(rec, filePath, relativePath, fullContent, chunks, parentSymbol, "record");
                break;

            case StructDeclarationSyntax str:
                ExtractTypeDeclaration(str, filePath, relativePath, fullContent, chunks, parentSymbol, "struct");
                break;

            case InterfaceDeclarationSyntax iface:
                ExtractTypeDeclaration(iface, filePath, relativePath, fullContent, chunks, parentSymbol, "interface");
                break;

            case EnumDeclarationSyntax enm:
                var enumSpan = enm.GetLocation().GetLineSpan();
                var enumContent = enm.ToFullString().Trim();
                chunks.Add(CreateChunk(
                    enumContent, filePath, relativePath,
                    enumSpan.StartLinePosition.Line + 1,
                    enumSpan.EndLinePosition.Line + 1,
                    "enum", enm.Identifier.Text, parentSymbol));
                break;

            case DelegateDeclarationSyntax del:
                var delSpan = del.GetLocation().GetLineSpan();
                var delContent = del.ToFullString().Trim();
                chunks.Add(CreateChunk(
                    delContent, filePath, relativePath,
                    delSpan.StartLinePosition.Line + 1,
                    delSpan.EndLinePosition.Line + 1,
                    "delegate", del.Identifier.Text, parentSymbol));
                break;

            case GlobalStatementSyntax:
                // Skip global statements, they'll be captured in file-level chunk if needed
                break;
        }
    }

    private void ExtractTypeDeclaration(
        TypeDeclarationSyntax typeDecl,
        string filePath,
        string relativePath,
        string fullContent,
        List<CodeChunk> chunks,
        string? parentSymbol,
        string typeName)
    {
        var typeSpan = typeDecl.GetLocation().GetLineSpan();
        var typeContent = typeDecl.ToFullString().Trim();
        var typeSymbol = typeDecl.Identifier.Text;

        // If the type is small enough, add it as a single chunk
        if (typeContent.Length <= _options.MaxChunkSize)
        {
            chunks.Add(CreateChunk(
                typeContent, filePath, relativePath,
                typeSpan.StartLinePosition.Line + 1,
                typeSpan.EndLinePosition.Line + 1,
                typeName, typeSymbol, parentSymbol));
            return;
        }

        // For larger types, extract individual members
        // First, add the type signature (without body)
        var signatureEnd = typeDecl.OpenBraceToken.SpanStart;
        var signature = fullContent[typeDecl.SpanStart..signatureEnd].Trim() + " { ... }";
        var sigSpan = typeDecl.GetLocation().GetLineSpan();

        chunks.Add(CreateChunk(
            signature, filePath, relativePath,
            sigSpan.StartLinePosition.Line + 1,
            sigSpan.StartLinePosition.Line + 1,
            $"{typeName}_signature", typeSymbol, parentSymbol));

        // Extract members
        foreach (var member in typeDecl.Members)
        {
            ExtractMember(member, filePath, relativePath, fullContent, chunks, typeSymbol);
        }
    }

    private void ExtractMember(
        MemberDeclarationSyntax member,
        string filePath,
        string relativePath,
        string fullContent,
        List<CodeChunk> chunks,
        string parentSymbol)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                AddMemberChunk(method, method.Identifier.Text, "method", filePath, relativePath, chunks, parentSymbol);
                break;

            case ConstructorDeclarationSyntax ctor:
                AddMemberChunk(ctor, ctor.Identifier.Text, "constructor", filePath, relativePath, chunks, parentSymbol);
                break;

            case PropertyDeclarationSyntax prop:
                AddMemberChunk(prop, prop.Identifier.Text, "property", filePath, relativePath, chunks, parentSymbol);
                break;

            case FieldDeclarationSyntax field:
                var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "field";
                AddMemberChunk(field, fieldName, "field", filePath, relativePath, chunks, parentSymbol);
                break;

            case EventDeclarationSyntax evt:
                AddMemberChunk(evt, evt.Identifier.Text, "event", filePath, relativePath, chunks, parentSymbol);
                break;

            case IndexerDeclarationSyntax indexer:
                AddMemberChunk(indexer, "this[]", "indexer", filePath, relativePath, chunks, parentSymbol);
                break;

            case OperatorDeclarationSyntax op:
                AddMemberChunk(op, $"operator {op.OperatorToken}", "operator", filePath, relativePath, chunks, parentSymbol);
                break;

            case TypeDeclarationSyntax nestedType:
                // Recursively handle nested types
                var nestedTypeName = nestedType switch
                {
                    ClassDeclarationSyntax => "class",
                    RecordDeclarationSyntax => "record",
                    StructDeclarationSyntax => "struct",
                    InterfaceDeclarationSyntax => "interface",
                    _ => "type"
                };
                ExtractTypeDeclaration(nestedType, filePath, relativePath, fullContent, chunks, parentSymbol, nestedTypeName);
                break;
        }
    }

    private void AddMemberChunk(
        SyntaxNode node,
        string symbolName,
        string chunkType,
        string filePath,
        string relativePath,
        List<CodeChunk> chunks,
        string parentSymbol)
    {
        var span = node.GetLocation().GetLineSpan();
        var content = node.ToFullString().Trim();

        // If content is too large, we might need to split it further
        // For now, just add it as-is (methods rarely exceed max chunk size)
        chunks.Add(CreateChunk(
            content, filePath, relativePath,
            span.StartLinePosition.Line + 1,
            span.EndLinePosition.Line + 1,
            chunkType, symbolName, parentSymbol));
    }

    private CodeChunk CreateChunk(
        string content,
        string filePath,
        string relativePath,
        int startLine,
        int endLine,
        string chunkType,
        string? symbolName,
        string? parentSymbol)
    {
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            RelativePath = relativePath,
            Content = content,
            StartLine = startLine,
            EndLine = endLine,
            ChunkType = chunkType,
            SymbolName = symbolName,
            ParentSymbol = parentSymbol,
            Language = "csharp",
            ContentHash = ComputeHash(content)
        };
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int CountLines(string content) =>
        content.Count(c => c == '\n') + 1;
}
