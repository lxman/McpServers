# http_put

Execute HTTP PUT request with JSON body.

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
http_put(
  url: "https://api.example.com/users/123",
  jsonBody: '{"name": "John Updated"}'
)
→ Updates user 123
```

---

## Notes

- **Use case:** Update existing resources
- **Idempotent:** Safe to retry
- **Full replacement:** Typically replaces entire resource

---

## Related Tools

- [http_post](http_post.md) - Create resources
- [http_get](http_get.md) - Retrieve before update