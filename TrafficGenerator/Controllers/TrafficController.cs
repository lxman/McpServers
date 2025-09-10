using Microsoft.AspNetCore.Mvc;
using TrafficGenerator.Models;
using TrafficGenerator.Services;

namespace TrafficGenerator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrafficController(ITrafficGenerationService trafficService, ILogger<TrafficController> logger)
    : ControllerBase
{
    /// <summary>
    /// Generate reconnaissance traffic (port scans, service enumeration)
    /// </summary>
    [HttpPost("reconnaissance")]
    public async Task<ActionResult<TrafficGenerationResponse>> GenerateReconnaissance([FromBody] ReconnaissanceRequest request)
    {
        try
        {
            logger.LogInformation("Starting reconnaissance traffic generation for {TargetNetwork}", request.TargetNetwork);
            TrafficGenerationResponse response = await trafficService.GenerateReconnaissanceTraffic(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate reconnaissance traffic");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate man-in-the-middle attack traffic
    /// </summary>
    [HttpPost("mitm")]
    public async Task<ActionResult<TrafficGenerationResponse>> GenerateMitm([FromBody] MitmRequest request)
    {
        try
        {
            logger.LogInformation("Starting MITM traffic generation with attack type {AttackType}", request.AttackType);
            TrafficGenerationResponse response = await trafficService.GenerateMitmTraffic(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate MITM traffic");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate data exfiltration traffic
    /// </summary>
    [HttpPost("exfiltration")]
    public async Task<ActionResult<TrafficGenerationResponse>> GenerateExfiltration([FromBody] ExfiltrationRequest request)
    {
        try
        {
            logger.LogInformation("Starting exfiltration traffic generation via {Method}", request.Method);
            TrafficGenerationResponse response = await trafficService.GenerateExfiltrationTraffic(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate exfiltration traffic");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate command and control communication traffic
    /// </summary>
    [HttpPost("c2")]
    public async Task<ActionResult<TrafficGenerationResponse>> GenerateC2([FromBody] C2CommunicationRequest request)
    {
        try
        {
            logger.LogInformation("Starting C2 communication traffic via {Protocol}", request.C2Protocol);
            TrafficGenerationResponse response = await trafficService.GenerateC2Traffic(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate C2 traffic");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get status of active traffic generation sessions
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<TrafficGenerationResponse>>> GetActiveSessions()
    {
        try
        {
            IEnumerable<TrafficGenerationResponse> sessions = await trafficService.GetActiveSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get active sessions");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get status of specific traffic generation session
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    public async Task<ActionResult<TrafficGenerationResponse>> GetSession(string sessionId)
    {
        try
        {
            TrafficGenerationResponse? session = await trafficService.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound(new { error = "Session not found" });
            
            return Ok(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop traffic generation session
    /// </summary>
    [HttpPost("sessions/{sessionId}/stop")]
    public async Task<ActionResult> StopSession(string sessionId)
    {
        try
        {
            bool success = await trafficService.StopSessionAsync(sessionId);
            if (!success)
                return NotFound(new { error = "Session not found or already stopped" });
            
            return Ok(new { message = "Session stopped successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop session {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List available network interfaces for traffic generation
    /// </summary>
    [HttpGet("interfaces")]
    public async Task<ActionResult<IEnumerable<object>>> GetNetworkInterfaces()
    {
        try
        {
            IEnumerable<object> interfaces = await trafficService.GetAvailableInterfacesAsync();
            return Ok(interfaces);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get network interfaces");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get traffic generation capabilities and supported attack types
    /// </summary>
    [HttpGet("capabilities")]
    public async Task<ActionResult<object>> GetCapabilities()
    {
        var capabilities = new
        {
            SupportedTrafficTypes = new[]
            {
                "reconnaissance",
                "mitm", 
                "exfiltration",
                "c2"
            },
            ReconnaissanceTypes = new[]
            {
                "port_scan",
                "service_enum", 
                "os_fingerprint",
                "dns_enum",
                "xmas_scan",
                "null_scan"
            },
            MitmAttacks = new[]
            {
                "arp_spoofing",
                "dns_spoofing", 
                "ssl_strip",
                "dhcp_starvation"
            },
            ExfiltrationMethods = new[]
            {
                "dns_tunnel",
                "icmp_tunnel",
                "http_upload",
                "steganography"
            },
            C2Protocols = new[]
            {
                "http",
                "https",
                "dns", 
                "icmp",
                "p2p"
            }
        };
        
        return Ok(capabilities);
    }
}
