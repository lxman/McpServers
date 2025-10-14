namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to extract Word document structure
/// </summary>
/// <param name="IncludeTables">Include table data in extraction (default: true)</param>
/// <param name="IncludeComments">Include document comments (default: true)</param>
/// <param name="IncludeHeadings">Include heading structure (default: true)</param>
public record ExtractWordRequest(
    bool IncludeTables = true, 
    bool IncludeComments = true, 
    bool IncludeHeadings = true);
