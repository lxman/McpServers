# approve_edit

PHASE 2: Apply pending edit (requires explicit confirmation).

**Category:** [file-editing](INDEX.md)  
**Workflow:** [Two-phase editing](INDEX.md#two-phase-workflow)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| approvalToken | string | ✓ | Token from prepare_* operation |
| confirmation | string | ✓ | Must be exactly "APPROVE" |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| filePath | string | File that was edited |
| editApplied | boolean | Whether edit succeeded |
| backupCreated | string | Backup file path (if applicable) |

---

## Example

```
approve_edit(
  approvalToken: "edit_456",
  confirmation: "APPROVE"
)
→ Edit applied atomically
```

---

## Notes

- **Confirmation required:** Must pass exact string "APPROVE"
- **Atomic:** Edit succeeds or fails completely
- **Validation:** Checks versionToken still matches
- **One-time:** approvalToken invalid after use

---

## Related Tools

- [prepare_replace_lines](prepare_replace_lines.md) - Prepare edit
- [cancel_edit](cancel_edit.md) - Cancel instead
- [list_pending_edits](list_pending_edits.md) - See pending