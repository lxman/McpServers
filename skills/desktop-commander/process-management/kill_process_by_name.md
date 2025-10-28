# kill_process_by_name

Terminate all processes matching a name.

**Category:** [process-management](INDEX.md)

---

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| processName | string | ✓ | - | Process name to terminate (e.g., "node", "chrome") |
| confirmation | string | ✗ | "" | Must be "KILL ALL" to execute |

---

## Returns

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation status |
| processName | string | Name searched |
| terminatedCount | integer | Processes terminated |
| terminatedIds | integer[] | Process IDs terminated |

---

## Example

```
kill_process_by_name(
  processName: "node",
  confirmation: "KILL ALL"
)
→ Terminates all node.exe processes
```

---

## Notes

- **CRITICAL:** Requires exact confirmation string "KILL ALL"
- **Matches all:** Kills every matching process
- **Case-insensitive:** "Node" = "node" = "NODE"
- **Use with caution:** Can kill multiple important processes
- **Verification:** Use [list_processes](list_processes.md) first to see what will be killed

---

## Related Tools

- [list_processes](list_processes.md) - Preview what will be killed
- [kill_process](kill_process.md) - Kill single process