namespace OfficeReader.Models.PowerPoint;

public class PowerPointSlide
{
    public int SlideNumber { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Layout { get; set; } = "";
    public List<PowerPointShape> Shapes { get; set; } = [];
    public List<PowerPointImage> Images { get; set; } = [];
}