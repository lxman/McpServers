namespace DocumentServer.Core.Services.FormatSpecific.PowerPoint.Models;

public class PowerPointSlide
{
    public int SlideNumber { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Layout { get; set; } = "";
}
