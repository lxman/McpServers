namespace OfficeMcp.Models.PowerPoint;

public class PowerPointProperties
{
    public int SlideCount { get; set; }
    public string Template { get; set; } = "";
    public bool HasTransitions { get; set; }
    public bool HasAnimations { get; set; }
    public bool HasSpeakerNotes { get; set; }
}