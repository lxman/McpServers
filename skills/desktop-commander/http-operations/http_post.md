# http_post

Execute HTTP POST request with JSON body.

**Category:** [http-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| url | string | ✓ | Target URL |
| jsonBody | string | ✓ | JSON body as string |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| statusCode | integer | HTTP status code |
| body | string | Response body |
| headers | object | Response headers |

---

## Example

```
http_post(
  url: "https://api.example.com/users",
  jsonBody: '{"name": "John", "email": "john@example.com"}'
)
→ Creates new user, returns created object
```

---

## Notes

- **JSON format:** jsonBody must be valid JSON string
- **Content-Type:** Automatically set to application/json
- **Use case:** Create resources, submit forms
- **Authentication:** Use [http_request](http_request.md) for auth headers

---

## Related Tools

- [http_get](http_get.md) - Retrieve data
- [http_put](http_put.md) - Update resources
- [http_request](http_request.md) - Custom headers