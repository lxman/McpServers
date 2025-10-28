# generate_hex_dump

Generate classic hex dump of binary file.

**Category:** [binary-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| filePath | string | ✓ | - | Full path to binary file |
| offset | integer | ✗ | 0 | Starting byte offset |
| length | integer | ✗ | 512 | Number of bytes to dump |
| bytesPerLine | integer | ✗ | 16 | Bytes per line (8 or 16) |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| hexDump | string | Formatted hex dump |
| bytesRead | integer | Bytes dumped |
| offset | integer | Starting offset |

---

## Example

```
generate_hex_dump(
  filePath: "C:\data\file.exe",
  offset: 0,
  length: 64,
  bytesPerLine: 16
)
→
00000000: 4D 5A 90 00 03 00 00 00 | MZ......
00000008: 04 00 00 00 FF FF 00 00 | ........
00000010: B8 00 00 00 00 00 00 00 | ¸.......
...
```

---

## Notes

- **Format:** Classic hex dump with offset, hex values, and ASCII
- **Use case:** File inspection, debugging, forensics
- **bytesPerLine:** 16 is standard, 8 for narrower display

---

## Related Tools

- [read_hex_bytes](read_hex_bytes.md) - Read specific bytes
- [search_hex_pattern](search_hex_pattern.md) - Find patterns