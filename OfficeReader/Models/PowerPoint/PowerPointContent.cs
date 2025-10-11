namespace OfficeReader.Models.PowerPoint;

public class PowerPointContent
{
    public string PlainText { get; set; } = string.Empty;
    public List<PowerPointSlide> Slides { get; set; } = [];
    public int SlideCount { get; set; }
    public PowerPointProperties Properties { get; set; } = new();
}
