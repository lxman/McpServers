# Browser Management Tools

Session management, viewport control, tab management, and device emulation.

## Key Methods

### LaunchMobileBrowser
Launch browser with mobile device emulation.

**Parameters:**
- `deviceType` (string, required): Device - iphone12, iphone13, ipad, galaxy_s21, pixel5
- `browserType` (string, default: "chrome"): Browser type
- `headless` (bool, default: true): Headless mode
- `sessionId` (string, default: "default"): Session ID

---

### LaunchAccessibilityBrowser
Launch browser with accessibility testing configuration.

**Parameters:**
- `browserType`, `headless`, `sessionId`, `viewportWidth`, `viewportHeight`

---

### LaunchDarkModeBrowser
Launch browser with dark mode enabled.

**Parameters:**
- `browserType`, `headless`, `sessionId`, `viewportWidth`, `viewportHeight`

---

### SetViewportSize
Change viewport size of existing session.

**Parameters:**
- `width` (int, required): Width in pixels
- `height` (int, required): Height in pixels
- `sessionId` (string, default: "default"): Session ID

---

### GetViewportInfo
Get current browser viewport information.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

---

### OpenNewTab
Open new tab in current browser session.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `url` (string?, optional): Optional URL to navigate to

---

### SwitchToTab
Switch to a specific tab by index.

**Parameters:**
- `index` (int, required): Tab index (0-based)
- `sessionId` (string, default: "default"): Session ID

---

### EmulateDevice
Simulate mobile device.

**Parameters:**
- `deviceType` (string, required): Device type
- `sessionId` (string, default: "default"): Session ID

---

### RotateDevice
Simulate device orientation change (portrait/landscape).

**Parameters:**
- `orientation` (string, required): Orientation - portrait or landscape
- `sessionId` (string, default: "default"): Session ID

---

### ClearStorage
Clear browser storage (localStorage, sessionStorage, indexedDB, cookies).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `type` (string, default: "all"): Storage type - localStorage, sessionStorage, indexedDB, cookies, or all
