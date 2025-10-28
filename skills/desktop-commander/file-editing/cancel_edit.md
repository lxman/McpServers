# cancel_edit

Cancel a pending edit.

**Category:** [file-editing](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| approvalToken | string | ✓ | Token from prepare_* operation |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| approvalToken | string | Token that was cancelled |
| cancelled | boolean | Whether cancellation succeeded |

---

## Example

```
cancel_edit(approvalToken: "edit_456")
→ Pending edit discarded
```

---

## Notes

- **No file changes:** File remains unchanged
- **Token invalidated:** approvalToken cannot be used again
- **Use when:** Preview shows unintended changes

---

## Related Tools

- [prepare_replace_lines](prepare_replace_lines.md) - Prepare edit
- [approve_edit](approve_edit.md) - Apply instead
- [list_pending_edits](list_pending_edits.md) - See pending