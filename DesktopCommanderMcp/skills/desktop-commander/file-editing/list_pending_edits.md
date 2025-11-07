# list_pending_edits

List all edits awaiting approval.

**Category:** [file-editing](INDEX.md)

---

## Parameters

None

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| pendingEdits | object[] | Array of pending edit objects |
| totalPending | integer | Number of pending edits |

**Pending edit object:**
```json
{
  "approvalToken": "edit_456",
  "filePath": "C:\\code\\MyClass.cs",
  "operation": "replace_lines",
  "startLine": 10,
  "endLine": 15,
  "createdAt": "2025-10-21T12:00:00Z"
}
```

---

## Example

```
list_pending_edits()
→ Shows all edits waiting for approval
```

---

## Notes

- **Use case:** Review pending edits, clean up forgotten edits, batch approval
- **Audit:** Check what's pending before approval

---

## Related Tools

- [approve_edit](approve_edit.md) - Approve pending edit
- [cancel_edit](cancel_edit.md) - Cancel pending edit