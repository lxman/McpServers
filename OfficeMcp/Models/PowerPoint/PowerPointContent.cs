namespace OfficeMcp.Models.PowerPoint;

public class PowerPointContent
{
    public List<PowerPointSlide> Slides { get; set; } = [];
    public PowerPointProperties Properties { get; set; } = new();
}