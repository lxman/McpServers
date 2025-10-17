using Microsoft.AspNetCore.Mvc;

namespace AwsServer.Controllers;

/// <summary>
/// API documentation endpoint for DirectoryMcp integration
/// </summary>
[ApiController]
[Route("")]
public class ApiDocumentationController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    /// <summary>
    /// Get OpenAPI specification
    /// </summary>
    [HttpGet("description")]
    public async Task<IActionResult> GetDescription()
    {
        // Fetch the OpenAPI document from the local endpoint
        var client = httpClientFactory.CreateClient();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await client.GetAsync($"{baseUrl}/openapi/v1.json");

        if (!response.IsSuccessStatusCode)
            return StatusCode(500, new { error = "Failed to retrieve OpenAPI specification" });
        
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }
}