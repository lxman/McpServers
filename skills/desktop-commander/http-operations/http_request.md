# http_request

Execute custom HTTP request with full control over method, headers, and body.

**Category:** [http-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| method | string | ✓ | - | HTTP method (GET, POST, PUT, DELETE, PATCH, etc.) |
| url | string | ✓ | - | Target URL |
| headersJson | string | ✗ | null | Custom headers as JSON string |
| jsonBody | string | ✗ | null | Request body as JSON string |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| statusCode | integer | HTTP status code |
| body | string | Response body |
| headers | object | Response headers |

---

## Examples

**Authenticated GET:**
```
http_request(
  method: "GET",
  url: "https://api.example.com/protected",
  headersJson: '{"Authorization": "Bearer token123"}'
)
```

**PATCH request:**
```
http_request(
  method: "PATCH",
  url: "https://api.example.com/users/123",
  headersJson: '{"Authorization": "Bearer token123"}',
  jsonBody: '{"email": "newemail@example.com"}'
)
```

**Custom headers:**
```
http_request(
  method: "POST",
  url: "https://api.example.com/data",
  headersJson: '{
    "Authorization": "Bearer token",
    "X-Custom-Header": "value",
    "Content-Type": "application/json"
  }',
  jsonBody: '{"data": "value"}'
)
```

---

## Notes

- **Use case:** Authentication, custom methods, special headers
- **Headers:** Must be valid JSON string
- **Methods:** GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS
- **Flexibility:** Full control over request
- **Response size protection:** If response exceeds 20,000 token limit (~80KB), response is blocked with error (see [../COMMON.md#response-size-limits](../COMMON.md#response-size-limits))

---

## Related Tools

- [http_get](http_get.md) - Simple GET
- [http_post](http_post.md) - Simple POST
- [http_put](http_put.md) - Simple PUT
- [http_delete](http_delete.md) - Simple DELETE