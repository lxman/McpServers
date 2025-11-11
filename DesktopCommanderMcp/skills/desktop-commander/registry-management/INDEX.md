# Registry Management

Tools for reading, writing, and managing Windows Registry keys and values.

**See [../COMMON.md](../COMMON.md) for shared concepts: standard responses, error handling.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [read_registry_value](read-value.md) | Read a registry value | path, valueName |
| [write_registry_value](write-value.md) | Write a registry value | path, valueName, value, valueType? |
| [check_registry_key_exists](check-key-exists.md) | Check if key exists | path |
| [check_registry_value_exists](check-value-exists.md) | Check if value exists | path, valueName |
| [get_registry_key_info](get-key-info.md) | Get key structure | path |
| [list_registry_subkeys](list-subkeys.md) | List subkeys | path |
| [list_registry_values](list-values.md) | List all values | path |
| [create_registry_key](create-key.md) | Create new key | path |
| [delete_registry_key](delete-key.md) | Delete key | path, recursive? |
| [delete_registry_value](delete-value.md) | Delete value | path, valueName |
| [enumerate_registry_keys_recursive](enumerate-recursive.md) | Recursive enumeration | path, maxDepth? |

---

## Common Workflows

### Read Application Settings
```
1. check_registry_key_exists(
     path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp"
   )
   → Verify key exists

2. list_registry_values(
     path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp"
   )
   → See all settings

3. read_registry_value(
     path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp",
     valueName: "Version"
   )
   → Read specific value
```

### Create Configuration Structure
```
1. create_registry_key(
     path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings"
   )
   → Create key structure

2. write_registry_value(
     path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
     valueName: "MaxConnections",
     value: "100",
     valueType: "DWord"
   )
   → Write configuration

3. write_registry_value(
     path: "HKEY_CURRENT_USER\\Software\\MyApp\\Settings",
     valueName: "LogPath",
     value: "C:\\Logs\\MyApp",
     valueType: "String"
   )
   → Write additional settings
```

### Explore Registry Structure
```
1. get_registry_key_info(
     path: "HKEY_LOCAL_MACHINE\\SOFTWARE"
   )
   → Get overview of subkeys and values

2. enumerate_registry_keys_recursive(
     path: "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft",
     maxDepth: 2
   )
   → Explore structure with depth limit
```

### Cleanup Registry
```
1. list_registry_values(
     path: "HKEY_CURRENT_USER\\Software\\MyApp"
   )
   → Review what will be deleted

2. delete_registry_value(
     path: "HKEY_CURRENT_USER\\Software\\MyApp",
     valueName: "TempData"
   )
   → Remove specific values

3. delete_registry_key(
     path: "HKEY_CURRENT_USER\\Software\\MyApp\\Temp",
     recursive: true
   )
   → Remove entire key tree
```

---

## Registry Paths

### Common Root Keys
- `HKEY_CLASSES_ROOT` - File associations and COM registration
- `HKEY_CURRENT_USER` - Current user settings
- `HKEY_LOCAL_MACHINE` - System-wide settings
- `HKEY_USERS` - All user profiles
- `HKEY_CURRENT_CONFIG` - Current hardware profile

### Path Format
```
HKEY_LOCAL_MACHINE\SOFTWARE\MyApp\Settings
│                  │        │     └─ Subkey
│                  │        └─ Application
│                  └─ Common location
└─ Root key
```

---

## Value Types

| Type | Description | Example |
|------|-------------|---------|
| String | Text string | "Hello World" |
| DWord | 32-bit integer | 100 |
| QWord | 64-bit integer | 9223372036854775807 |
| Binary | Binary data (hex string) | "48656C6C6F" |
| MultiString | Array of strings | "Path1\nPath2\nPath3" |
| ExpandString | String with environment variables | "%TEMP%\\logs" |

---

## Best Practices

### Safety
- **Check before write:** Use check_registry_key_exists before operations
- **Backup first:** Read values before modifying
- **Test on HKEY_CURRENT_USER:** Safer than HKEY_LOCAL_MACHINE
- **Recursive delete caution:** Review structure before recursive deletion
- **Audit logs:** All operations are logged for tracking

### Permissions
- **HKEY_CURRENT_USER:** Usually accessible without elevation
- **HKEY_LOCAL_MACHINE:** Often requires administrator rights
- **Read vs Write:** Write operations require more permissions
- **Elevation:** Consider UAC requirements

### Performance
- **Batch operations:** Group related operations
- **Depth limits:** Use maxDepth in recursive operations
- **Caching:** Registry reads are typically cached by Windows

---

## Error Handling

### Common Errors
- **Access Denied:** Insufficient permissions (try elevated prompt)
- **Key Not Found:** Path doesn't exist (check spelling)
- **Value Not Found:** Value name doesn't exist
- **Invalid Type:** Type conversion failed
- **Recursive Required:** Key has subkeys but recursive=false

### Error Response
```json
{
  "success": false,
  "error": "Registry key not found: HKEY_LOCAL_MACHINE\\SOFTWARE\\Missing"
}
```

---

## Security Considerations

- **Audit logging:** All operations are logged with timestamp, user, machine
- **Sensitive data:** Avoid storing passwords in plain text
- **System stability:** Be cautious with HKEY_LOCAL_MACHINE modifications
- **Malware target:** Registry is common malware persistence location
- **Backup strategy:** Consider registry export before bulk changes

---

## Related Documentation

- [Windows Registry Documentation](https://learn.microsoft.com/windows/win32/sysinfo/registry)
- [Registry Value Types](https://learn.microsoft.com/windows/win32/sysinfo/registry-value-types)
- [Registry Functions](https://learn.microsoft.com/windows/win32/sysinfo/registry-functions)
