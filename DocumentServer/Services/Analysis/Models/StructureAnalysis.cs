namespace DocumentServer.Services.Analysis.Models;

/// <summary>
/// Results of document structure analysis
/// </summary>
public class StructureAnalysis
{
    /// <summary>
    /// Full path to the analyzed document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Document type
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Number of pages (for paginated documents)
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// Total character count
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Character count excluding spaces
    /// </summary>
    public int CharacterCountNoSpaces { get; set; }

    /// <summary>
    /// Total word count
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// Number of lines
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Number of empty lines
    /// </summary>
    public int EmptyLineCount { get; set; }

    /// <summary>
    /// Approximate sentence count
    /// </summary>
    public int SentenceCount { get; set; }

    /// <summary>
    /// Approximate paragraph count
    /// </summary>
    public int ParagraphCount { get; set; }

    /// <summary>
    /// Average word length
    /// </summary>
    public double AverageWordLength { get; set; }

    /// <summary>
    /// Average words per line
    /// </summary>
    public double AverageWordsPerLine { get; set; }

    /// <summary>
    /// Average words per sentence
    /// </summary>
    public double AverageWordsPerSentence { get; set; }

    /// <summary>
    /// Readability score (Flesch Reading Ease approximation)
    /// </summary>
    public double ReadabilityScore { get; set; }

    /// <summary>
    /// Top 10 most frequent words
    /// </summary>
    public Dictionary<string, int> TopWords { get; set; } = new();

    /// <summary>
    /// Document metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Structured content (format-specific)
    /// </summary>
    public object? StructuredContent { get; set; }
}