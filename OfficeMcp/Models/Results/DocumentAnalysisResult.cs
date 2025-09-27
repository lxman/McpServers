using OfficeMcp.Models.Excel;
using OfficeMcp.Models.PowerPoint;
using OfficeMcp.Models.Word;

namespace OfficeMcp.Models.Results;

public class DocumentAnalysisResult
{
    public long FileSize { get; set; }
    public string FilePath { get; set; } = "";
    public DocumentType DocumentType { get; set; }
    public OfficeMetadata Metadata { get; set; } = new();
    public DateTime LastModified { get; set; }
    
    public Dictionary<string, object> Statistics { get; set; } = new();
    
    // Type-specific analysis
    public WordAnalysis? WordAnalysis { get; set; }
    public ExcelAnalysis? ExcelAnalysis { get; set; }
    public PowerPointAnalysis? PowerPointAnalysis { get; set; }
}