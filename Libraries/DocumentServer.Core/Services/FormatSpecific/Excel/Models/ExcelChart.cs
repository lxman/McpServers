namespace DocumentServer.Core.Services.FormatSpecific.Excel.Models;

/// <summary>
/// Represents a chart in an Excel workbook (reserved for future implementation)
/// </summary>
public class ExcelChart
{
    /// <summary>
    /// Chart name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chart type (e.g., "Line", "Bar", "Pie")
    /// </summary>
    public string ChartType { get; set; } = string.Empty;

    /// <summary>
    /// Worksheet containing this chart
    /// </summary>
    public string WorksheetName { get; set; } = string.Empty;

    /// <summary>
    /// Chart title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Data range for the chart
    /// </summary>
    public string DataRange { get; set; } = string.Empty;
}
