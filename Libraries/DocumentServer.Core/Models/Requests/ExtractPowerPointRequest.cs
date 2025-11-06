namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to extract PowerPoint slides and speaker notes
/// </summary>
/// <param name="SlideNumber">Optional specific slide number to extract (1-based)</param>
/// <param name="IncludeSpeakerNotes">Include speaker notes in extraction (default: true)</param>
/// <param name="IncludeImages">Include image information in extraction (default: false)</param>
public record ExtractPowerPointRequest(
    int? SlideNumber = null, 
    bool IncludeSpeakerNotes = true, 
    bool IncludeImages = false);
