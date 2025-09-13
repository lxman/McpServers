using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DnsClient;
using Flurl.Http;
using SharpPcap;
using TrafficGenerator.Models;

namespace TrafficGenerator.Services;

public class TrafficGenerationService(ILogger<TrafficGenerationService> logger) : ITrafficGenerationService
{
    private readonly ConcurrentDictionary<string, TrafficGenerationResponse> _activeSessions = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCancellationTokens = new();

    public async Task<TrafficGenerationResponse> GenerateReconnaissanceTraffic(ReconnaissanceRequest request)
    {
        var response = new TrafficGenerationResponse
        {
            TrafficType = "reconnaissance",
            Details = new Dictionary<string, object>
            {
                ["scanType"] = request.ScanType,
                ["targetNetwork"] = request.TargetNetwork,
                ["aggressive"] = request.AggressiveScan
            }
        };

        _activeSessions[response.SessionId] = response;
        var cancellationToken = new CancellationTokenSource();
        _sessionCancellationTokens[response.SessionId] = cancellationToken;

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteReconnaissanceAsync(request, response, cancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
                response.Status = "cancelled";
                logger.LogInformation("Reconnaissance session {SessionId} was cancelled", response.SessionId);
            }
            catch (Exception ex)
            {
                response.Status = "failed";
                logger.LogError(ex, "Reconnaissance session {SessionId} failed", response.SessionId);
            }
            finally
            {
                _sessionCancellationTokens.TryRemove(response.SessionId, out _);
            }
        }, cancellationToken.Token);

        return response;
    }

    public async Task<TrafficGenerationResponse> GenerateMitmTraffic(MitmRequest request)
    {
        var response = new TrafficGenerationResponse
        {
            TrafficType = "mitm",
            Details = new Dictionary<string, object>
            {
                ["attackType"] = request.AttackType,
                ["targetIPs"] = request.TargetIPs,
                ["sslStripping"] = request.EnableSslStripping
            }
        };

        _activeSessions[response.SessionId] = response;
        var cancellationToken = new CancellationTokenSource();
        _sessionCancellationTokens[response.SessionId] = cancellationToken;

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteMitmAsync(request, response, cancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
                response.Status = "cancelled";
                logger.LogInformation("MITM session {SessionId} was cancelled", response.SessionId);
            }
            catch (Exception ex)
            {
                response.Status = "failed";
                logger.LogError(ex, "MITM session {SessionId} failed", response.SessionId);
            }
            finally
            {
                _sessionCancellationTokens.TryRemove(response.SessionId, out _);
            }
        }, cancellationToken.Token);

        return response;
    }

    public async Task<TrafficGenerationResponse> GenerateExfiltrationTraffic(ExfiltrationRequest request)
    {
        var response = new TrafficGenerationResponse
        {
            TrafficType = "exfiltration",
            Details = new Dictionary<string, object>
            {
                ["method"] = request.Method,
                ["domain"] = request.ExfilDomain,
                ["dataSizeKB"] = request.DataSizeKB,
                ["encoding"] = request.EncodingMethod
            }
        };

        _activeSessions[response.SessionId] = response;
        var cancellationToken = new CancellationTokenSource();
        _sessionCancellationTokens[response.SessionId] = cancellationToken;

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteExfiltrationAsync(request, response, cancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
                response.Status = "cancelled";
                logger.LogInformation("Exfiltration session {SessionId} was cancelled", response.SessionId);
            }
            catch (Exception ex)
            {
                response.Status = "failed";
                logger.LogError(ex, "Exfiltration session {SessionId} failed", response.SessionId);
            }
            finally
            {
                _sessionCancellationTokens.TryRemove(response.SessionId, out _);
            }
        }, cancellationToken.Token);

        return response;
    }

    public async Task<TrafficGenerationResponse> GenerateC2Traffic(C2CommunicationRequest request)
    {
        var response = new TrafficGenerationResponse
        {
            TrafficType = "c2",
            Details = new Dictionary<string, object>
            {
                ["protocol"] = request.C2Protocol,
                ["server"] = request.C2Server,
                ["beaconInterval"] = request.BeaconInterval,
                ["jitter"] = request.JitterPercent,
                ["dga"] = request.UseDGA
            }
        };

        _activeSessions[response.SessionId] = response;
        var cancellationToken = new CancellationTokenSource();
        _sessionCancellationTokens[response.SessionId] = cancellationToken;

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteC2Async(request, response, cancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
                response.Status = "cancelled";
                logger.LogInformation("C2 session {SessionId} was cancelled", response.SessionId);
            }
            catch (Exception ex)
            {
                response.Status = "failed";
                logger.LogError(ex, "C2 session {SessionId} failed", response.SessionId);
            }
            finally
            {
                _sessionCancellationTokens.TryRemove(response.SessionId, out _);
            }
        }, cancellationToken.Token);

        return response;
    }

    #region Private Implementation Methods

    private async Task ExecuteReconnaissanceAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {ScanType} reconnaissance against {TargetNetwork}", request.ScanType, request.TargetNetwork);
        
        response.Status = "running";
        DateTime endTime = DateTime.UtcNow.AddSeconds(request.DurationSeconds);

        switch (request.ScanType.ToLowerInvariant())
        {
            case "port_scan":
                await ExecutePortScanAsync(request, response, endTime, cancellationToken);
                break;
            case "service_enum":
                await ExecuteServiceEnumerationAsync(request, response, endTime, cancellationToken);
                break;
            case "os_fingerprint":
                await ExecuteOsFingerprintAsync(request, response, endTime, cancellationToken);
                break;
            case "dns_enum":
                await ExecuteDnsEnumerationAsync(request, response, endTime, cancellationToken);
                break;
            case "xmas_scan":
                await ExecuteXmasScanAsync(request, response, endTime, cancellationToken);
                break;
            case "null_scan":
                await ExecuteNullScanAsync(request, response, endTime, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported scan type: {request.ScanType}");
        }

        response.Status = "completed";
        logger.LogInformation("Reconnaissance session {SessionId} completed. Generated {PacketCount} packets", 
            response.SessionId, response.PacketsGenerated);
    }

    private async Task ExecutePortScanAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        List<string> targets = ParseNetworkRange(request.TargetNetwork);
        string[] ports = request.Ports.Any() ? request.Ports : ["22", "23", "53", "80", "135", "139", "443", "445", "993", "995"
        ];

        foreach (string target in targets)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            foreach (string portStr in ports)
            {
                if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                    break;

                if (int.TryParse(portStr, out int port))
                {
                    try
                    {
                        using var tcpClient = new TcpClient();
                        Task connectTask = tcpClient.ConnectAsync(target, port);
                        Task timeoutTask = Task.Delay(1000, cancellationToken);
                        
                        await Task.WhenAny(connectTask, timeoutTask);
                        
                        response.PacketsGenerated += 2; // SYN + RST/ACK
                        response.BytesGenerated += 120;
                        
                        if (!request.AggressiveScan)
                            await Task.Delay(request.DelayBetweenScans, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Expected for closed ports
                        logger.LogTrace("Port scan to {Target}:{Port} - {Message}", target, port, ex.Message);
                    }
                }
            }
        }
    }

    private async Task ExecuteServiceEnumerationAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // Service enumeration via banner grabbing
        List<string> targets = ParseNetworkRange(request.TargetNetwork);
        var servicePorts = new[] { 21, 22, 23, 25, 53, 80, 110, 143, 443, 993, 995 };

        foreach (string target in targets)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            foreach (int port in servicePorts)
            {
                if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(target, port).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                    
                    if (tcpClient.Connected)
                    {
                        using NetworkStream stream = tcpClient.GetStream();
                        var buffer = new byte[1024];
                        
                        try
                        {
                            // Try to read banner
                            await stream.ReadAsync(buffer, 0, buffer.Length).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                        }
                        catch
                        {
                            // Some services don't send banners immediately
                        }

                        response.PacketsGenerated += 4; // SYN, SYN-ACK, ACK, FIN
                        response.BytesGenerated += 240;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogTrace("Service enumeration to {Target}:{Port} - {Message}", target, port, ex.Message);
                }

                if (!request.AggressiveScan)
                    await Task.Delay(request.DelayBetweenScans, cancellationToken);
            }
        }
    }

    private async Task ExecuteOsFingerprintAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // OS fingerprinting via various TCP options and ICMP
        List<string> targets = ParseNetworkRange(request.TargetNetwork);

        foreach (string target in targets)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // ICMP ping for TTL analysis
                using var ping = new Ping();
                PingReply reply = await ping.SendPingAsync(target, 1000);
                
                response.PacketsGenerated += 2; // ICMP request/reply
                response.BytesGenerated += 64;

                // TCP window size probing
                var fingerprintPorts = new[] { 80, 443, 22 };
                foreach (int port in fingerprintPorts)
                {
                    if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        using var tcpClient = new TcpClient();
                        await tcpClient.ConnectAsync(target, port).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                        
                        response.PacketsGenerated += 3;
                        response.BytesGenerated += 180;
                    }
                    catch
                    {
                        // Expected for many cases
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogTrace("OS fingerprint to {Target} - {Message}", target, ex.Message);
            }

            if (!request.AggressiveScan)
                await Task.Delay(request.DelayBetweenScans * 2, cancellationToken);
        }
    }

    private async Task ExecuteDnsEnumerationAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        var dnsClient = new LookupClient();
        var subdomains = new[] { "www", "mail", "ftp", "admin", "test", "dev", "staging", "api", "app", "portal" };
        string baseDomain = request.TargetNetwork; // Treat as domain for DNS enum

        foreach (string subdomain in subdomains)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var query = $"{subdomain}.{baseDomain}";
                await dnsClient.QueryAsync(query, QueryType.A, cancellationToken: cancellationToken);
                
                response.PacketsGenerated += 2; // DNS query/response
                response.BytesGenerated += 128;
            }
            catch (Exception ex)
            {
                logger.LogTrace("DNS enumeration for {Query} - {Message}", $"{subdomain}.{baseDomain}", ex.Message);
            }

            await Task.Delay(request.DelayBetweenScans, cancellationToken);
        }
    }

    private async Task ExecuteXmasScanAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // XMAS scan - TCP packets with FIN+PSH+URG flags set (like a Christmas tree)
        List<string> targets = ParseNetworkRange(request.TargetNetwork);
        string[] ports = request.Ports.Any() ? request.Ports : ["22", "23", "53", "80", "135", "139", "443", "445", "993", "995"];

        foreach (string target in targets)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            foreach (string portStr in ports)
            {
                if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                    break;

                if (int.TryParse(portStr, out int port))
                {
                    try
                    {
                        await SendXmasPacketAsync(target, port, response);
                        
                        if (!request.AggressiveScan)
                            await Task.Delay(request.DelayBetweenScans, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogTrace("XMAS scan to {Target}:{Port} - {Message}", target, port, ex.Message);
                    }
                }
            }
        }
    }

    private async Task ExecuteNullScanAsync(ReconnaissanceRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // NULL scan - TCP packets with no flags set
        List<string> targets = ParseNetworkRange(request.TargetNetwork);
        string[] ports = request.Ports.Any() ? request.Ports : ["22", "23", "53", "80", "135", "139", "443", "445", "993", "995"];

        foreach (string target in targets)
        {
            if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                break;

            foreach (string portStr in ports)
            {
                if (DateTime.UtcNow >= endTime || cancellationToken.IsCancellationRequested)
                    break;

                if (int.TryParse(portStr, out int port))
                {
                    try
                    {
                        await SendNullPacketAsync(target, port, response);
                        
                        if (!request.AggressiveScan)
                            await Task.Delay(request.DelayBetweenScans, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogTrace("NULL scan to {Target}:{Port} - {Message}", target, port, ex.Message);
                    }
                }
            }
        }
    }

    private async Task SendXmasPacketAsync(string target, int port, TrafficGenerationResponse response)
    {
        try
        {
            // Create XMAS packet with FIN+PSH+URG flags
            // For simulation purposes, we'll use a raw socket approach
            // In real implementation, this would use PacketDotNet to craft the exact packet
            
            // Simulate XMAS packet characteristics
            using var tcpClient = new TcpClient();
            
            // Set socket options to simulate unusual packet behavior
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            
            Task connectTask = tcpClient.ConnectAsync(target, port);
            Task timeoutTask = Task.Delay(500); // Shorter timeout for stealth
            
            await Task.WhenAny(connectTask, timeoutTask);
            
            response.PacketsGenerated += 1; // XMAS packet
            response.BytesGenerated += 54; // TCP header + minimal payload
            
            logger.LogTrace("XMAS packet sent to {Target}:{Port}", target, port);
        }
        catch
        {
            // Expected behavior for XMAS scans - most ports will not respond normally
            response.PacketsGenerated += 1;
            response.BytesGenerated += 54;
        }
        
        await Task.Delay(10); // Small delay to simulate packet transmission
    }

    private async Task SendNullPacketAsync(string target, int port, TrafficGenerationResponse response)
    {
        try
        {
            // Create NULL packet with no flags set
            // For simulation purposes, we'll use a raw socket approach
            // In real implementation, this would use PacketDotNet to craft the exact packet
            
            // Simulate NULL packet characteristics  
            using var tcpClient = new TcpClient();
            
            // Set socket options to simulate unusual packet behavior
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            
            Task connectTask = tcpClient.ConnectAsync(target, port);
            Task timeoutTask = Task.Delay(300); // Very short timeout for NULL scan
            
            await Task.WhenAny(connectTask, timeoutTask);
            
            response.PacketsGenerated += 1; // NULL packet
            response.BytesGenerated += 40; // Minimal TCP header
            
            logger.LogTrace("NULL packet sent to {Target}:{Port}", target, port);
        }
        catch
        {
            // Expected behavior for NULL scans - designed to not establish connections
            response.PacketsGenerated += 1; 
            response.BytesGenerated += 40;
        }
        
        await Task.Delay(5); // Very small delay to simulate packet transmission
    }

    private async Task ExecuteMitmAsync(MitmRequest request, TrafficGenerationResponse response, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {AttackType} MITM attack", request.AttackType);
        
        response.Status = "running";
        DateTime endTime = DateTime.UtcNow.AddSeconds(request.DurationSeconds);

        switch (request.AttackType.ToLowerInvariant())
        {
            case "arp_spoofing":
                await ExecuteArpSpoofingAsync(request, response, endTime, cancellationToken);
                break;
            case "dns_spoofing":
                await ExecuteDnsSpoofingAsync(request, response, endTime, cancellationToken);
                break;
            case "ssl_strip":
                await ExecuteSslStrippingAsync(request, response, endTime, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported MITM attack: {request.AttackType}");
        }

        response.Status = "completed";
    }

    private async Task ExecuteArpSpoofingAsync(MitmRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // Simulate ARP spoofing by generating ARP packets
        // Note: This is simulation - real ARP spoofing would require raw sockets and admin privileges
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            foreach (string targetIp in request.TargetIPs)
            {
                // Simulate ARP reply packets
                response.PacketsGenerated += 1;
                response.BytesGenerated += 42; // Typical ARP packet size
                
                logger.LogTrace("Generated ARP spoof packet for {TargetIP}", targetIp);
            }
            
            await Task.Delay(5000, cancellationToken); // ARP spoofing typically every 5 seconds
        }
    }

    private async Task ExecuteDnsSpoofingAsync(MitmRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // Simulate DNS spoofing responses
        var spoofDomains = new[] { "google.com", "facebook.com", "microsoft.com", "apple.com" };
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            foreach (string domain in spoofDomains)
            {
                // Simulate DNS response packets
                response.PacketsGenerated += 1;
                response.BytesGenerated += 128;
                
                logger.LogTrace("Generated DNS spoof response for {Domain}", domain);
            }
            
            await Task.Delay(2000, cancellationToken);
        }
    }

    private async Task ExecuteSslStrippingAsync(MitmRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // Simulate SSL stripping by generating HTTP traffic that should be HTTPS
        var httpsUrls = new[] 
        { 
            "http://secure.example.com/login",
            "http://banking.example.com/account", 
            "http://admin.example.com/panel"
        };

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            foreach (string url in httpsUrls)
            {
                try
                {
                    // Generate HTTP traffic where HTTPS would be expected
                    await url.GetStringAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    
                    response.PacketsGenerated += 6; // HTTP request/response cycle
                    response.BytesGenerated += 2048;
                }
                catch (Exception ex)
                {
                    logger.LogTrace("SSL strip simulation to {Url} - {Message}", url, ex.Message);
                }
            }
            
            await Task.Delay(10000, cancellationToken);
        }
    }

    private async Task ExecuteExfiltrationAsync(ExfiltrationRequest request, TrafficGenerationResponse response, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {Method} data exfiltration", request.Method);
        
        response.Status = "running";
        DateTime endTime = DateTime.UtcNow.AddSeconds(request.DurationSeconds);

        switch (request.Method.ToLowerInvariant())
        {
            case "dns_tunnel":
                await ExecuteDnsTunnelingAsync(request, response, endTime, cancellationToken);
                break;
            case "icmp_tunnel":
                await ExecuteIcmpTunnelingAsync(request, response, endTime, cancellationToken);
                break;
            case "http_upload":
                await ExecuteHttpUploadAsync(request, response, endTime, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported exfiltration method: {request.Method}");
        }

        response.Status = "completed";
    }

    private async Task ExecuteDnsTunnelingAsync(ExfiltrationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        var dnsClient = new LookupClient();
        int totalBytes = request.DataSizeKB * 1024;
        int chunkSize = Math.Min(request.ChunkSizeBytes, 255); // DNS label limit
        int chunks = (totalBytes + chunkSize - 1) / chunkSize;

        for (var i = 0; i < chunks && DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested; i++)
        {
            // Generate fake data chunk
            byte[] data = GenerateRandomData(chunkSize);
            string encodedData = Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").Replace("=", "");
            
            // Create DNS query with data in subdomain
            var query = $"{encodedData.Substring(0, Math.Min(encodedData.Length, 63))}.{request.ExfilDomain}";
            
            try
            {
                await dnsClient.QueryAsync(query, QueryType.A, cancellationToken: cancellationToken);
                
                response.PacketsGenerated += 2; // DNS query/response
                response.BytesGenerated += query.Length + 64;
                
                logger.LogTrace("DNS tunnel chunk {ChunkIndex}/{TotalChunks} - {Query}", i + 1, chunks, query);
            }
            catch (Exception ex)
            {
                logger.LogTrace("DNS tunnel chunk failed - {Message}", ex.Message);
            }

            await Task.Delay(1000, cancellationToken); // Slow exfiltration to avoid detection
        }
    }

    private async Task ExecuteIcmpTunnelingAsync(ExfiltrationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        // Simulate ICMP tunneling using ping with custom data
        int totalBytes = request.DataSizeKB * 1024;
        int chunkSize = Math.Min(request.ChunkSizeBytes, 1400); // ICMP data limit
        int chunks = (totalBytes + chunkSize - 1) / chunkSize;

        using var ping = new Ping();
        var options = new PingOptions { DontFragment = true };

        for (var i = 0; i < chunks && DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                byte[] data = GenerateRandomData(chunkSize);
                PingReply reply = await ping.SendPingAsync(request.TargetHost ?? "8.8.8.8", 5000, data, options);
                
                response.PacketsGenerated += 2; // ICMP request/reply
                response.BytesGenerated += data.Length + 28; // ICMP header overhead
                
                logger.LogTrace("ICMP tunnel chunk {ChunkIndex}/{TotalChunks} - {Status}", i + 1, chunks, reply.Status);
            }
            catch (Exception ex)
            {
                logger.LogTrace("ICMP tunnel chunk failed - {Message}", ex.Message);
            }

            await Task.Delay(2000, cancellationToken);
        }
    }

    private async Task ExecuteHttpUploadAsync(ExfiltrationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        int totalBytes = request.DataSizeKB * 1024;
        int chunkSize = Math.Min(request.ChunkSizeBytes, 1024 * 1024); // 1MB max chunks
        int chunks = (totalBytes + chunkSize - 1) / chunkSize;

        var uploadUrl = $"https://{request.ExfilDomain}/upload";

        for (var i = 0; i < chunks && DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                byte[] data = GenerateRandomData(chunkSize);
                string encodedData = Convert.ToBase64String(data);
                
                var postData = new { chunk = i, data = encodedData, session = response.SessionId };
                
                await uploadUrl.PostJsonAsync(postData, cancellationToken: cancellationToken);
                
                response.PacketsGenerated += 4; // HTTP request/response cycle
                response.BytesGenerated += encodedData.Length + 512; // Headers overhead
                
                logger.LogTrace("HTTP upload chunk {ChunkIndex}/{TotalChunks} - {Size} bytes", i + 1, chunks, chunkSize);
            }
            catch (Exception ex)
            {
                logger.LogTrace("HTTP upload chunk failed - {Message}", ex.Message);
            }

            await Task.Delay(5000, cancellationToken); // Throttled upload
        }
    }

    private async Task ExecuteC2Async(C2CommunicationRequest request, TrafficGenerationResponse response, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {Protocol} C2 communication to {Server}", request.C2Protocol, request.C2Server);
        
        response.Status = "running";
        DateTime endTime = DateTime.UtcNow.AddSeconds(request.DurationSeconds);

        switch (request.C2Protocol.ToLowerInvariant())
        {
            case "http":
            case "https":
                await ExecuteHttpC2Async(request, response, endTime, cancellationToken);
                break;
            case "dns":
                await ExecuteDnsC2Async(request, response, endTime, cancellationToken);
                break;
            case "icmp":
                await ExecuteIcmpC2Async(request, response, endTime, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported C2 protocol: {request.C2Protocol}");
        }

        response.Status = "completed";
    }

    private async Task ExecuteHttpC2Async(C2CommunicationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        string[] userAgents = request.UserAgents.Any() ? request.UserAgents :
        [
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"
        ];

        var random = new Random();
        var beaconCount = 0;

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                string c2Url = request.UseDGA ? GenerateDgaDomain() : request.C2Server;
                string userAgent = userAgents[random.Next(userAgents.Length)];
                
                var beaconData = new 
                { 
                    id = response.SessionId,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    beacon = ++beaconCount,
                    data = Convert.ToBase64String(GenerateRandomData(128))
                };

                await $"https://{c2Url}/api/beacon"
                    .WithHeader("User-Agent", userAgent)
                    .PostJsonAsync(beaconData, cancellationToken: cancellationToken);

                response.PacketsGenerated += 4; // HTTP request/response
                response.BytesGenerated += 1024;

                logger.LogTrace("C2 beacon {BeaconCount} to {Server}", beaconCount, c2Url);

                // Calculate jittered delay
                int baseDelay = request.BeaconInterval * 1000;
                var jitter = (int)(baseDelay * (request.JitterPercent / 100.0));
                int delay = baseDelay + random.Next(-jitter, jitter);

                await Task.Delay(Math.Max(delay, 1000), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogTrace("C2 beacon failed - {Message}", ex.Message);
                await Task.Delay(30000, cancellationToken); // Wait on failure
            }
        }
    }

    private async Task ExecuteDnsC2Async(C2CommunicationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        var dnsClient = new LookupClient();
        var random = new Random();
        var beaconCount = 0;

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                string c2Domain = request.UseDGA ? GenerateDgaDomain() : request.C2Server;
                var beaconData = $"{++beaconCount:x8}.{response.SessionId[..8]}.{c2Domain}";

                await dnsClient.QueryAsync(beaconData, QueryType.A, cancellationToken: cancellationToken);

                response.PacketsGenerated += 2; // DNS query/response
                response.BytesGenerated += 128;

                logger.LogTrace("DNS C2 beacon {BeaconCount} to {Domain}", beaconCount, beaconData);

                int baseDelay = request.BeaconInterval * 1000;
                var jitter = (int)(baseDelay * (request.JitterPercent / 100.0));
                int delay = baseDelay + random.Next(-jitter, jitter);

                await Task.Delay(Math.Max(delay, 5000), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogTrace("DNS C2 beacon failed - {Message}", ex.Message);
                await Task.Delay(60000, cancellationToken);
            }
        }
    }

    private async Task ExecuteIcmpC2Async(C2CommunicationRequest request, TrafficGenerationResponse response, DateTime endTime, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var random = new Random();
        var beaconCount = 0;

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                byte[] beaconData = GenerateRandomData(32);
                beaconData[0] = (byte)(++beaconCount & 0xFF); // Beacon counter in first byte

                PingReply reply = await ping.SendPingAsync(request.C2Server, 5000, beaconData);

                response.PacketsGenerated += 2; // ICMP request/reply
                response.BytesGenerated += 60;

                logger.LogTrace("ICMP C2 beacon {BeaconCount} - {Status}", beaconCount, reply.Status);

                int baseDelay = request.BeaconInterval * 1000;
                var jitter = (int)(baseDelay * (request.JitterPercent / 100.0));
                int delay = baseDelay + random.Next(-jitter, jitter);

                await Task.Delay(Math.Max(delay, 10000), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogTrace("ICMP C2 beacon failed - {Message}", ex.Message);
                await Task.Delay(120000, cancellationToken);
            }
        }
    }

    #endregion

    #region Helper Methods

    private List<string> ParseNetworkRange(string networkRange)
    {
        var targets = new List<string>();
        
        if (networkRange.Contains('/'))
        {
            // CIDR notation - simplified implementation
            string[] parts = networkRange.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress? baseIp) && int.TryParse(parts[1], out int prefixLength))
            {
                // For demo purposes, generate a few IPs from the range
                byte[] baseBytes = baseIp.GetAddressBytes();
                for (var i = 1; i <= Math.Min(10, Math.Pow(2, 32 - prefixLength)); i++)
                {
                    var ip = new IPAddress(new byte[] 
                    { 
                        baseBytes[0], 
                        baseBytes[1], 
                        baseBytes[2], 
                        (byte)(baseBytes[3] + i) 
                    });
                    targets.Add(ip.ToString());
                }
            }
        }
        else if (networkRange.Contains('-'))
        {
            // Range notation like 192.168.1.1-192.168.1.10
            string[] parts = networkRange.Split('-');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress? startIp) && IPAddress.TryParse(parts[1], out IPAddress? endIp))
            {
                byte[] startBytes = startIp.GetAddressBytes();
                byte[] endBytes = endIp.GetAddressBytes();
                
                for (int i = startBytes[3]; i <= endBytes[3] && i <= startBytes[3] + 20; i++)
                {
                    var ip = new IPAddress(new byte[] { startBytes[0], startBytes[1], startBytes[2], (byte)i });
                    targets.Add(ip.ToString());
                }
            }
        }
        else
        {
            // Single IP or hostname
            targets.Add(networkRange);
        }

        return targets;
    }

    private byte[] GenerateRandomData(int size)
    {
        var random = new Random();
        var data = new byte[size];
        random.NextBytes(data);
        return data;
    }

    private string GenerateDgaDomain()
    {
        var random = new Random();
        var tlds = new[] { "com", "net", "org", "info", "biz" };
        int domainLength = random.Next(8, 16);
        
        var domain = new string(Enumerable.Range(0, domainLength)
            .Select(_ => (char)('a' + random.Next(26)))
            .ToArray());
            
        return $"{domain}.{tlds[random.Next(tlds.Length)]}";
    }

    #endregion

    #region Session Management

    public async Task<IEnumerable<TrafficGenerationResponse>> GetActiveSessionsAsync()
    {
        return await Task.FromResult(_activeSessions.Values.ToList());
    }

    public async Task<TrafficGenerationResponse?> GetSessionAsync(string sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out TrafficGenerationResponse? session);
        return await Task.FromResult(session);
    }

    public async Task<bool> StopSessionAsync(string sessionId)
    {
        if (_sessionCancellationTokens.TryRemove(sessionId, out CancellationTokenSource? cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            if (_activeSessions.TryGetValue(sessionId, out TrafficGenerationResponse? session))
            {
                session.Status = "stopped";
            }

            return await Task.FromResult(true);
        }

        return await Task.FromResult(false);
    }

    public async Task<IEnumerable<object>> GetAvailableInterfacesAsync()
    {
        var interfaces = new List<object>();
        
        try
        {
            var devices = CaptureDeviceList.Instance;
            for (var i = 0; i < devices.Count; i++)
            {
                ILiveDevice? device = devices[i];
                interfaces.Add(new
                {
                    Index = i,
                    Name = device.Name,
                    Description = device.Description,
                    MacAddress = device.MacAddress?.ToString() ?? "Unknown",
                    IsLoopback = device.Name?.ToLower().Contains("loopback") ?? false
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate network interfaces");
        }

        return await Task.FromResult(interfaces);
    }

    #endregion
}
