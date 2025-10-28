# http_get

Execute HTTP GET request.

**Category:** [http-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| url | string | ✓ | Target URL |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| statusCode | integer | HTTP status code (200, 404, etc.) |
| body | string | Response body |
| headers | object | Response headers |

---

## Example

```
http_get(url: "https://api.example.com/users")
→ {
    "statusCode": 200,
    "body": "{\"users\": [...]}",
    "headers": {"content-type": "application/json"}
  }
```

---

## Notes

- **Use case:** Retrieve data from APIs
- **Authentication:** Use [http_request](http_request.md) for custom headers
- **Timeouts:** Default 30 seconds
- **Error handling:** Check statusCode (4xx/5xx indicate errors)

---

## Related Tools

- [http_post](http_post.md) - Create resources
- [http_request](http_request.md) - Custom headers/method