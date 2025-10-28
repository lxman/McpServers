# http_delete

Execute HTTP DELETE request.

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
| statusCode | integer | HTTP status code (typically 204) |
| body | string | Response body (often empty) |
| headers | object | Response headers |

---

## Example

```
http_delete(url: "https://api.example.com/users/123")
→ Deletes user 123
```

---

## Notes

- **Use case:** Delete resources
- **Status codes:** 204 (No Content) or 200 (OK)
- **Idempotent:** Safe to retry
- **Authentication:** Use [http_request](http_request.md) for auth
- **Response size protection:** If response exceeds 20,000 token limit (~80KB), response is blocked with error (see [../COMMON.md#response-size-limits](../COMMON.md#response-size-limits))

---

## Related Tools

- [http_get](http_get.md) - Verify deletion
- [http_request](http_request.md) - Custom headers