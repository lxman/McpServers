namespace OfficeMcp.Models.PowerPoint;

public class PowerPointShape
{
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}