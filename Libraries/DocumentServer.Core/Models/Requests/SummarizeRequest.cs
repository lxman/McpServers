namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to generate a summary of a document
/// </summary>
/// <param name="MaxLength">Maximum length of the summary in characters (default: 500)</param>
public record SummarizeRequest(int MaxLength = 500);
