# Interaction Testing Tools

Element interaction, keyboard input, drag-and-drop, file uploads, and form testing.

## Key Methods

### HoverElement
Simulate hover effects on element.

**Parameters:**
- `selector` (string, required): Element selector
- `sessionId` (string, default: "default"): Session ID

---

### DragAndDrop
Drag and drop between elements.

**Parameters:**
- `sourceSelector` (string, required): Source element
- `targetSelector` (string, required): Target element
- `sessionId` (string, default: "default"): Session ID

---

### SendKeyboardShortcut
Send keyboard shortcuts (Ctrl+S, Tab, complex sequences).

**Parameters:**
- `keys` (string, required): Keyboard shortcut (e.g., 'Ctrl+S', 'Alt+Tab', 'Cmd+C', 'Tab Tab Enter')
- `sessionId` (string, default: "default"): Session ID

---

### UploadFiles
Upload files for testing.

**Parameters:**
- `selector` (string, required): File upload selector
- `filePaths` (string[], required): File paths or test file types (pdf, jpg, png, invalid)
- `sessionId` (string, default: "default"): Session ID

---

### WaitForDownload
Handle file downloads with support for multiple concurrent downloads.

**Parameters:**
- `triggerSelector` (string, required): Selector that triggers download
- `sessionId` (string, default: "default"): Session ID
- `timeoutSeconds` (int, default: 30): Timeout to wait
- `expectedFileName` (string?, optional): Expected filename pattern for verification

**Returns:** string - JSON with download info

---

### ListActiveDownloads
List all active downloads for a session.

---

### CleanupDownloads
Clean up downloaded files.

**Parameters:**
- `sessionId`, `downloadId`, `deleteFiles`
