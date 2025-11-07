# search_hex_pattern

Search for hex byte pattern in binary file.

**Category:** [binary-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to binary file |
| hexPattern | string | ✓ | - | Hex pattern to search (e.g., "4D5A") |
| startOffset | integer | ✗ | 0 | Starting offset for search |
| maxResults | integer | ✗ | 100 | Maximum matches to return |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| matches | integer[] | Array of byte offsets where pattern found |
| pattern | string | Pattern searched |
| matchCount | integer | Number of matches |

---

## Examples

**Find MZ headers (EXE files):**
```
search_hex_pattern(
  filePath: "C:\disk.img",
  hexPattern: "4D5A"
)
→ Finds all executables in disk image
```

**Find JPEG signatures:**
```
search_hex_pattern(
  filePath: "C:\data\recovery.bin",
  hexPattern: "FFD8FF",
  maxResults: 50
)
```

---

## Notes

- **Pattern format:** Even number of hex digits (0-9, A-F), no spaces
- **Case insensitive:** "4D5A" = "4d5a"
- **Use case:** File recovery, signature detection, pattern analysis
- **Follow-up:** Use [read_hex_bytes](read_hex_bytes.md) at found offsets

---

## Related Tools

- [read_hex_bytes](read_hex_bytes.md) - Read at found offsets
- [generate_hex_dump](generate_hex_dump.md) - View surrounding bytes