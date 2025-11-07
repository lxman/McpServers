# HTTP Operations

HTTP client for making REST API calls and web requests.

**See [../COMMON.md](../COMMON.md) for shared concepts: standard responses, error handling.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [http_get](http_get.md) | GET request | url |
| [http_post](http_post.md) | POST request with JSON body | url, jsonBody |
| [http_put](http_put.md) | PUT request with JSON body | url, jsonBody |
| [http_delete](http_delete.md) | DELETE request | url |
| [http_request](http_request.md) | Custom HTTP request | method, url, headersJson?, jsonBody? |

---

## Common Workflows

### Simple GET Request
```
http_get(url: "https://api.example.com/users")
→ Returns response body, status code, headers
```

### POST with JSON Body
```
http_post(
  url: "https://api.example.com/users",
  jsonBody: '{"name": "John", "email": "john@example.com"}'
)
→ Creates resource, returns response
```

### Custom Headers
```
http_request(
  method: "GET",
  url: "https://api.example.com/protected",
  headersJson: '{"Authorization": "Bearer token123"}'
)
→ Authenticated request
```

### PUT to Update
```
http_put(
  url: "https://api.example.com/users/123",
  jsonBody: '{"name": "John Updated"}'
)
→ Updates resource
```

---

## Response Format

All HTTP tools return:

```json
{
  "success": boolean,
  "statusCode": integer,
  "body": string,
  "headers": object,
  "error": string  // Only if failed
}
```

**Success Example:**
```json
{
  "success": true,
  "statusCode": 200,
  "body": "{\"users\": [...]}",
  "headers": {
    "content-type": "application/json",
    "content-length": "1234"
  }
}
```

**Error Example:**
```json
{
  "success": false,
  "statusCode": 404,
  "body": "{\"error\": \"Not found\"}",
  "error": "HTTP request failed with status 404"
}
```

---

## Best Practices

### JSON Body Format

1. **Always use valid JSON string:**
    - ✓ Good: `'{"key": "value"}'`
    - ✗ Bad: `{key: value}` (not JSON string)

2. **Escape quotes properly:**
    - In parameter: `'{"name": "John"}'`
    - Or use double escaping: `"{\"name\": \"John\"}"`

3. **Complex objects:**
   ```
   jsonBody: '{
     "user": {
       "name": "John",
       "preferences": {"theme": "dark"}
     }
   }'
   ```

### Headers

1. **Common headers:**
   ```
   Authorization: "Bearer token"
   Content-Type: "application/json"
   Accept: "application/json"
   User-Agent: "DesktopCommander/1.0"
   ```

2. **Custom headers format:**
   ```
   headersJson: '{
     "Authorization": "Bearer token123",
     "X-Custom-Header": "value"
   }'
   ```

### Error Handling

1. **Check success field:**
   ```
   response = http_get(url)
   if response.success:
       data = parse_json(response.body)
   else:
       handle_error(response.error, response.statusCode)
   ```

2. **Handle HTTP errors:**
    - 4xx: Client errors (bad request, auth, not found)
    - 5xx: Server errors (retry with backoff)
    - Network errors: Connection issues

3. **Parse response body:**
    - Check Content-Type header
    - Parse JSON if application/json
    - Handle empty responses

---

## Common Use Cases

### REST API Integration
```
1. GET /users → List users
2. POST /users → Create user
3. PUT /users/123 → Update user
4. DELETE /users/123 → Delete user
```

### Authentication Flow
```
1. POST /auth/login → Get token
2. Use token in subsequent requests
3. GET /protected → With Authorization header
```

### Webhook Testing
```
http_post(
  url: "https://webhook.site/...",
  jsonBody: '{"event": "test", "data": {...}}'
)
```

### Health Checks
```
http_get(url: "https://api.example.com/health")
→ Check if service is up (200 = healthy)
```

---

## HTTP Methods

### GET
- Retrieve data
- No request body
- Idempotent (safe to retry)
- Cacheable

### POST
- Create new resource
- Has request body
- Not idempotent
- Not cacheable

### PUT
- Update resource (replace)
- Has request body
- Idempotent
- Not cacheable

### DELETE
- Remove resource
- No request body typically
- Idempotent
- Not cacheable

---

## Security Considerations

1. **Never expose secrets in logs:**
    - Authorization tokens
    - API keys
    - Passwords

2. **Use HTTPS:**
    - Avoid HTTP for sensitive data
    - Verify SSL certificates

3. **Rate limiting:**
    - Respect API rate limits
    - Implement backoff strategies
    - Handle 429 (Too Many Requests)

4. **Input validation:**
    - Validate URLs before requests
    - Sanitize user input in JSON bodies
    - Prevent injection attacks

---

## Performance Tips

1. **Connection reuse:**
    - Multiple requests to same host are efficient
    - HTTP keep-alive is automatic

2. **Request timeouts:**
    - Default timeout: 30 seconds
    - Set appropriate timeouts for API

3. **Compression:**
    - Automatic gzip/deflate support
    - Reduces bandwidth usage

---

## Troubleshooting

### Common Issues

**DNS Resolution Failed:**
```
error: "Could not resolve host"
→ Check URL spelling and network connection
```

**Timeout:**
```
error: "Request timeout"
→ API is slow or unresponsive
→ Consider increasing timeout or checking API status
```

**Invalid JSON:**
```
error: "Invalid JSON in body"
→ Verify jsonBody is valid JSON string
→ Use JSON validator
```

**SSL Error:**
```
error: "SSL certificate verification failed"
→ Check certificate validity
→ Verify HTTPS configuration
```

---

**Total Tools:** 5  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**