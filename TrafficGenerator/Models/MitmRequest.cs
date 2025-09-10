namespace TrafficGenerator.Models;

public class MitmRequest : TrafficGenerationRequest
{
    public MitmRequest() => TrafficType = "mitm";
    
    public string AttackType { get; set; } = "arp_spoofing"; // arp_spoofing, dns_spoofing, ssl_strip
    public string[] TargetIPs { get; set; } = [];
    public string GatewayIP { get; set; } = string.Empty;
    public bool EnableSslStripping { get; set; } = false;
}