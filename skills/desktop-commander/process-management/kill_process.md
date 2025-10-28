# kill_process

Terminate a process by ID.

**Category:** [process-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| processId | integer | ✓ | - | Process ID to terminate |
| force | boolean | ✗ | false | Force kill if process doesn't respond |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| processId | integer | Process ID terminated |
| terminated | boolean | Whether termination succeeded |

---

## Examples

**Graceful termination:**
```
kill_process(processId: 1234)
→ Allows process to cleanup
```

**Force kill:**
```
kill_process(processId: 1234, force: true)
→ Immediate termination
```

---

## Notes

- **Graceful first:** Try without force first
- **Force:** Use only if process hangs or doesn't respond
- **Verification:** Use [get_process_info](get_process_info.md) before killing
- **Safety:** Double-check processId before calling

---

## Related Tools

- [list_processes](list_processes.md) - Find process
- [get_process_info](get_process_info.md) - Verify before killing
- [kill_process_by_name](kill_process_by_name.md) - Kill all matching