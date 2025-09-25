using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using FuzzySharp;
using Microsoft.Extensions.Caching.Memory;
using McpCodeEditor.Models.Options;
using LuceneDocument = Lucene.Net.Documents.Document;

namespace McpCodeEditor.Services;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolType { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string Preview { get; set; } = string.Empty;
    public float Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SymbolReference
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ContainingType { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<Location> Definitions { get; set; } = [];
    public List<Location> References { get; set; } = [];
}

public class Location
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // Definition, Reference, Declaration
}

public class SearchService : IDisposable
{
    private readonly CodeEditorConfigurationService _config;
    private readonly IMemoryCache _cache;
    private LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;
    private Lucene.Net.Store.Directory? _indexDirectory;
    private IndexWriter? _indexWriter;
    private DirectoryReader? _indexReader;
    private IndexSearcher? _searcher;
    private readonly object _indexLock = new();
    private DateTime _lastIndexUpdate = DateTime.MinValue;
    private bool _isIndexing = false;

    public SearchService(CodeEditorConfigurationService config, IMemoryCache cache)
    {
        _config = config;
        _cache = cache;
        InitializeIndex();
    }

    private void InitializeIndex()
    {
        try
        {
            var indexPath = Path.Combine(Path.GetTempPath(), "McpCodeEditor", "Index");
            System.IO.Directory.CreateDirectory(indexPath);

            _indexDirectory = FSDirectory.Open(indexPath);
            var analyzer = new StandardAnalyzer(_luceneVersion);
            var config = new IndexWriterConfig(_luceneVersion, analyzer);
            _indexWriter = new IndexWriter(_indexDirectory, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize search index: {ex.Message}");
        }
    }

    /// <summary>
    /// Build or rebuild the search index for the current workspace
    /// </summary>
    public async Task<bool> RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_isIndexing) return false;

        lock (_indexLock)
        {
            if (_isIndexing) return false;
            _isIndexing = true;
        }

        try
        {
            if (_indexWriter == null) return false;

            _indexWriter.DeleteAll();
            _indexWriter.Commit();

            var workspacePath = _config.DefaultWorkspace;
            var files = System.IO.Directory.GetFiles(workspacePath, "*", SearchOption.AllDirectories)
                .Where(f => _config.AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !_config.ExcludedDirectories.Any(d => f.Contains(d)))
                .Where(f => new FileInfo(f).Length <= _config.MaxFileSize);

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await IndexFileAsync(filePath, cancellationToken);
            }

            _indexWriter.Commit();
            await RefreshSearcherAsync();
            _lastIndexUpdate = DateTime.Now;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Index rebuild failed: {ex.Message}");
            return false;
        }
        finally
        {
            _isIndexing = false;
        }
    }

    private async Task IndexFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath) || _indexWriter == null) return;

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Index file content
            var fileDoc = new LuceneDocument();
            fileDoc.Add(new StringField("type", "file", Field.Store.YES));
            fileDoc.Add(new StringField("path", filePath, Field.Store.YES));
            fileDoc.Add(new TextField("content", content, Field.Store.NO));
            fileDoc.Add(new StringField("extension", extension, Field.Store.YES));
            fileDoc.Add(new StringField("filename", Path.GetFileName(filePath), Field.Store.YES));

            _indexWriter.AddDocument(fileDoc);

            // Index C# symbols if this is a C# file
            if (extension == ".cs")
            {
                await IndexCSharpSymbolsAsync(filePath, content, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to index file {filePath}: {ex.Message}");
        }
    }

    private async Task IndexCSharpSymbolsAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        try
        {
            if (_indexWriter == null) return;

            var syntaxTree = CSharpSyntaxTree.ParseText(content, path: filePath, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Create a minimal compilation for symbol analysis
            var compilation = CSharpCompilation.Create("temp")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var sourceText = await syntaxTree.GetTextAsync(cancellationToken);

            // Index all symbols
            var symbols = root.DescendantNodes()
                .Where(n => n.IsKind(SyntaxKind.ClassDeclaration) ||
                           n.IsKind(SyntaxKind.MethodDeclaration) ||
                           n.IsKind(SyntaxKind.PropertyDeclaration) ||
                           n.IsKind(SyntaxKind.FieldDeclaration) ||
                           n.IsKind(SyntaxKind.InterfaceDeclaration) ||
                           n.IsKind(SyntaxKind.EnumDeclaration) ||
                           n.IsKind(SyntaxKind.StructDeclaration));

            foreach (var symbolNode in symbols)
            {
                var symbolInfo = semanticModel.GetDeclaredSymbol(symbolNode, cancellationToken: cancellationToken);
                if (symbolInfo == null) continue;

                var location = symbolNode.GetLocation();
                var linePosition = location.GetLineSpan().StartLinePosition;
                var lineText = sourceText.Lines[linePosition.Line].ToString().Trim();

                var symbolDoc = new LuceneDocument();
                symbolDoc.Add(new StringField("type", "symbol", Field.Store.YES));
                symbolDoc.Add(new StringField("path", filePath, Field.Store.YES));
                symbolDoc.Add(new StringField("name", symbolInfo.Name, Field.Store.YES));
                symbolDoc.Add(new StringField("kind", symbolInfo.Kind.ToString(), Field.Store.YES));
                symbolDoc.Add(new StringField("containing_type", symbolInfo.ContainingType?.Name ?? "", Field.Store.YES));
                symbolDoc.Add(new StringField("namespace", symbolInfo.ContainingNamespace?.ToDisplayString() ?? "", Field.Store.YES));
                symbolDoc.Add(new Int32Field("line", linePosition.Line + 1, Field.Store.YES));
                symbolDoc.Add(new Int32Field("column", linePosition.Character + 1, Field.Store.YES));
                symbolDoc.Add(new TextField("preview", lineText, Field.Store.YES));
                symbolDoc.Add(new TextField("searchable", $"{symbolInfo.Name} {symbolInfo.Kind} {symbolInfo.ContainingType?.Name ?? ""}", Field.Store.NO));

                _indexWriter.AddDocument(symbolDoc);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to index C# symbols in {filePath}: {ex.Message}");
        }
    }

    private Task RefreshSearcherAsync()
    {
        try
        {
            if (_indexDirectory == null) return Task.CompletedTask;

            _indexReader?.Dispose();
            _indexReader = DirectoryReader.Open(_indexDirectory);
            _searcher = new IndexSearcher(_indexReader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to refresh searcher: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Search for symbols by name with optional fuzzy matching
    /// </summary>
    public async Task<List<SearchResult>> SearchSymbolsAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SearchOptions();

        if (_searcher == null)
        {
            await RebuildIndexAsync(cancellationToken);
            if (_searcher == null) return [];
        }

        var results = new List<SearchResult>();

        try
        {
            // Lucene search for exact/partial matches
            var luceneResults = PerformLuceneSearch("symbol", query, options);
            results.AddRange(luceneResults);

            // Fuzzy search if enabled
            if (options.UseFuzzyMatch)
            {
                var fuzzyResults = PerformFuzzySearch("symbol", query, options);
                results.AddRange(fuzzyResults);
            }

            // Remove duplicates and sort by score
            results = results
                .GroupBy(r => $"{r.FilePath}:{r.LineNumber}:{r.SymbolName}")
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(options.MaxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Symbol search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Search for text across all files
    /// </summary>
    public async Task<List<SearchResult>> SearchTextAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SearchOptions();

        if (_searcher == null)
        {
            await RebuildIndexAsync(cancellationToken);
            if (_searcher == null) return [];
        }

        var results = new List<SearchResult>();

        try
        {
            var luceneResults = PerformLuceneSearch("file", query, options);
            results.AddRange(luceneResults);

            if (options.UseFuzzyMatch)
            {
                var fuzzyResults = PerformFuzzySearch("file", query, options);
                results.AddRange(fuzzyResults);
            }

            results = results
                .GroupBy(r => $"{r.FilePath}:{r.LineNumber}")
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(options.MaxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Text search failed: {ex.Message}");
        }

        return results;
    }

    private List<SearchResult> PerformLuceneSearch(string type, string query, SearchOptions options)
    {
        var results = new List<SearchResult>();

        if (_searcher == null) return results;

        try
        {
            var analyzer = new StandardAnalyzer(_luceneVersion);
            var parser = new QueryParser(_luceneVersion,
                type == "symbol" ? "searchable" : "content", analyzer);

            var luceneQuery = parser.Parse(query);
            var filter = new BooleanQuery();
            filter.Add(new TermQuery(new Term("type", type)), Occur.MUST);

            var finalQuery = new BooleanQuery();
            finalQuery.Add(luceneQuery, Occur.MUST);
            finalQuery.Add(filter, Occur.MUST);

            var topDocs = _searcher.Search(finalQuery, options.MaxResults);

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = _searcher.Doc(scoreDoc.Doc);

                var result = new SearchResult
                {
                    FilePath = doc.Get("path") ?? "",
                    Score = scoreDoc.Score,
                    LineNumber = int.TryParse(doc.Get("line"), out var line) ? line : 0,
                    Column = int.TryParse(doc.Get("column"), out var col) ? col : 0,
                    Preview = doc.Get("preview") ?? ""
                };

                if (type == "symbol")
                {
                    result.SymbolName = doc.Get("name") ?? "";
                    result.SymbolType = doc.Get("kind") ?? "";
                    result.Metadata["containingType"] = doc.Get("containing_type") ?? "";
                    result.Metadata["namespace"] = doc.Get("namespace") ?? "";
                }

                results.Add(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lucene search failed: {ex.Message}");
        }

        return results;
    }

    private List<SearchResult> PerformFuzzySearch(string type, string query, SearchOptions options)
    {
        var results = new List<SearchResult>();

        if (_searcher == null) return results;

        try
        {
            // Get all documents of the specified type for fuzzy matching
            var allDocsQuery = new TermQuery(new Term("type", type));
            var topDocs = _searcher.Search(allDocsQuery, 10000); // Get more docs for fuzzy matching

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = _searcher.Doc(scoreDoc.Doc);
                var targetText = type == "symbol" ? doc.Get("name") : doc.Get("content");

                if (string.IsNullOrEmpty(targetText)) continue;

                var ratio = Fuzz.Ratio(query.ToLowerInvariant(), targetText.ToLowerInvariant());

                if (ratio >= options.FuzzyThreshold)
                {
                    var result = new SearchResult
                    {
                        FilePath = doc.Get("path") ?? "",
                        Score = ratio / 100f,
                        LineNumber = int.TryParse(doc.Get("line"), out var line) ? line : 0,
                        Column = int.TryParse(doc.Get("column"), out var col) ? col : 0,
                        Preview = doc.Get("preview") ?? ""
                    };

                    if (type == "symbol")
                    {
                        result.SymbolName = doc.Get("name") ?? "";
                        result.SymbolType = doc.Get("kind") ?? "";
                        result.Metadata["containingType"] = doc.Get("containing_type") ?? "";
                        result.Metadata["namespace"] = doc.Get("namespace") ?? "";
                        result.Metadata["fuzzyRatio"] = ratio;
                    }

                    results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fuzzy search failed: {ex.Message}");
        }

        return results;
    }

    public void Dispose()
    {
        _indexWriter?.Dispose();
        _indexReader?.Dispose();
        _indexDirectory?.Dispose();
    }
}
