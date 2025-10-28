# Network Testing Tools

Request interception, API mocking, network conditions simulation, and HAR generation.

## Key Methods

### MockApiResponse
Mock API responses for testing.

**Parameters:**
- `urlPattern` (string, required): URL pattern to match (supports wildcards like */api/users*)
- `responseBody` (string, required): Response body (JSON, XML, or plain text)
- `sessionId` (string, default: "default"): Session ID
- `statusCode` (int, default: 200): HTTP status code
- `method` (string, default: "GET"): HTTP method (GET, POST, PUT, DELETE, PATCH, OPTIONS, HEAD, or * for all)
- `delayMs` (int, default: 0): Response delay in milliseconds
- `headers` (string?, optional): Additional response headers as JSON object

**Returns:** string - Success message

---

### InterceptRequests
Intercept and modify network requests.

**Parameters:**
- `urlPattern` (string, required): URL pattern to intercept
- `sessionId` (string, default: "default"): Session ID
- `action` (string, default: "block"): Action - block, modify, log, or delay
- `method` (string, default: "*"): HTTP method (* for all)
- `delayMs` (int, default: 1000): Delay for 'delay' action
- `modifiedStatusCode` (int?, optional): Modified status code for 'modify' action
- `modifiedBody` (string?, optional): Modified response body for 'modify' action
- `modifiedHeaders` (string?, optional): Modified headers as JSON for 'modify' action

**Returns:** string - Success message

---

### SimulateNetworkConditions
Simulate network conditions (slow, fast, offline).

**Parameters:**
- `networkType` (string, required): Network type - slow, fast, offline, mobile3g, mobile4g
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success message

---

### GenerateHarFile
Generate HAR file for network analysis.

**Parameters:**
- `outputPath` (string, required): Output path for HAR file
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Path to generated HAR file

---

### ListMockRules / ListInterceptRules
List active mock or intercept rules.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with active rules

## Use Cases

- Mock backend APIs during testing
- Test offline functionality
- Simulate slow networks
- Block analytics/tracking requests
- Test error handling

## Example

```
# Mock API
playwright:mock_api_response \
  --urlPattern "*/api/users*" \
  --responseBody '{"users": []}' \
  --statusCode 200

# Simulate slow network
playwright:simulate_network_conditions --networkType slow
```
