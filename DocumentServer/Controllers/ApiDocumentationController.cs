using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace DocumentServer.Controllers;

/// <summary>
/// API documentation endpoint for DirectoryMcp integration
/// </summary>
[ApiController]
[Route("")]
// IOpenApiDocumentProvider is registered as a keyed service; the key is the document name
// passed to AddOpenApi(), which defaults to "v1".
public class ApiDocumentationController(
    [FromKeyedServices("v1")] IOpenApiDocumentProvider documentProvider) : ControllerBase
{
    /// <summary>
    /// Get OpenAPI specification
    /// </summary>
    [HttpGet("description")]
    public async Task<IActionResult> GetDescription(CancellationToken cancellationToken)
    {
        // Generated in-process. This previously issued an HTTP request back to this same server
        // for /openapi/v1.json, which 404'd outside Development and tripped over HTTPS redirection.
        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(cancellationToken);

        await using var stringWriter = new StringWriter();
        var writer = new OpenApiJsonWriter(stringWriter);
        // 3.1 matches what MapOpenApi() serves at /openapi/v1.json, so both endpoints
        // describe nullability identically.
        document.SerializeAsV31(writer);
        await writer.FlushAsync();

        return Content(stringWriter.ToString(), "application/json");
    }
}
