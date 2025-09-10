namespace TrafficGenerator.Models;

public class C2CommunicationRequest : TrafficGenerationRequest
{
    public C2CommunicationRequest() => TrafficType = "c2";
    
    public string C2Protocol { get; set; } = "http"; // http, https, dns, icmp
    public string C2Server { get; set; } = "c2.attacker.com";
    public int BeaconInterval { get; set; } = 30; // seconds
    public int JitterPercent { get; set; } = 20;
    public bool UseDGA { get; set; } = false; // Domain Generation Algorithm
    public string[] UserAgents { get; set; } = [];
}