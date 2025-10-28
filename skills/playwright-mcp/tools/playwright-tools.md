# PlaywrightTools

Core browser automation operations.

## Methods

### LaunchBrowser
Launch a new browser instance and create a session.

**Parameters:**
- `browserType` (string, default: "chrome"): Browser to launch - chrome, firefox, webkit
- `headless` (bool, default: true): Run in headless mode
- `sessionId` (string, default: "default"): Unique identifier for this session
- `viewportWidth` (int, default: 1920): Viewport width in pixels
- `viewportHeight` (int, default: 1080): Viewport height in pixels
- `deviceEmulation` (string?, optional): Device to emulate - iphone12, iphone13, ipad, galaxy_s21, pixel5
- `userAgent` (string?, optional): Custom user agent string
- `timezone` (string?, optional): Timezone (e.g., 'America/New_York')
- `locale` (string?, optional): Locale (e.g., 'en-US')
- `colorScheme` (string?, optional): Color scheme preference - light, dark, or null for system default
- `reducedMotion` (string?, optional): Reduce motion preference - reduce, no-preference, or null
- `enableGeolocation` (bool, default: false): Enable geolocation permissions
- `enableCamera` (bool, default: false): Enable camera permissions
- `enableMicrophone` (bool, default: false): Enable microphone permissions
- `extraHttpHeaders` (string?, optional): Extra HTTP headers as JSON object

**Returns:** string - Success message with session details

**Example:**
```
playwright:launch_browser --browserType chrome --headless true --sessionId test
```

---

### NavigateToUrl
Navigate to a URL.

**Parameters:**
- `url` (string, required): URL to navigate to
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success or error message

**Example:**
```
playwright:navigate_to_url --url https://example.com --sessionId test
```

---

### FillField
Fill a form field using CSS selector or data-testid.

**Parameters:**
- `selector` (string, required): Field selector (CSS selector or data-testid value)
- `value` (string, required): Value to fill
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success or error message

**Example:**
```
playwright:fill_field --selector "#username" --value "testuser" --sessionId test
```

---

### ClickElement
Click an element using CSS selector or data-testid.

**Parameters:**
- `selector` (string, required): Element selector (CSS selector or data-testid value)
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success or error message

**Example:**
```
playwright:click_element --selector "button[type='submit']" --sessionId test
```

---

### ExecuteJavaScript
Execute custom JavaScript on the page.

**Parameters:**
- `jsCode` (string, required): JavaScript code to execute
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Execution result or error message

**Example:**
```
playwright:execute_java_script --jsCode "document.title" --sessionId test
```

---

### SelectOption
Select an option from a dropdown.

**Parameters:**
- `selector` (string, required): Dropdown selector
- `value` (string, required): Option value to select
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success or error message

---

### GetConsoleLogs
Get console logs from a browser session.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `logType` (string?, optional): Filter by log type - log, error, warning, info, debug
- `maxLogs` (int, default: 100): Maximum number of logs to return

**Returns:** string - JSON with console logs

---

### GetNetworkActivity
Get network activity from a browser session.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `urlFilter` (string?, optional): Filter by URL pattern

**Returns:** string - JSON with network activity

---

### GetSessionDebugSummary
Get session debug summary with counts and recent activity.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with session statistics

---

### ClearSessionLogs
Clear console and network logs for a browser session.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `clearConsole` (bool, default: true): Clear console logs
- `clearNetwork` (bool, default: true): Clear network logs

**Returns:** string - Success message

---

### CloseBrowser
Close browser session and cleanup resources.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success or error message

**Example:**
```
playwright:close_browser --sessionId test
```

---

### InspectElementStyles
Extract comprehensive CSS style information for a specific element.

**Parameters:**
- `selector` (string, required): Element selector (CSS selector or data-testid value)
- `sessionId` (string, default: "default"): Session ID
- `includeAllStyles` (bool, default: false): Include all computed styles (default: false, only returns key visual properties)

**Returns:** string - JSON with element styles

## Notes

- Use descriptive session IDs to organize tests
- Always close sessions when done to free resources
- Selectors can be CSS selectors or data-testid attributes
- Playwright has built-in auto-waiting for elements
