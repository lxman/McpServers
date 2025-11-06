namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to extract images from a PDF document
/// </summary>
/// <param name="OutputDirectory">Directory path where extracted images will be saved</param>
public record ExtractImagesRequest(string OutputDirectory);
