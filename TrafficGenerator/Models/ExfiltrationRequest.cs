namespace TrafficGenerator.Models;

public class ExfiltrationRequest : TrafficGenerationRequest
{
    public ExfiltrationRequest() => TrafficType = "exfiltration";
    
    public string Method { get; set; } = "dns_tunnel"; // dns_tunnel, icmp_tunnel, http_upload
    public string ExfilDomain { get; set; } = "attacker.com";
    public int DataSizeKB { get; set; } = 1024;
    public string EncodingMethod { get; set; } = "base64";
    public int ChunkSizeBytes { get; set; } = 255;
}