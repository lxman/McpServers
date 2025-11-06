namespace DocumentServer.Core.Models.Common;

/// <summary>
/// Represents the type of document being processed
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Unknown or unsupported document type
    /// </summary>
    Unknown,
    
    /// <summary>
    /// PDF document (.pdf)
    /// </summary>
    Pdf,
    
    /// <summary>
    /// Microsoft Word document (.doc, .docx)
    /// </summary>
    Word,
    
    /// <summary>
    /// Microsoft Excel document (.xls, .xlsx)
    /// </summary>
    Excel,
    
    /// <summary>
    /// Microsoft PowerPoint document (.ppt, .pptx)
    /// </summary>
    PowerPoint,
    
    /// <summary>
    /// Image file (.png, .jpg, .jpeg, .tiff, etc.)
    /// </summary>
    Image
}
