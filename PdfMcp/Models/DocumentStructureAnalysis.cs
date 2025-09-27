namespace PdfMcp.Models;

public class DocumentStructureAnalysis
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public int PageCount { get; set; }
    public int TotalWords { get; set; }
    public int TotalCharacters { get; set; }
    public double AverageWordsPerPage { get; set; }
    public bool HasImages { get; set; }
    public bool HasAnnotations { get; set; }
    public bool HasLinks { get; set; }
    public int ImageCount { get; set; }
    public int AnnotationCount { get; set; }
    public int LinkCount { get; set; }
    public List<PageAnalysis> Pages { get; set; } = [];
}