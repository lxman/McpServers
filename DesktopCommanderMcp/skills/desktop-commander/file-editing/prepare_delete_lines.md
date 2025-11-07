# prepare_delete_lines

PHASE 1: Prepare line range deletion for review.

**Category:** [file-editing](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| startLine | integer | ✓ | - | First line to delete (1-based) |
| endLine | integer | ✓ | - | Last line to delete (1-based) |
| versionToken | string | ✓ | - | Version token from read_file |
| createBackup | boolean | ✗ | false | Create backup file |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| approvalToken | string | Token for approve_edit |
| filePath | string | File to be edited |
| linesDeleted | integer | Number of lines to delete |
| preview | object | Shows lines to be deleted |

---

## Example

```
1. file = read_file(path: "code.cs")
2. prepare_delete_lines(
     filePath: "code.cs",
     startLine: 50,
     endLine: 75,
     versionToken: file.versionToken
   )
   → preview shows 26 lines to be deleted
3. approve_edit(...)
```

---

## Notes

- **Subsequent lines shift up:** Line numbers change after deletion
- **Preview carefully:** Ensure correct range

---

## Related Tools

- [approve_edit](approve_edit.md) - Apply deletion
- [cancel_edit](cancel_edit.md) - Cancel