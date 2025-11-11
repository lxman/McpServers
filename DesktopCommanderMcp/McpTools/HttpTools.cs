using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DesktopCommander.Core.Services;
using Mcp.Common.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// Tools for making HTTP requests to MCP servers
/// </summary>
[McpServerToolType]
public class HttpTools(
    IHttpClientFactory httpClientFactory,
    ResponseSizeGuard responseSizeGuard,
    ILogger<HttpTools> logger)
{
    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient("mcp-client");
    }

    [McpServerTool, DisplayName("http_get")]
    [Description("GET request to endpoint. See http-operations/SKILL.md")]
    public async Task<string> HttpGet(
        string url)
    {
        try
        {
            logger.LogInformation("Making GET request to: {Url}", url);
            
            using HttpClient httpClient = CreateClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);
            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase,
                    error = content
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check response size before processing
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckStringSize(content, "http_get");

            if (!sizeCheck.IsWithinLimit)
            {
                return ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"HTTP response from '{url}' is too large.",
                    "Try fetching a more specific endpoint, use query parameters to filter results, or implement pagination.",
                    new { url, statusCode = (int)response.StatusCode, contentLength = content.Length });
            }

            // Try to parse as JSON for pretty printing
            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(doc, SerializerOptions.JsonOptionsIndented);
            }
            catch
            {
                // Not JSON, return as-is
                return content;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making GET request to {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("http_post")]
    [Description("POST request with JSON body. See http-operations/SKILL.md")]
    public async Task<string> HttpPost(
        string url,
        string jsonBody)
    {
        try
        {
            logger.LogInformation("Making POST request to: {Url}", url);
            
            using HttpClient httpClient = CreateClient();
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(url, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase,
                    error = responseContent
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check response size before processing
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckStringSize(responseContent, "http_post");

            if (!sizeCheck.IsWithinLimit)
            {
                return ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"HTTP POST response from '{url}' is too large.",
                    "Try posting to a more specific endpoint, request fewer results, or use pagination in the API.",
                    new { url, statusCode = (int)response.StatusCode, contentLength = responseContent.Length });
            }

            // Try to parse as JSON for pretty printing
            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseContent);
                return JsonSerializer.Serialize(doc, SerializerOptions.JsonOptionsIndented);
            }
            catch
            {
                // Not JSON, return as-is
                return responseContent;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making POST request to {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("http_put")]
    [Description("PUT request with JSON body. See http-operations/SKILL.md")]
    public async Task<string> HttpPut(
        string url,
        string jsonBody)
    {
        try
        {
            logger.LogInformation("Making PUT request to: {Url}", url);
            
            using HttpClient httpClient = CreateClient();
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PutAsync(url, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase,
                    error = responseContent
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check response size before processing
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckStringSize(responseContent, "http_put");

            if (!sizeCheck.IsWithinLimit)
            {
                return ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"HTTP PUT response from '{url}' is too large.",
                    "The API response is too large to return. Consider requesting a summary or specific fields.",
                    new { url, statusCode = (int)response.StatusCode, contentLength = responseContent.Length });
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseContent);
                return JsonSerializer.Serialize(doc, SerializerOptions.JsonOptionsIndented);
            }
            catch
            {
                return responseContent;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making PUT request to {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("http_delete")]
    [Description("DELETE request to endpoint. See http-operations/SKILL.md")]
    public async Task<string> HttpDelete(
        string url)
    {
        try
        {
            logger.LogInformation("Making DELETE request to: {Url}", url);
            
            using HttpClient httpClient = CreateClient();
            HttpResponseMessage response = await httpClient.DeleteAsync(url);
            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase,
                    error = content
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check response size before processing
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckStringSize(content, "http_delete");

            if (!sizeCheck.IsWithinLimit)
            {
                return ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"HTTP DELETE response from '{url}' is too large.",
                    "The API response is too large to return. Consider requesting confirmation or summary data only.",
                    new { url, statusCode = (int)response.StatusCode, contentLength = content.Length });
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(doc, SerializerOptions.JsonOptionsIndented);
            }
            catch
            {
                return content;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making DELETE request to {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("http_request")]
    [Description("Custom HTTP request with headers. See http-operations/SKILL.md")]
    public async Task<string> HttpRequest(
        string method,
        string url,
        string jsonBody = "",
        string headersJson = "")
    {
        try
        {
            logger.LogInformation("Making {Method} request to: {Url}", method, url);
            
            using HttpClient httpClient = CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(method.ToUpper()), url);

            // Add custom headers if provided
            if (!string.IsNullOrWhiteSpace(headersJson))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                    if (headers != null)
                    {
                        foreach (KeyValuePair<string, string> header in headers)
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse custom headers JSON");
                }
            }

            // Add body if provided
            if (!string.IsNullOrWhiteSpace(jsonBody))
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = await httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase,
                    error = content
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Check response size before processing
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckStringSize(content, "http_request");

            if (!sizeCheck.IsWithinLimit)
            {
                return ResponseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"HTTP {method.ToUpper()} response from '{url}' is too large.",
                    "The API response is too large to return. Try requesting specific fields, applying filters, or using pagination.",
                    new { method = method.ToUpper(), url, statusCode = (int)response.StatusCode, contentLength = content.Length });
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(doc, SerializerOptions.JsonOptionsIndented);
            }
            catch
            {
                return content;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making {Method} request to {Url}", method, url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}