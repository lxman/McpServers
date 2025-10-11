using Microsoft.AspNetCore.Mvc;

namespace PlaywrightServer.Controllers;

/// <summary>
/// API documentation endpoint for directory integration
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
        HttpClient client = httpClientFactory.CreateClient();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        HttpResponseMessage response = await client.GetAsync($"{baseUrl}/openapi/v1.json");

        if (!response.IsSuccessStatusCode)
            return StatusCode(500, new { error = "Failed to retrieve OpenAPI specification" });
        string content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }
}