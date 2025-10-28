# prepare_replace_lines

PHASE 1: Prepare line range replacement for review.

**Category:** [file-editing](INDEX.md)  
**Workflow:** [Two-phase editing](INDEX.md#two-phase-workflow)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| startLine | integer | ✓ | - | First line to replace (1-based, inclusive) |
| endLine | integer | ✓ | - | Last line to replace (1-based, inclusive) |
| newContent | string | ✓ | - | New content (can span multiple lines) |
| versionToken | string | ✓ | - | Version token from read_file |
| createBackup | boolean | ✗ | false | Create backup file |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| approvalToken | string | Token for approve_edit |
| filePath | string | File to be edited |
| startLine | integer | Lines to be replaced (start) |
| endLine | integer | Lines to be replaced (end) |
| preview | object | Before/after preview |
| backupPath | string | Backup path (if createBackup: true) |

---

## Example

```
1. file = read_file(path: "MyClass.cs")
   → versionToken: "sha256:abc123"

2. prepare_replace_lines(
     filePath: "MyClass.cs",
     startLine: 10,
     endLine: 15,
     newContent: "public void NewMethod() {\n  return;\n}",
     versionToken: "sha256:abc123",
     createBackup: true
   )
   → approvalToken: "edit_456"
   → preview shows old vs new

3. approve_edit(approvalToken: "edit_456", confirmation: "APPROVE")
   → Edit applied
```

---

## Notes

- **PHASE 1 only:** Does NOT modify file yet
- **Review preview:** Check before approving
- **Line numbers:** 1-based, inclusive range
- **Multi-line:** newContent can contain \n for multiple lines

---

## Related Tools

- [approve_edit](approve_edit.md) - PHASE 2: Apply edit
- [cancel_edit](cancel_edit.md) - Cancel edit
- [list_pending_edits](list_pending_edits.md) - See pending