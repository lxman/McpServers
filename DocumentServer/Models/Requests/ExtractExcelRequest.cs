namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to extract data from Excel worksheets
/// </summary>
/// <param name="WorksheetName">Optional specific worksheet name to extract</param>
/// <param name="CellRange">Optional cell range to extract (e.g., "A1:C10")</param>
/// <param name="IncludeFormulas">Include cell formulas in extraction (default: true)</param>
public record ExtractExcelRequest(
    string? WorksheetName = null, 
    string? CellRange = null, 
    bool IncludeFormulas = true);
