namespace OfficeMcp.Models.PowerPoint;

public class PowerPointAnalysis
{
    public int SlideCount { get; set; }
    public int ImageCount { get; set; }
    public int ShapeCount { get; set; }
    public bool HasSpeakerNotes { get; set; }
    public bool HasTransitions { get; set; }
    public bool HasAnimations { get; set; }
    public List<string> SlideLayouts { get; set; } = [];
}