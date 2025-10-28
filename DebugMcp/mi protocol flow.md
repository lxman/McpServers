# MI Protocol Flow Documentation

Based on netcoredbg MI protocol demonstration with FontComparator.dll

## Overview

The MI (Machine Interface) protocol follows a command-response pattern with asynchronous notifications. The protocol must handle multiple possible execution paths depending on:
- Whether breakpoints are set
- Whether breakpoints are hit
- Whether the program crashes
- Whether the program exits normally

---

## Step-by-Step Protocol Flow (Demonstrated)

### Step 0: Initial State
```
Command: netcoredbg.exe --interpreter=mi -- dotnet <path-to-dll>
Response: (gdb)
```

**State:** Debugger ready, program not started, waiting for commands.

---

### Step 1: Set Breakpoint

**Command:**
```
-break-insert C:\Users\jorda\source\repos\FontComparator\FontComparator\Program.cs:15
```

**Response:**
```
^done,bkpt={
  number="1",
  type="breakpoint",
  disp="keep",
  enabled="y",
  warning="No executable code of the debugger's target code type is associated with this line."
}
(gdb)
```

**Response Structure:**
- **Result Class:** `^done` (success)
- **Data:** `bkpt={...}` - Breakpoint object

**Breakpoint Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| number | "1" | Breakpoint ID (unique identifier) |
| type | "breakpoint" | Type: breakpoint, watchpoint, catchpoint |
| disp | "keep" | Disposition: "keep" or "del" (delete after hit) |
| enabled | "y" | "y" or "n" |
| warning | "No executable code..." | No IL code at this line yet (symbols not loaded) |

**Note:** The warning appears because the program hasn't started yet, so symbols aren't loaded.

**Alternative Scenarios:**
- **Invalid file/line:** `^error,msg="Cannot access memory at address 0x..."`
- **Valid location:** No warning field present
- **Multiple breakpoints:** Each gets unique `number`

---

### Step 2: Start Execution

**Command:**
```
-exec-run
```

**Response Sequence:**
```
^running
(gdb)
=library-loaded,id="{41cc61bc-bc19-425f-8f90-18ba4b79c072}",target-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Private.CoreLib.dll",host-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Private.CoreLib.dll",symbols-loaded="0",base-address="0x7ff8cbac0000",size="15253504"
=thread-created,id="24636"
=library-loaded,id="{6d2bf74c-c423-4c57-be50-258b2ef6b7f6}",target-name="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\bin\\Debug\\net9.0\\FontComparator.dll",host-name="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\bin\\Debug\\net9.0\\FontComparator.dll",symbols-loaded="1",base-address="0x22559080000",size="32768"
=breakpoint-modified,bkpt={number="1",type="breakpoint",disp="keep",enabled="y",func="",file="Program.cs",fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",line="15"}
=library-loaded,id="{f19539b4-fcfd-47d8-a495-c8972e3ea75b}",target-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Runtime.dll",host-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Runtime.dll",symbols-loaded="0",base-address="0x22559090000",size="57344"
=library-loaded,id="{a9b3ee48-58ca-45db-b5b5-66446f754db7}",target-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Console.dll",host-name="C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\9.0.10\\System.Console.dll",symbols-loaded="0",base-address="0x7ff9181c0000",size="163840"
*stopped,reason="entry-point-hit",thread-id="24636",stopped-threads="all",frame={file="Program.cs",fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",line="14",col="5",end-line="14",end-col="6",clr-addr={module-id="{6d2bf74c-c423-4c57-be50-258b2ef6b7f6}",method-token="0x06000001",method-version="1",il-offset="0",native-offset="46"},func="FontComparator.Program.Main()",addr="0x0000000000000000",active-statement-flags="MethodUpToDate"}
(gdb)
```

**Response Breakdown:**

#### 2.1: Result Record
```
^running
```
- Program execution has started
- Control returns immediately (async)

#### 2.2: Library Loaded Notifications (`=library-loaded`)
**Structure:**
```
=library-loaded,
  id="{guid}",
  target-name="path",
  host-name="path",
  symbols-loaded="0|1",
  base-address="0x...",
  size="bytes"
```

**Key Observations:**
- **System.Private.CoreLib.dll:** `symbols-loaded="0"` (framework, no debug info)
- **FontComparator.dll:** `symbols-loaded="1"` (user code, has symbols!)
- **System.Runtime.dll:** `symbols-loaded="0"`
- **System.Console.dll:** `symbols-loaded="0"`

#### 2.3: Thread Created Notification
```
=thread-created,id="24636"
```
- Main thread created
- Thread ID: "24636"

#### 2.4: Breakpoint Modified Notification
```
=breakpoint-modified,bkpt={
  number="1",
  type="breakpoint",
  disp="keep",
  enabled="y",
  func="",
  file="Program.cs",
  fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",
  line="15"
}
```

**Changes from Step 1:**
- **warning field removed** - Now valid!
- **file, fullname, line added** - Resolved to actual location

#### 2.5: Stopped at Entry Point
```
*stopped,
  reason="entry-point-hit",
  thread-id="24636",
  stopped-threads="all",
  frame={
    file="Program.cs",
    fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",
    line="14",
    col="5",
    end-line="14",
    end-col="6",
    clr-addr={
      module-id="{6d2bf74c-c423-4c57-be50-258b2ef6b7f6}",
      method-token="0x06000001",
      method-version="1",
      il-offset="0",
      native-offset="46"
    },
    func="FontComparator.Program.Main()",
    addr="0x0000000000000000",
    active-statement-flags="MethodUpToDate"
  }
```

**Stop Reason:** `entry-point-hit`
**Location:** Line 14 (entry point, not the breakpoint at line 15 yet)

**Frame Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| file | "Program.cs" | Source file name |
| fullname | "C:\\Users\\..." | Full path to source |
| line | "14" | Current line number |
| col | "5" | Column position |
| end-line, end-col | "14", "6" | Statement end position |
| func | "FontComparator.Program.Main()" | Current function |
| addr | "0x0000000000000000" | Native address (0 = managed) |

**CLR-Specific Address Info:**
| Field | Value | Meaning |
|-------|-------|---------|
| module-id | "{guid}" | Which assembly |
| method-token | "0x06000001" | Metadata token for method |
| method-version | "1" | Version (Edit & Continue) |
| il-offset | "0" | IL instruction offset |
| native-offset | "46" | Native code offset (JIT) |

**Active Statement Flags:**
- `MethodUpToDate` - No code changes since compilation

**Alternative Scenarios:**
- **No entry point breakpoint:** Would run until first breakpoint or exit
- **Crash on startup:** `*stopped,reason="signal-received",signal-name="SIGSEGV"`
- **No breakpoints set:** Would run to completion immediately

---

### Step 3: List Stack Frames

**Command:**
```
-stack-list-frames
```

**Response:**
```
^done,stack=[
  frame={
    level="0",
    file="Program.cs",
    fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",
    line="14",
    col="5",
    end-line="14",
    end-col="6",
    clr-addr={
      module-id="{6d2bf74c-c423-4c57-be50-258b2ef6b7f6}",
      method-token="0x06000001",
      method-version="1",
      il-offset="0",
      native-offset="46"
    },
    func="FontComparator.Program.Main()",
    addr="0x00007ff86cf0188e",
    active-statement-flags="LeafFrame,MethodUpToDate"
  }
]
(gdb)
```

**Response Structure:**
- **Result Class:** `^done`
- **Data:** `stack=[...]` - Array of frame objects

**Stack Frame Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| level | "0" | Frame depth (0 = current, 1 = caller, etc.) |
| active-statement-flags | "LeafFrame,MethodUpToDate" | LeafFrame = no nested calls |

**Alternative Scenarios:**
- **Multiple frames (nested calls):**
  ```
  stack=[
    frame={level="0", func="Inner()", ...},
    frame={level="1", func="Middle()", ...},
    frame={level="2", func="Main()", ...}
  ]
  ```
- **Empty stack (shouldn't happen in normal debugging)**
- **Optimized frames:** May have `[Optimized]` or missing source info

---

### Step 4: List Variables

**Command:**
```
-stack-list-variables
```

**Response:**
```
^done,variables=[
  {name="args",value="{string[0]}"}
]
(gdb)
```

**Response Structure:**
- **Result Class:** `^done`
- **Data:** `variables=[...]` - Array of variable objects

**Variable Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| name | "args" | Variable name (Main parameter) |
| value | "{string[0]}" | Array with 0 elements |

**Value Format:**
- **Primitives:** `"5"`, `"true"`, `"3.14"`
- **Strings:** `"\"hello\""`
- **Arrays:** `"{type[length]}"` e.g., `"{int[5]}"`
- **Objects:** `"{TypeName}"` e.g., `"{Person}"`
- **Null:** `"null"`

**Command Variants:**
- `-stack-list-variables --simple-values` - Show primitive values fully
- `-stack-list-variables --all-values` - Show all values (deep inspection)
- `-stack-list-variables --no-values` - Just names

**Alternative Scenarios:**
- **No local variables:** `variables=[]`
- **Multiple variables:**
  ```
  variables=[
    {name="x",value="42"},
    {name="name",value="\"John\""},
    {name="person",value="{Person}"}
  ]
  ```

---

### Step 5: Continue to Breakpoint

**Command:**
```
-exec-continue
```

**Response:**
```
^running
(gdb)
*stopped,
  reason="breakpoint-hit",
  thread-id="24636",
  stopped-threads="all",
  bkptno="1",
  times="1",
  frame={
    file="Program.cs",
    fullname="C:\\Users\\jorda\\source\\repos\\FontComparator\\FontComparator\\Program.cs",
    line="15",
    col="9",
    end-line="15",
    end-col="94",
    clr-addr={
      module-id="{6d2bf74c-c423-4c57-be50-258b2ef6b7f6}",
      method-token="0x06000001",
      method-version="1",
      il-offset="1",
      native-offset="47"
    },
    func="FontComparator.Program.Main()",
    addr="0x0000000000000000",
    active-statement-flags="PartiallyExecuted,MethodUpToDate"
  }
(gdb)
```

**Response Breakdown:**

#### 5.1: Running
```
^running
```
- Execution resumed

#### 5.2: Stopped at Breakpoint
```
*stopped,reason="breakpoint-hit"
```

**Stop Reason:** `breakpoint-hit` (vs `entry-point-hit`)

**New Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| bkptno | "1" | Which breakpoint was hit |
| times | "1" | How many times this breakpoint has been hit |

**Changed Fields:**
| Field | Before (Entry Point) | After (Breakpoint) |
|-------|---------------------|-------------------|
| reason | "entry-point-hit" | "breakpoint-hit" |
| line | "14" | "15" |
| il-offset | "0" | "1" |
| native-offset | "46" | "47" |
| active-statement-flags | "MethodUpToDate" | "PartiallyExecuted,MethodUpToDate" |

**Alternative Stop Reasons:**
- `step-end` - After `-exec-step`
- `function-finished` - After `-exec-finish`
- `signal-received` - Crash or exception
- `watchpoint-trigger` - Watchpoint hit
- `exited` - Program terminated

---

### Step 6: Continue to Exit

**Command:**
```
-exec-continue
```

**Response:**
```
^running
(gdb)
[Multiple =library-loaded notifications...]
[Console output from program...]
[Debug/error output...]
*stopped,reason="exited",exit-code="0"
(gdb)
```

**Final Stop:**
```
*stopped,reason="exited",exit-code="0"
```

**Exit Fields:**
| Field | Value | Meaning |
|-------|-------|---------|
| reason | "exited" | Program completed |
| exit-code | "0" | Exit code (0 = success) |

**Alternative Exit Scenarios:**
- **Non-zero exit:** `exit-code="1"` or other codes
- **Crash:** `reason="exited-signalled",signal-name="SIGSEGV"`
- **Uncaught exception:** `reason="exited",exit-code="<exception-hresult>"`

---

## Protocol Flow Variations

### Variation 1: No Breakpoints Set
```
1. -exec-run                     → ^running → *stopped,reason="exited",exit-code="0"
```
Program runs to completion immediately.

### Variation 2: Step Through Instead of Continue
```
1. -break-insert <file:line>     → ^done,bkpt={...}
2. -exec-run                     → ^running → *stopped,reason="entry-point-hit"
3. -exec-step                    → ^running → *stopped,reason="step-end"
4. -exec-step                    → ^running → *stopped,reason="step-end"
...
```

### Variation 3: Inspect Variables at Breakpoint
```
1-4. [Same as main flow]
5. -stack-list-variables         → ^done,variables=[...]
6. -var-create - * <expression>  → ^done,name="var1",...
7. -var-list-children var1       → ^done,numchild="3",children=[...]
8. -exec-continue                → ^running → ...
```

### Variation 4: Program Crashes
```
1. -exec-run                     → ^running
2. [Some execution happens]
3. *stopped,reason="signal-received",signal-name="SIGSEGV",signal-meaning="Segmentation fault"
4. -stack-list-frames            → ^done,stack=[...]  # See crash location
```

### Variation 5: Multiple Breakpoints
```
1. -break-insert file1.cs:10     → ^done,bkpt={number="1",...}
2. -break-insert file2.cs:25     → ^done,bkpt={number="2",...}
3. -exec-run                     → ^running → *stopped,bkptno="1"
4. -exec-continue                → ^running → *stopped,bkptno="2"
5. -break-delete 1               → ^done
6. -exec-continue                → ^running → *stopped,bkptno="2"  # Only bp 2 remains
```

---

## MI Record Types Summary

### Result Records (Synchronous Responses)
| Prefix | Class | Meaning |
|--------|-------|---------|
| `^` | done | Command completed successfully |
| `^` | running | Execution started |
| `^` | connected | Connected to target |
| `^` | error | Command failed |
| `^` | exit | Debugger exiting |

### Async Output Records
| Prefix | Type | Meaning |
|--------|------|---------|
| `*` | exec | Execution state changed (stopped, running) |
| `=` | notify | Notification (library-loaded, thread-created, etc.) |
| `+` | status | Status update (rarely used) |

### Stream Records (Console Output)
| Prefix | Type | Meaning |
|--------|------|---------|
| `~` | console | Target program output |
| `@` | target | Remote protocol output |
| `&` | log | Debug log messages |

---

## Common MI Commands Reference

### Execution Control
| Command | Response | Purpose |
|---------|----------|---------|
| `-exec-run` | `^running → *stopped` | Start program |
| `-exec-continue` | `^running → *stopped` | Continue execution |
| `-exec-step` | `^running → *stopped` | Step into (line) |
| `-exec-next` | `^running → *stopped` | Step over (line) |
| `-exec-finish` | `^running → *stopped` | Step out (finish function) |
| `-exec-interrupt` | `^done → *stopped` | Pause execution |

### Breakpoints
| Command | Response | Purpose |
|---------|----------|---------|
| `-break-insert <location>` | `^done,bkpt={...}` | Set breakpoint |
| `-break-delete <number>` | `^done` | Delete breakpoint |
| `-break-disable <number>` | `^done` | Disable breakpoint |
| `-break-enable <number>` | `^done` | Enable breakpoint |
| `-break-list` | `^done,BreakpointTable={...}` | List all breakpoints |
| `-break-condition <number> <expr>` | `^done` | Set condition |

### Stack Inspection
| Command | Response | Purpose |
|---------|----------|---------|
| `-stack-list-frames` | `^done,stack=[...]` | Get call stack |
| `-stack-info-frame` | `^done,frame={...}` | Current frame info |
| `-stack-select-frame <level>` | `^done` | Switch frame context |
| `-stack-list-variables` | `^done,variables=[...]` | List local variables |
| `-stack-list-arguments` | `^done,stack-args=[...]` | List function arguments |
| `-stack-list-locals` | `^done,locals=[...]` | List local variables (alt) |

### Variable Inspection
| Command | Response | Purpose |
|---------|----------|---------|
| `-var-create <name> <frame> <expr>` | `^done,name="var1",...` | Create variable object |
| `-var-list-children <name>` | `^done,numchild="N",children=[...]` | List object members |
| `-var-evaluate-expression <name>` | `^done,value="..."` | Get variable value |
| `-var-update <name>` | `^done,changelist=[...]` | Check for changes |
| `-var-delete <name>` | `^done` | Delete variable object |

### Information Commands
| Command | Response | Purpose |
|---------|----------|---------|
| `-file-list-exec-source-files` | `^done,files=[...]` | List source files |
| `-thread-info` | `^done,threads=[...]` | List threads |
| `-gdb-show` | `^done,value="..."` | Show setting |
| `-gdb-set` | `^done` | Set debugger option |

---

## Implementation Considerations for DebugMcp

### 1. State Machine Required
The debugger must track:
- **NotStarted** → `-exec-run` allowed
- **Running** → Can receive `*stopped` async
- **Stopped** → Can execute inspection commands
- **Exited** → Program finished, only `-exec-run` to restart

### 2. Async Handling
- Result records (`^`) are synchronous responses to commands
- Exec records (`*`) arrive asynchronously
- Notify records (`=`) arrive asynchronously
- Must queue/buffer async records

### 3. Command Sequencing
Some commands only valid in certain states:
- `-stack-list-frames` → Only when stopped
- `-exec-continue` → Only when stopped
- `-break-insert` → Any time (but behavior differs)

### 4. Token Matching
Commands can include token prefix:
```
001-break-insert Program.cs:15
001^done,bkpt={...}
```
Use tokens to match async responses to requests.

### 5. Error Handling
Commands can fail:
```
-break-insert BadFile.cs:999
^error,msg="No source file named BadFile.cs."
```

### 6. Multiple Sessions
Each debugging session needs isolated state:
- Breakpoint IDs
- Variable object names
- Thread IDs
- Current frame level

---

## Next Steps for Analysis

1. **Map MI protocol to MCP tool interface**
    - How do MCP tools represent breakpoints?
    - How do async notifications become MCP events?

2. **Design state management**
    - Session lifecycle
    - Command validation by state
    - Async event queuing

3. **Error handling strategy**
    - Map MI errors to MCP errors
    - Timeout handling
    - Crash recovery

4. **Variable inspection depth**
    - How deep to auto-expand objects?
    - Lazy loading for large collections?
    - Circular reference handling

5. **Multi-threading support**
    - Thread selection
    - Thread-specific operations
    - All-stop vs non-stop mode