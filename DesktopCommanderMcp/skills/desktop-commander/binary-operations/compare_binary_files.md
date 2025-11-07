# compare_binary_files

Compare two binary files byte-by-byte.

**Category:** [binary-operations](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| file1Path | string | ✓ | - | First file path |
| file2Path | string | ✓ | - | Second file path |
| offset | integer | ✗ | 0 | Starting offset for comparison |
| length | integer | ✗ | null | Bytes to compare (null = entire file) |
| showMatches | boolean | ✗ | false | Include matching bytes in results |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| identical | boolean | Files are identical |
| differences | object[] | Array of difference objects |
| differenceCount | integer | Number of differences |
| bytesCompared | integer | Total bytes compared |

**Difference object:**
```json
{
  "offset": 42,
  "file1Byte": "4D",
  "file2Byte": "5A"
}
```

---

## Examples

**Compare files:**
```
compare_binary_files(
  file1Path: "C:\original.exe",
  file2Path: "C:\patched.exe"
)
→ Shows all byte differences
```

**Compare specific section:**
```
compare_binary_files(
  file1Path: "file1.bin",
  file2Path: "file2.bin",
  offset: 1000,
  length: 500
)
→ Compares bytes 1000-1500 only
```

---

## Notes

- **Use case:** Verify copies, detect modifications, find patches
- **Performance:** Fast comparison, differences only by default
- **showMatches:** Set true for complete comparison report

---

## Related Tools

- [read_hex_bytes](read_hex_bytes.md) - Examine differences
- [generate_hex_dump](generate_hex_dump.md) - View context