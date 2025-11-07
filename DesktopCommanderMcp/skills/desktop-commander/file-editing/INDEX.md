# File Editing

Two-phase file editing system for safe, reviewable modifications with automatic backups.

**See [../COMMON.md](../COMMON.md) for shared concepts: versionToken, approvalToken, path parameters.**

---

## Tools Quick Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| [prepare_replace_lines](prepare_replace_lines.md) | Prepare line range replacement | filePath, startLine, endLine, newContent, versionToken |
| [prepare_replace_in_file](prepare_replace_in_file.md) | Prepare text pattern replacement | filePath, searchPattern, replaceWith, versionToken, useRegex? |
| [prepare_delete_lines](prepare_delete_lines.md) | Prepare line range deletion | filePath, startLine, endLine, versionToken |
| [prepare_insert_after_line](prepare_insert_after_line.md) | Prepare content insertion | filePath, afterLine, content, versionToken |
| [approve_edit](approve_edit.md) | Apply pending edit | approvalToken, confirmation |
| [cancel_edit](cancel_edit.md) | Cancel pending edit | approvalToken |
| [list_pending_edits](list_pending_edits.md) | List all pending edits | (none) |

---

## Two-Phase Workflow

### Why Two Phases?

**Safety:**
- Preview changes before applying
- Prevent accidental overwrites
- Review for unintended consequences

**Reliability:**
- Automatic version checking
- Backup creation (optional)
- Atomic operations

**Visibility:**
- See exactly what will change
- Verify line numbers and content
- Confirm before commitment

### Workflow Steps

```
PHASE 1: PREPARE
  1. Read file → get versionToken
  2. prepare_* operation
     → Returns approvalToken + preview
     → Edit is PENDING, not applied yet

PHASE 2: APPROVE
  3. Review preview
  4. approve_edit(approvalToken, confirmation: "APPROVE")
     → Edit applied atomically
     → approvalToken invalidated

ALTERNATIVE: CANCEL
  3. cancel_edit(approvalToken)
     → Discards pending edit
```

---

## Common Workflows

### Replace Lines (Most Common)
```
1. read_file(path: "MyClass.cs")
   → content: "..." 
   → versionToken: "sha256:abc123"

2. prepare_replace_lines(
     filePath: "MyClass.cs",
     startLine: 10,
     endLine: 15,
     newContent: "// New implementation\npublic void Method() { }",
     versionToken: "sha256:abc123",
     createBackup: true
   )
   → approvalToken: "edit_456"
   → preview: shows old vs new

3. approve_edit(
     approvalToken: "edit_456",
     confirmation: "APPROVE"
   )
   → Edit applied
   → Backup created: MyClass.cs.backup.20251021120000
```

### Find and Replace
```
1. read_file(path: "config.json")
   → versionToken: "sha256:def789"

2. prepare_replace_in_file(
     filePath: "config.json",
     searchPattern: "localhost:3000",
     replaceWith: "api.example.com",
     versionToken: "sha256:def789",
     useRegex: false
   )
   → approvalToken: "edit_789"
   → preview: shows all replacements

3. approve_edit(approvalToken: "edit_789", confirmation: "APPROVE")
```

### Delete Lines
```
1. read_file(path: "code.cs")
   → versionToken: "sha256:xyz111"

2. prepare_delete_lines(
     filePath: "code.cs",
     startLine: 50,
     endLine: 75,
     versionToken: "sha256:xyz111"
   )
   → approvalToken: "edit_111"
   → preview: shows lines to be deleted

3. approve_edit(approvalToken: "edit_111", confirmation: "APPROVE")
```

### Insert Content
```
1. read_file(path: "README.md")
   → versionToken: "sha256:abc999"

2. prepare_insert_after_line(
     filePath: "README.md",
     afterLine: 10,
     content: "## New Section\nContent here...",
     versionToken: "sha256:abc999"
   )
   → approvalToken: "edit_999"

3. approve_edit(approvalToken: "edit_999", confirmation: "APPROVE")
```

---

## Prepare Operations Details

### prepare_replace_lines

**Use for:**
- Replacing specific line ranges
- Updating methods/functions
- Changing configuration blocks

**Line numbering:**
- 1-based (first line = 1)
- Inclusive range (both start and end lines replaced)
- Can span multiple lines

**Example:**
```
Lines 10-15 contain:
  10: public void Old() {
  11:   // old code
  12:   return;
  13: }
  14: 
  15: // comment

Replace 10-15 with 3 new lines:
  newContent: "public void New() {\n  return true;\n}"

Result:
  10: public void New() {
  11:   return true;
  12: }
  (lines 13-15 removed, replaced by above)
```

### prepare_replace_in_file

**Use for:**
- Find-and-replace operations
- Updating repeated patterns
- Changing all occurrences

**Search modes:**
- `useRegex: false` - Simple text matching
- `useRegex: true` - Regular expression patterns
- `caseSensitive: false` (default) - Ignore case
- `caseSensitive: true` - Match exact case

**Example:**
```
Find all: "TODO"
Replace with: "DONE"
→ All occurrences changed
```

### prepare_delete_lines

**Use for:**
- Removing code blocks
- Deleting comments
- Cleaning up unused sections

**Behavior:**
- Lines deleted completely
- Subsequent lines shift up
- Line numbers change after deletion

### prepare_insert_after_line

**Use for:**
- Adding new content
- Inserting documentation
- Adding imports/dependencies

**Behavior:**
- Content inserted after specified line
- Existing lines shift down
- `maintainIndentation: true` - Matches surrounding indentation

---

## Approval Process

### approve_edit

**Required confirmation:** `"APPROVE"` (exact string)

**Validates:**
- approvalToken exists and is valid
- File hasn't changed since prepare
- versionToken still matches
- All preconditions met

**On success:**
- Edit applied atomically
- Backup created (if requested)
- approvalToken invalidated
- Success response with details

**On failure:**
- Edit NOT applied
- File remains unchanged
- Error message explains why
- Can retry prepare if needed

### cancel_edit

**Use when:**
- Preview shows unexpected changes
- Made a mistake in prepare
- Changed mind about edit
- Want to try different approach

**Effect:**
- Pending edit discarded
- approvalToken invalidated
- File unchanged
- No backup created

---

## Best Practices

### Always Use versionToken

1. **Read file first:**
   ```
   file = read_file(path)
   versionToken = file.versionToken
   ```

2. **Pass to prepare:**
   ```
   prepare_*(... versionToken: versionToken)
   ```

3. **Handles race conditions:**
    - If file changed: prepare fails
    - Prevents overwriting other changes
    - Forces re-read and retry

### Review Previews

1. **Check preview carefully:**
    - Line numbers correct?
    - Content as expected?
    - No unintended changes?

2. **Verify surrounding context:**
    - Lines before/after unchanged?
    - Indentation preserved?
    - No unexpected side effects?

3. **Cancel if unsure:**
    - Better safe than sorry
    - Re-read file if needed
    - Adjust and retry

### Backup Strategy

1. **Enable for important files:**
   ```
   createBackup: true  // Recommended for production
   ```

2. **Disable for temporary files:**
   ```
   createBackup: false  // OK for tests, temp files
   ```

3. **Clean up old backups:**
    - Use [maintenance/cleanup_backup_files](../maintenance/cleanup_backup_files.md)
    - Set retention policy
    - Monitor disk usage

### Error Recovery

1. **versionToken mismatch:**
   ```
   Error: "Version token mismatch"
   → File was modified
   → Re-read file: get new versionToken
   → Retry prepare operation
   ```

2. **approvalToken expired:**
   ```
   Error: "Approval token not found"
   → Token may have timed out
   → Re-prepare edit
   → Approve more quickly
   ```

3. **File locked:**
   ```
   Error: "File is in use"
   → Close applications using file
   → Retry operation
   → Or wait for lock release
   ```

---

## Common Use Cases

### Code Refactoring
```
1. Read source file
2. prepare_replace_lines for each method
3. Review all changes
4. approve_edit for each
5. Test changes
```

### Configuration Updates
```
1. prepare_replace_in_file(
     searchPattern: "env=dev",
     replaceWith: "env=prod"
   )
2. Review all occurrences
3. approve_edit
```

### Documentation Maintenance
```
1. prepare_insert_after_line(
     afterLine: <section-end>,
     content: "## New Section\n..."
   )
2. Verify placement
3. approve_edit
```

### Bulk Cleanup
```
For each file:
  1. Find deprecated patterns
  2. prepare_replace_in_file
  3. Review preview
  4. approve_edit or cancel_edit
```

---

## Security Considerations

1. **File access:**
    - Files must be in allowed directories
    - See [security-config](../security-config/INDEX.md)
    - Test access before editing

2. **Backup protection:**
    - Backups follow same security rules
    - Created in same directory as original
    - Subject to cleanup policies

3. **Audit logging:**
    - All prepare operations logged
    - All approve/cancel operations logged
    - Includes approvalToken and changes made
    - Review with [maintenance/get_audit_log](../maintenance/get_audit_log.md)

---

## Performance Tips

1. **Batch related edits:**
    - Prepare multiple edits
    - Review all together
    - Approve in sequence

2. **Use appropriate tool:**
    - Line ranges: `prepare_replace_lines`
    - Pattern matching: `prepare_replace_in_file`
    - Choose based on use case

3. **Minimize re-reads:**
    - Cache versionToken
    - Reuse for multiple prepares on same file
    - Re-read only if file changed

---

## Pending Edits Management

### list_pending_edits

Shows all edits awaiting approval:

```json
{
  "pendingEdits": [
    {
      "approvalToken": "edit_456",
      "filePath": "C:\\code\\MyClass.cs",
      "operation": "replace_lines",
      "startLine": 10,
      "endLine": 15,
      "createdAt": "2025-10-21T12:00:00Z"
    }
  ],
  "totalPending": 1
}
```

**Use cases:**
- Review what's pending
- Clean up forgotten edits
- Audit before approval
- Batch approval workflow

---

## Troubleshooting

### Preview Shows Wrong Lines
```
Issue: Preview shows unexpected lines
Solutions:
  - Verify line numbers (1-based)
  - Check file hasn't changed
  - Re-read file to get current state
  - Confirm correct file path
```

### Approval Fails
```
Issue: approve_edit fails even after successful prepare
Solutions:
  - Check confirmation string is exactly "APPROVE"
  - Verify approvalToken is correct
  - File may have changed (check versionToken)
  - Token may have expired (re-prepare)
```

### Indentation Wrong
```
Issue: Inserted content has wrong indentation
Solutions:
  - Use maintainIndentation: true
  - Manually format newContent
  - Check source file indentation style
  - Use analyze_indentation first
```

### Backup Not Created
```
Issue: createBackup: true but no backup file
Solutions:
  - Check directory is writable
  - Verify allowed directory access
  - Check disk space
  - Review error message
```

---

**Total Tools:** 7  
**See [../INDEX.md](../INDEX.md) for complete Desktop Commander reference**