using System.ComponentModel.DataAnnotations;

namespace TrafficGenerator.Models;

public class ReconnaissanceRequest : TrafficGenerationRequest
{
    public ReconnaissanceRequest() => TrafficType = "reconnaissance";
    
    [Required] public string TargetNetwork { get; set; } = string.Empty;
    public string ScanType { get; set; } = "port_scan"; // port_scan, service_enum, os_fingerprint, xmas_scan, null_scan
    public string[] Ports { get; set; } = [];
    public bool AggressiveScan { get; set; } = false;
    public int DelayBetweenScans { get; set; } = 100; // milliseconds
}