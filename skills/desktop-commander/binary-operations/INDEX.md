# Binary Operations

Tools for reading, analyzing, and comparing binary files.

**See [../COMMON.md](../COMMON.md) for shared concepts: path parameters, standard responses.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [read_hex_bytes](read_hex_bytes.md) | Read bytes in hex format | filePath, offset, length, format? |
| [generate_hex_dump](generate_hex_dump.md) | Generate classic hex dump | filePath, offset?, length?, bytesPerLine? |
| [search_hex_pattern](search_hex_pattern.md) | Find hex pattern in file | filePath, hexPattern, startOffset?, maxResults? |
| [compare_binary_files](compare_binary_files.md) | Compare two binary files | file1Path, file2Path, offset?, length?, showMatches? |

---

## Common Workflows

### Inspecting Binary File
```
1. read_hex_bytes(filePath, offset: 0, length: 256)
   → Quick look at file header

2. generate_hex_dump(filePath, offset: 0, length: 512)
   → Classic hex dump view with ASCII
```

### Finding Patterns
```
1. search_hex_pattern(
     filePath: "data.bin",
     hexPattern: "4D5A"  // MZ header
   )
   → Find all occurrences

2. read_hex_bytes at each offset
   → Examine surrounding bytes
```

### Comparing Files
```
1. compare_binary_files(
     file1Path: "original.bin",
     file2Path: "modified.bin"
   )
   → Find all differences

2. For each difference:
   - Offset location
   - Original byte
   - New byte
```

---

## Hex Format

### Hex Pattern Format

**Input:** String of hex digits (0-9, A-F)
- Case insensitive
- No spaces or separators
- Even number of characters (pairs)

**Examples:**
```
"4D5A"        // MZ (DOS header)
"504B0304"    // PK (ZIP file)
"FFD8FF"      // JPEG header
"89504E47"    // PNG signature
```

### Output Formats

**hex-ascii** (default):
```
00000000: 4D 5A 90 00 03 00 00 00 | MZ......
```

**hex-only**:
```
4D5A900003000000
```

**hex-spaced**:
```
4D 5A 90 00 03 00 00 00
```

---

## Best Practices

### File Headers

1. **Common file signatures:**
   ```
   PDF:  25 50 44 46        // %PDF
   PNG:  89 50 4E 47        // .PNG
   JPEG: FF D8 FF           // ÿØÿ
   ZIP:  50 4B 03 04        // PK..
   EXE:  4D 5A              // MZ
   ```

2. **Verify file type:**
   ```
   read_hex_bytes(filePath, offset: 0, length: 8)
   → Check against known signatures
   ```

### Offset and Length

1. **Start small:**
    - Read 256-512 bytes first
    - Expand if needed
    - Avoid loading entire large files

2. **Strategic offsets:**
    - 0: File header
    - End of file: Footer/trailer
    - Known structure offsets

### Pattern Searching

1. **Be specific:**
    - Longer patterns = fewer false positives
    - Use known fixed sequences
    - Consider endianness

2. **Limit results:**
    - Set `maxResults` for large files
    - Process incrementally if many matches

---

## Common Use Cases

### File Type Detection
```
read_hex_bytes(filePath, offset: 0, length: 16)
→ Compare header to known signatures
→ Determine actual file type
```

### Data Recovery
```
search_hex_pattern(
  filePath: "disk.img",
  hexPattern: "FFD8FF"  // JPEG header
)
→ Find JPEG files in disk image
→ Extract data at each offset
```

### Binary Diff
```
compare_binary_files(
  file1Path: "version1.exe",
  file2Path: "version2.exe"
)
→ Identify changes between versions
→ Locate patches or modifications
```

### Structure Analysis
```
1. generate_hex_dump(filePath, offset: 0, length: 1024)
   → View structured data
   → Identify field boundaries

2. read_hex_bytes for specific fields
   → Extract exact bytes
   → Parse data structures
```

---

## Security Considerations

1. **Large file handling:**
    - Set reasonable length limits
    - Don't load entire files into memory
    - Use pagination for results

2. **Malware analysis:**
    - Work in isolated environment
    - Don't execute suspicious binaries
    - Use read-only operations

3. **Data sensitivity:**
    - Binary files may contain sensitive data
    - Audit access to encrypted files
    - Handle credentials carefully

---

## Performance Tips

1. **Optimize searches:**
    - Use specific patterns (reduce matches)
    - Set `maxResults` limit
    - Start from known offsets when possible

2. **Comparison efficiency:**
    - Use `offset` and `length` to compare specific sections
    - Set `showMatches: false` for faster comparison
    - Compare file sizes first

3. **Hex dumps:**
    - Use reasonable `bytesPerLine` (16 is standard)
    - Limit `length` for display purposes
    - Generate full dumps only when needed

---

## Understanding Hex Dumps

### Standard Format
```
Offset    Hex Values                      ASCII
00000000: 4D 5A 90 00 03 00 00 00 | MZ......
00000008: 04 00 00 00 FF FF 00 00 | ........
00000010: B8 00 00 00 00 00 00 00 | ¸.......
```

**Columns:**
- **Offset:** Position in file (hex)
- **Hex Values:** Raw byte values
- **ASCII:** Printable characters (. for non-printable)

### Reading Hex Dumps

1. **Identify structures:**
    - Look for repeated patterns
    - Identify null bytes (00)
    - Find text strings in ASCII column

2. **Determine endianness:**
   ```
   Little-endian: 0x12345678 → 78 56 34 12
   Big-endian:    0x12345678 → 12 34 56 78
   ```

3. **Calculate offsets:**
    - Each line typically 16 bytes (0x10)
    - Offset + relative position = byte location

---

## Troubleshooting

### Pattern Not Found
```
Issue: search_hex_pattern returns no results
Solutions:
  - Verify hex pattern is correct
  - Check endianness
  - Try searching with wildcards (if supported)
  - Expand search range
```

### Invalid Hex String
```
Issue: "Invalid hex pattern"
Solutions:
  - Ensure even number of hex digits
  - Use only 0-9, A-F characters
  - Remove spaces and separators
  - Check for typos
```

### Large File Performance
```
Issue: Operations slow on large files
Solutions:
  - Reduce length parameter
  - Use specific offsets
  - Set maxResults limit
  - Process in chunks
```

---

**Total Tools:** 4  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**