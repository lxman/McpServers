# prepare_insert_after_line

PHASE 1: Prepare content insertion for review.

**Category:** [file-editing](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to file |
| afterLine | integer | ✓ | - | Line number to insert after (1-based) |
| content | string | ✓ | - | Content to insert |
| versionToken | string | ✓ | - | Version token from read_file |
| maintainIndentation | boolean | ✗ | true | Match surrounding indentation |
| createBackup | boolean | ✗ | false | Create backup file |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| approvalToken | string | Token for approve_edit |
| insertionPoint | integer | Line after which content inserted |
| preview | object | Shows insertion |

---

## Example

```
prepare_insert_after_line(
  filePath: "README.md",
  afterLine: 10,
  content: "## New Section\nContent here...",
  versionToken: "sha256:abc",
  maintainIndentation: true
)
```

---

## Notes

- **Subsequent lines shift down:** Line numbers change after insertion
- **Indentation:** maintainIndentation matches surrounding code style

---

## Related Tools

- [approve_edit](approve_edit.md) - Apply insertion
- [analyze_indentation](../file-operations/analyze_indentation.md) - Check style