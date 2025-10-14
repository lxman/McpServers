using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// Tools for making HTTP requests to MCP servers
/// </summary>
[McpServerToolType]
public class HttpTools(IHttpClientFactory httpClientFactory, ILogger<HttpTools> logger)
{
    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient("mcp-client");
    }

    [McpServerTool, DisplayName("http_get")]
    [Description("""
                 Make a GET request to an MCP server endpoint.
                     
                 Use this to:
                 - Call /description on any MCP server to see its capabilities
                 - Query endpoints that return data
                 - Check server health/status

                 Example: To see what Redis tools are available, call:
                   url: 'https://localhost:7183/description'
                 """)]
    public async Task<string> HttpGet(
        [Description("Full URL to request (e.g., 'https://localhost:7183/description')")] string url)
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
    [Description("""
                 Make a POST request to an MCP server endpoint with a JSON body.
                     
                 Use this to:
                 - Call MCP server tool endpoints
                 - Send data to server operations
                 - Execute server commands

                 Example: To execute a Redis command:
                   url: 'https://localhost:7183/api/redis/execute'
                   jsonBody: '{"command":"PING"}'
                 """)]
    public async Task<string> HttpPost(
        [Description("Full URL to request")] string url,
        [Description("JSON body to send (as a string)")] string jsonBody)
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
    [Description("""
                 Make a PUT request to an MCP server endpoint with a JSON body.
                     
                 Use this to:
                 - Update resources on MCP servers
                 - Modify server configurations
                 - Execute update operations
                 """)]
    public async Task<string> HttpPut(
        [Description("Full URL to request")] string url,
        [Description("JSON body to send (as a string)")] string jsonBody)
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
    [Description("""
                 Make a DELETE request to an MCP server endpoint.
                     
                 Use this to:
                 - Delete resources on MCP servers
                 - Remove data or cancel operations
                 - Clean up server state
                 """)]
    public async Task<string> HttpDelete(
        [Description("Full URL to request")] string url)
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
    [Description("""
                 Make a custom HTTP request with full control over method, headers, and body.
                     
                 Use this for:
                 - Custom HTTP methods (PATCH, OPTIONS, etc.)
                 - Requests requiring custom headers
                 - Advanced HTTP operations
                 """)]
    public async Task<string> HttpRequest(
        [Description("HTTP method (GET, POST, PUT, DELETE, PATCH, etc.)")] string method,
        [Description("Full URL to request")] string url,
        [Description("JSON body to send (optional, empty string for no body)")] string jsonBody = "",
        [Description("Custom headers as JSON object (optional, e.g., '{\"X-Custom\":\"value\"}')")] string headersJson = "")
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