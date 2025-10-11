using OfficeReader.Models.Excel;
using OfficeReader.Models.PowerPoint;
using OfficeReader.Models.Word;

namespace OfficeReader.Models;

public class OfficeDocument
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DocumentType Type { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public OfficeMetadata Metadata { get; set; } = new();
    public bool IsLoaded { get; set; }
    public string? LoadError { get; set; }
    
    // Document-specific content
    public WordContent? WordContent { get; set; }
    public ExcelContent? ExcelContent { get; set; }
    public PowerPointContent? PowerPointContent { get; set; }
}