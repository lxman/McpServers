# write_file

Write content to file (overwrite or append mode).

**Category:** [file-operations](INDEX.md)  
**Common concepts:** [path parameters](../COMMON.md#path-parameters), [versionToken](../COMMON.md#versiontoken), [security](../COMMON.md#security-model)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | ✓ | - | Full path to file |
| content | string | ✓ | - | Content to write |
| mode | string | ✗ | "overwrite" | Write mode: "overwrite" or "append" |
| versionToken | string | ✗ | null | Version token for safe editing |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| path | string | Absolute path written |
| bytesWritten | integer | Bytes written to file |
| versionToken | string | New version token after write |

---

## Examples

**Overwrite file:**
```
write_file(
  path: "C:\config\app.json",
  content: "{\"version\": \"2.0\"}"
)
```

**Append to log:**
```
write_file(
  path: "C:\logs\app.log",
  content: "2025-10-21: New entry\n",
  mode: "append"
)
```

**Safe editing with version token:**
```
1. file = read_file(path: "C:\code\app.cs")
2. newContent = modify(file.content)
3. write_file(
     path: "C:\code\app.cs",
     content: newContent,
     versionToken: file.versionToken
   )
→ Fails if file changed between read and write
```

---

## Notes

- **Security:** Path must be in [allowed directories](../security-config/INDEX.md)
- **Encoding:** UTF-8
- **Version token:** Prevents race conditions, strongly recommended
- **Atomic:** Write is atomic (all-or-nothing)
- **Backup:** For safer editing with backups, use [file-editing tools](../file-editing/INDEX.md)

---

## Related Tools

- [read_file](read_file.md) - Read content and get versionToken
- [prepare_replace_lines](../file-editing/prepare_replace_lines.md) - Safe editing with backup