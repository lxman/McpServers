# Tool Description Trimming Guide

## Pattern to Follow

### Before (Verbose)
```csharp
[Description(@"Long description explaining:
- What the tool does
- When to use it  
- Multiple examples
- Edge cases
- Best practices")]
```

### After (Trimmed)
```csharp
[Description("Brief summary. Details: skill:<skill-name>")]
```

## Desktop Commander Tools to Trim

### HTTP Tools
| Tool | Current | New |
|------|---------|-----|
| http_get | ~150 tokens | "Execute GET request. Details: skill:desktop-commander-http" |
| http_post | ~180 tokens | "Execute POST with JSON body. Details: skill:desktop-commander-http" |
| http_put | ~140 tokens | "Execute PUT request. Details: skill:desktop-commander-http" |
| http_delete | ~100 tokens | "Execute DELETE request. Details: skill:desktop-commander-http" |
| http_request | ~200 tokens | "Execute custom HTTP request with headers. Details: skill:desktop-commander-http" |

### File Operations
| Tool | Current | New |
|------|---------|-----|
| read_file | ~120 tokens | "Read file with pagination. Details: skill:desktop-commander-files" |
| write_file | ~100 tokens | "Write/append to file. Details: skill:desktop-commander-files" |
| prepare_replace_lines | ~180 tokens | "Prepare line replacement (approval required). Details: skill:desktop-commander-files" |
| prepare_insert_after_line | ~160 tokens | "Prepare line insertion (approval required). Details: skill:desktop-commander-files" |
| find_in_file | ~130 tokens | "Search file for pattern. Details: skill:desktop-commander-files" |

### Process Management
| Tool | Current | New |
|------|---------|-----|
| list_processes | ~90 tokens | "List running processes. Details: skill:desktop-commander-process" |
| get_process_info | ~80 tokens | "Get process details. Details: skill:desktop-commander-process" |
| kill_process | ~110 tokens | "Terminate process by ID. Details: skill:desktop-commander-process" |
| kill_process_by_name | ~130 tokens | "Terminate processes by name. Details: skill:desktop-commander-process" |

### Command Execution
| Tool | Current | New |
|------|---------|-----|
| execute_command | ~150 tokens | "Execute shell command in session. Details: skill:desktop-commander-commands" |
| send_input | ~80 tokens | "Send input to terminal session. Details: skill:desktop-commander-commands" |
| get_session_output | ~70 tokens | "Get terminal session output. Details: skill:desktop-commander-commands" |

## Playwright Tools to Trim

### Browser Management (estimated 15-20% of current tokens)
| Tool Group | Current Tokens | Target Tokens | Skill Reference |
|------------|----------------|---------------|-----------------|
| Browser Launch/Control | ~500 | ~100 | playwright-browser |
| Navigation | ~300 | ~60 | playwright-browser |
| Element Interaction | ~800 | ~150 | playwright-interaction |

### Angular Testing (estimated 30-40% of current tokens)
| Tool Group | Current Tokens | Target Tokens | Skill Reference |
|------------|----------------|---------------|-----------------|
| Component Testing | ~1200 | ~250 | playwright-angular |
| Change Detection | ~800 | ~150 | playwright-angular |
| Routing | ~600 | ~120 | playwright-angular |
| NgRx Testing | ~700 | ~140 | playwright-angular |
| Signal Monitoring | ~500 | ~100 | playwright-angular |

### Accessibility Testing
| Tool Group | Current Tokens | Target Tokens | Skill Reference |
|------------|----------------|---------------|-----------------|
| ARIA Validation | ~400 | ~80 | playwright-accessibility |
| WCAG Testing | ~600 | ~120 | playwright-accessibility |
| Material Compliance | ~500 | ~100 | playwright-accessibility |

### Performance Testing
| Tool Group | Current Tokens | Target Tokens | Skill Reference |
|------------|----------------|---------------|-----------------|
| Metrics Collection | ~400 | ~80 | playwright-performance |
| Profiling | ~600 | ~120 | playwright-performance |
| Bundle Analysis | ~500 | ~100 | playwright-performance |

## Estimated Total Savings

### Desktop Commander
- Current: ~2500 tokens
- Target: ~400 tokens
- **Savings: ~2100 tokens (84%)**

### Playwright
- Current: ~8000+ tokens (rough estimate based on tool count)
- Target: ~1500 tokens
- **Savings: ~6500 tokens (81%)**

## Implementation Steps

1. Create all Skills files first
2. Verify Skills are comprehensive
3. Update tool descriptions to reference Skills
4. Test with actual AI interactions
5. Refine based on usage patterns
