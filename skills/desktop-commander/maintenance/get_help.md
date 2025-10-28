# get_help

Get help information about Desktop Commander capabilities and available tools.

**Category:** [maintenance](INDEX.md)  
**See also:** [../COMMON.md](../COMMON.md)

---

## Parameters

None - this tool takes no parameters.

---

## Returns

```json
{
  "success": boolean,
  "version": "1.0.0",
  "serverName": "Desktop Commander MCP",
  "categories": [
    {
      "name": "file-operations",
      "toolCount": 13,
      "description": "File system operations"
    },
    {
      "name": "http-operations",
      "toolCount": 5,
      "description": "HTTP client operations"
    }
    // ... other categories
  ],
  "totalTools": 50,
  "endpoints": {
    "documentation": "https://docs.example.com",
    "support": "https://support.example.com"
  }
}
```

---

## Example Usage

```
get_help()
→ Returns complete tool reference
```

---

## Use Cases

### Discovering Capabilities
```
New user wants to know what Desktop Commander can do:
  1. get_help()
  2. Review categories
  3. See tool counts
  4. Identify relevant category
  5. Read category INDEX.md
```

### API Exploration
```
Developer integrating with Desktop Commander:
  1. get_help()
  2. Get version information
  3. List all categories
  4. Enumerate tools per category
  5. Read detailed documentation
```

### Quick Reference
```
User forgets what's available:
  1. get_help()
  2. Scan category names
  3. Find relevant category
  4. Navigate to specific tool
```

### Version Checking
```
Troubleshooting compatibility:
  1. get_help()
  2. Check version field
  3. Compare to requirements
  4. Verify feature availability
```

---

## Response Details

### Version Information
- **version:** Semantic version (e.g., "1.0.0")
- **serverName:** Official server name
- Used for compatibility checking

### Category Information
Each category includes:
- **name:** Category identifier
- **toolCount:** Number of tools in category
- **description:** Brief category description

### Endpoint Information
- **documentation:** Link to full docs
- **support:** Link to support/issues
- **repository:** Link to source code (if public)

---

## Best Practices

1. **First-time setup:**
    - Run `get_help()` to understand capabilities
    - Review category list
    - Navigate to relevant categories

2. **Integration:**
    - Check version compatibility
    - Enumerate available tools
    - Validate required tools exist

3. **Documentation:**
    - Use as starting point
    - Navigate to detailed docs via links
    - Reference category INDEX files

---

## Navigation Path

After `get_help()`:
```
1. Identify relevant category
2. Read ../[category]/INDEX.md
3. Find specific tool
4. Read ../[category]/[tool].md
```

**Example:**
```
Need to read files:
  1. get_help() → See file-operations category
  2. Read ../file-operations/INDEX.md
  3. Find read_file tool
  4. Read ../file-operations/read_file.md
```

---

## Complete Tool List

For complete alphabetical tool listing with parameters, see [../INDEX.md](../INDEX.md).

For category-organized reference, use:
- [file-operations](../file-operations/INDEX.md) - 13 tools
- [http-operations](../http-operations/INDEX.md) - 5 tools
- [binary-operations](../binary-operations/INDEX.md) - 4 tools
- [process-management](../process-management/INDEX.md) - 4 tools
- [command-execution](../command-execution/INDEX.md) - 5 tools
- [file-editing](../file-editing/INDEX.md) - 7 tools
- [service-management](../service-management/INDEX.md) - 5 tools
- [security-config](../security-config/INDEX.md) - 5 tools
- [maintenance](../maintenance/INDEX.md) - 3 tools

---

## Notes

- Tool counts and descriptions may vary by version
- Always check version for compatibility
- Use detailed documentation for parameter specifics
- This is a high-level overview tool
- For specific tool details, navigate to tool documentation

---

**See [INDEX.md](INDEX.md) for maintenance category overview**