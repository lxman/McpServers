# prepare_replace_in_file

PHASE 1: Prepare text pattern replacement for review.

**Category:** [file-editing](INDEX.md)  
**Workflow:** [Two-phase editing](INDEX.md#two-phase-workflow)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| searchPattern | string | ✓ | - | Text pattern to find |
| replaceWith | string | ✓ | - | Replacement text |
| versionToken | string | ✓ | - | Version token from read_file |
| useRegex | boolean | ✗ | false | Use regular expressions |
| caseSensitive | boolean | ✗ | false | Case-sensitive matching |
| createBackup | boolean | ✗ | false | Create backup file |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| approvalToken | string | Token for approve_edit |
| filePath | string | File to be edited |
| matchCount | integer | Number of matches found |
| preview | object | Shows all replacements |

---

## Example

```
1. file = read_file(path: "config.json")
2. prepare_replace_in_file(
     filePath: "config.json",
     searchPattern: "localhost:3000",
     replaceWith: "api.example.com",
     versionToken: file.versionToken
   )
   → approvalToken: "edit_789"
   → preview shows all 5 occurrences

3. approve_edit(approvalToken: "edit_789", confirmation: "APPROVE")
```

---

## Notes

- **Find and replace:** Replaces ALL occurrences
- **Preview:** Shows each match and replacement
- **Regex:** Use for complex patterns

---

## Related Tools

- [approve_edit](approve_edit.md) - Apply edit
- [cancel_edit](cancel_edit.md) - Cancel edit
- [find_in_file](../file-operations/find_in_file.md) - Preview matches first