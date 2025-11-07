# read_hex_bytes

Read bytes from binary file in hex format.

**Category:** [binary-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| filePath | string | ✓ | Full path to binary file |
| offset | integer | ✓ | Starting byte offset (0-based) |
| length | integer | ✓ | Number of bytes to read |
| format | string | ✗ | Output format: "hex-ascii" (default), "hex-only", "hex-spaced" |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| hexData | string | Hex representation of bytes |
| bytesRead | integer | Actual bytes read |
| offset | integer | Starting offset |
| format | string | Format used |

---

## Examples

**Read file header:**
```
read_hex_bytes(
  filePath: "C:\data\file.bin",
  offset: 0,
  length: 16,
  format: "hex-ascii"
)
→ "4D 5A 90 00 | MZ.."
```

**Hex only:**
```
read_hex_bytes(
  filePath: "C:\data\file.bin",
  offset: 0,
  length: 8,
  format: "hex-only"
)
→ "4D5A90000300"
```

---

## Notes

- **Offset:** 0-based byte position
- **Use case:** Inspect file headers, verify signatures, extract specific bytes
- **Formats:** hex-ascii includes ASCII column, hex-only is compact

---

## Related Tools

- [generate_hex_dump](generate_hex_dump.md) - Classic hex dump view
- [search_hex_pattern](search_hex_pattern.md) - Find byte patterns