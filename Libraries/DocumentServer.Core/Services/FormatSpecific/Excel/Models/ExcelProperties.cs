namespace DocumentServer.Core.Services.FormatSpecific.Excel.Models;

/// <summary>
/// Represents Excel workbook properties and metadata
/// </summary>
public class ExcelProperties
{
    /// <summary>
    /// Workbook title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Workbook author
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Subject of the workbook
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Keywords associated with the workbook
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// Comments or description
    /// </summary>
    public string Comments { get; set; } = string.Empty;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// Last modified by
    /// </summary>
    public string LastModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Company name
    /// </summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>
    /// Manager name
    /// </summary>
    public string Manager { get; set; } = string.Empty;

    /// <summary>
    /// Category
    /// </summary>
    public string Category { get; set; } = string.Empty;
}
