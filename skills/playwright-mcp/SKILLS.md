# PlaywrightServerMcp Skills Guide

## 📋 Overview

PlaywrightServerMcp is a comprehensive browser automation and testing MCP server with **extensive Angular framework support**. This is one of the most feature-rich MCP servers in the ecosystem.

**Project Statistics:**
- **29 Tool Classes** organized by functional area
- **120 Model Classes** for structured data
- **19 Angular-Specific Tools** for framework testing
- **10 Core Testing Tools** for general web automation
- **Shared Playwright.Core** library for session management

**Framework:** .NET 9.0  
**Protocol:** Model Context Protocol (MCP)  
**Location:** `C:\Users\jorda\RiderProjects\McpServers\PlaywrightServerMcp`

---

## 🎯 Key Capabilities

### Angular Framework Testing (19 Tools)
- **Bundle Analysis**: Analyze bundle sizes by component with optimization recommendations
- **Change Detection**: Detect bottlenecks, validate patterns, test zoneless mode
- **Circular Dependencies**: Comprehensive detection with resolution strategies
- **CLI Integration**: Execute ng commands, generate artifacts, build projects
- **Component Analysis**: Extract hierarchy, validate contracts, analyze styles
- **Configuration**: Parse and validate angular.json
- **Lifecycle Monitoring**: Track hooks, profile render times, analyze patterns
- **Material Accessibility**: WCAG compliance testing for Material components
- **NgRx State Management**: Test actions, effects, reducers, selectors
- **Performance**: Monitor Zone.js, memory usage, system metrics
- **Routing**: Test guards, resolvers, lazy loading, navigation flows
- **Service Dependencies**: Analyze DI graph, detect circular dependencies
- **Signal Monitoring**: Track signal updates and dependencies (Angular 17+)
- **Style Guide Compliance**: Validate against Angular best practices
- **Style Analysis**: Component styling and isolation validation
- **Stability Detection**: Wait for Angular stability, check async operations
- **Testing Integration**: Execute unit tests with Karma/Jasmine
- **Zoneless Migration**: Test zoneless mode, generate migration plans

### Core Web Testing (10 Tools)
- **Browser Management**: Launch, close, configure browsers with device emulation
- **Element Interaction**: Click, fill, hover, drag-and-drop, keyboard shortcuts
- **Navigation**: URL navigation, tab management, session control
- **Visual Testing**: Screenshots, PDFs, video recording, visual regression
- **Accessibility**: ARIA validation, keyboard navigation, screen reader testing
- **Performance**: Lighthouse audits, Core Web Vitals, performance traces
- **Network**: Request interception, mock APIs, HAR generation
- **Data**: MongoDB integration, test data generation
- **Advanced**: Coverage tracking, security testing, business rule validation

---

## 📚 Documentation Structure

### Main Documentation
- **SKILLS.md** (this file): Overview and navigation
- **INDEX.md**: Quick reference index of all tools

### Tool Category Documentation
All detailed tool documentation is in the `tools/` directory:

#### Angular Framework Tools
Located in `tools/angular/`:
- `bundle-analyzer.md` - Bundle size analysis
- `change-detection-analyzer.md` - Change detection testing
- `circular-dependency-detector.md` - Dependency cycle detection
- `cli-integration.md` - Angular CLI integration
- `component-analyzer.md` - Component hierarchy analysis
- `component-contract-testing.md` - Contract validation
- `configuration-analyzer.md` - angular.json analysis
- `lifecycle-monitor.md` - Lifecycle hook monitoring
- `material-accessibility-testing.md` - Material WCAG testing
- `ngrx-testing.md` - NgRx state management testing
- `performance-tools.md` - Angular performance monitoring
- `routing-testing.md` - Routing and navigation testing
- `service-dependency-analyzer.md` - Service DI analysis
- `signal-monitor.md` - Signal tracking (Angular 17+)
- `stability-detection.md` - Stability waiting
- `style-guide-compliance.md` - Style guide validation
- `style-tools.md` - Component styling analysis
- `testing-integration.md` - Unit test execution
- `zoneless-testing.md` - Zoneless migration testing

#### Core Testing Tools
Located in `tools/`:
- `playwright-tools.md` - Core browser automation
- `browser-management-tools.md` - Browser lifecycle
- `interaction-testing-tools.md` - Element interactions
- `visual-testing-tools.md` - Visual and screenshot testing
- `accessibility-testing-tools.md` - WCAG and a11y testing
- `performance-testing-tools.md` - Performance monitoring
- `network-testing-tools.md` - Network interception
- `database-testing-tools.md` - MongoDB testing
- `advanced-testing-tools.md` - Advanced features

---

## 🚀 Quick Start Guide

### 1. Launch a Browser Session

```typescript
// Basic Chrome session
playwright:launch_browser
{
  "sessionId": "my-session",
  "browserType": "chrome",
  "headless": true
}

// Mobile device emulation
playwright:launch_mobile_browser
{
  "sessionId": "mobile-test",
  "deviceType": "iphone13"
}

// Accessibility testing configuration
playwright:launch_accessibility_browser
{
  "sessionId": "a11y-test",
  "browserType": "chrome"
}
```

### 2. Navigate and Interact

```typescript
// Navigate to URL
playwright:navigate_to_url
{
  "sessionId": "my-session",
  "url": "https://example.com"
}

// Fill form fields
playwright:fill_field
{
  "sessionId": "my-session",
  "selector": "[data-testid='email-input']",
  "value": "test@example.com"
}

// Click button
playwright:click_element
{
  "sessionId": "my-session",
  "selector": "[data-testid='submit-btn']"
}
```

### 3. Angular-Specific Testing

```typescript
// Wait for Angular to be stable
playwright:wait_for_angular_stability
{
  "sessionId": "my-session",
  "timeoutSeconds": 30
}

// Get component tree
playwright:get_angular_component_tree
{
  "sessionId": "my-session"
}

// Monitor change detection
playwright:detect_change_detection_bottlenecks
{
  "sessionId": "my-session",
  "durationSeconds": 30
}

// Test routing
playwright:test_angular_routing_scenarios
{
  "sessionId": "my-session",
  "testNavigationGuards": true,
  "testLazyLoading": true
}
```

### 4. Visual and Performance Testing

```typescript
// Capture screenshot
playwright:capture_screenshot
{
  "sessionId": "my-session",
  "fullPage": true
}

// Run Lighthouse audit
playwright:run_lighthouse_audit
{
  "sessionId": "my-session",
  "category": "performance"
}

// Monitor memory
playwright:monitor_memory_usage
{
  "sessionId": "my-session",
  "durationSeconds": 60
}
```

### 5. Clean Up

```typescript
// Close browser session
playwright:close_browser
{
  "sessionId": "my-session"
}
```

---

## 🔧 Common Testing Patterns

### Pattern 1: Complete Angular Application Test

```typescript
1. playwright:launch_browser (setup)
2. playwright:navigate_to_url (navigate)
3. playwright:wait_for_angular_stability (wait for ready)
4. playwright:get_angular_component_tree (analyze structure)
5. playwright:detect_change_detection_bottlenecks (performance)
6. playwright:validate_material_accessibility_compliance (a11y)
7. playwright:test_angular_routing_scenarios (routing)
8. playwright:analyze_bundle_size_by_component (bundle analysis)
9. playwright:close_browser (cleanup)
```

### Pattern 2: Visual Regression Testing

```typescript
1. playwright:launch_browser
2. playwright:navigate_to_url
3. playwright:capture_screenshot (baseline)
4. [make changes to application]
5. playwright:navigate_to_url
6. playwright:compare_screenshots (regression check)
7. playwright:close_browser
```

### Pattern 3: Accessibility Audit

```typescript
1. playwright:launch_accessibility_browser
2. playwright:navigate_to_url
3. playwright:validate_aria_labels
4. playwright:test_keyboard_navigation
5. playwright:test_color_contrast
6. playwright:test_screen_reader_navigation
7. playwright:validate_material_accessibility_compliance (if using Material)
8. playwright:close_browser
```

### Pattern 4: Performance Profiling

```typescript
1. playwright:launch_browser
2. playwright:navigate_to_url
3. playwright:start_performance_trace
4. [user interaction simulation]
5. playwright:stop_performance_trace
6. playwright:run_lighthouse_audit
7. playwright:monitor_memory_usage
8. playwright:get_performance_metrics
9. playwright:close_browser
```

### Pattern 5: Network Testing

```typescript
1. playwright:launch_browser
2. playwright:mock_api_response (setup mocks)
3. playwright:intercept_requests (monitor traffic)
4. playwright:navigate_to_url
5. [perform actions]
6. playwright:get_network_activity (verify calls)
7. playwright:close_browser
```

---

## 📖 Best Practices

### Session Management

1. **Use Descriptive Session IDs**: Name sessions based on their purpose
   ```typescript
   sessionId: "auth-flow-test"
   sessionId: "mobile-checkout"
   sessionId: "perf-profile"
   ```

2. **Always Clean Up**: Close browser sessions when done
   ```typescript
   playwright:close_browser { "sessionId": "my-session" }
   ```

3. **Reuse Sessions**: Multiple tools can share a session ID for efficiency

### Selector Strategy

1. **Prefer data-testid**: Most reliable selector
   ```typescript
   selector: "[data-testid='login-button']"
   ```

2. **Fallback to CSS**: When testids aren't available
   ```typescript
   selector: "button.submit-btn"
   ```

3. **Avoid XPath**: Less maintainable, slower

### Angular Testing

1. **Always Wait for Stability**: Before interacting with Angular apps
   ```typescript
   playwright:wait_for_angular_stability
   ```

2. **Test Change Detection First**: Identify performance issues early
   ```typescript
   playwright:detect_change_detection_bottlenecks
   ```

3. **Validate Component Contracts**: Ensure inputs/outputs work correctly
   ```typescript
   playwright:validate_component_contracts
   ```

4. **Monitor for Circular Dependencies**: Before they cause runtime issues
   ```typescript
   playwright:detect_circular_dependencies
   ```

### Performance Testing

1. **Use Multiple Metrics**: Don't rely on single metric
   - Lighthouse audits
   - Core Web Vitals
   - Memory monitoring
   - Change detection cycles

2. **Test on Real Devices**: Mobile emulation is good, but test on actual devices too

3. **Profile Incrementally**: Test small changes to identify impact

### Accessibility Testing

1. **Test Early and Often**: Don't save a11y for the end

2. **Use Multiple Test Types**:
   - Automated validation (ARIA, contrast)
   - Keyboard navigation testing
   - Screen reader testing

3. **Target WCAG 2.1 AA Minimum**: AAA for critical applications

---

## 🎓 Learning Resources

### Angular Framework Testing
- See `tools/angular/` directory for detailed Angular tool docs
- Focus on these first: stability-detection, component-analyzer, change-detection-analyzer
- For Angular 17+: signal-monitor, zoneless-testing

### Core Testing Fundamentals
- Start with `playwright-tools.md` for basics
- Then `browser-management-tools.md` for setup
- Progress to `interaction-testing-tools.md` for automation

### Specialized Testing
- Visual: `visual-testing-tools.md`
- Accessibility: `accessibility-testing-tools.md`
- Performance: `performance-testing-tools.md`
- Network: `network-testing-tools.md`

---

## 🔍 Tool Discovery

### Find Tools by Category

**Angular-Specific**: `tools/angular/*.md`  
**Browser Control**: `browser-management-tools.md`  
**User Interaction**: `interaction-testing-tools.md`  
**Visual Testing**: `visual-testing-tools.md`  
**Accessibility**: `accessibility-testing-tools.md`  
**Performance**: `performance-testing-tools.md`  
**Network**: `network-testing-tools.md`  
**Database**: `database-testing-tools.md`

### Find Tools by Use Case

**Testing Angular Apps**: Check all `tools/angular/*.md` files  
**Mobile Testing**: `browser-management-tools.md` → launch_mobile_browser  
**Visual Regression**: `visual-testing-tools.md`  
**API Mocking**: `network-testing-tools.md` → mock_api_response  
**Accessibility Audits**: `accessibility-testing-tools.md`  
**Performance Profiling**: `performance-testing-tools.md`  
**Form Testing**: `interaction-testing-tools.md` + `advanced-testing-tools.md`

---

## 🛠️ Advanced Workflows

### Angular Migration to Zoneless

```typescript
// Phase 1: Analyze current state
1. playwright:launch_browser
2. playwright:navigate_to_url
3. playwright:wait_for_angular_stability
4. playwright:validate_change_detection_patterns (identify issues)
5. playwright:monitor_zone_activity (baseline)

// Phase 2: Generate migration plan
6. playwright:generate_zoneless_migration_plan

// Phase 3: Test incrementally
7. [implement changes]
8. playwright:test_zoneless_change_detection
9. playwright:detect_change_detection_bottlenecks (verify improvement)
10. playwright:close_browser
```

### Comprehensive Angular Audit

```typescript
1. playwright:launch_browser
2. playwright:navigate_to_url
3. playwright:wait_for_angular_stability

// Architecture
4. playwright:get_angular_component_tree
5. playwright:detect_circular_dependencies
6. playwright:analyze_service_dependency_graph
7. playwright:validate_angular_style_guide_compliance

// Performance
8. playwright:detect_change_detection_bottlenecks
9. playwright:analyze_bundle_size_by_component
10. playwright:monitor_memory_usage

// Functionality
11. playwright:test_angular_routing_scenarios
12. playwright:test_ngrx_store_actions (if using NgRx)
13. playwright:validate_component_contracts

// Accessibility
14. playwright:validate_material_accessibility_compliance

15. playwright:close_browser
```

### E2E Test with Data Setup

```typescript
// Setup test data
1. playwright:insert_test_data (MongoDB)

// Execute test
2. playwright:launch_browser
3. playwright:navigate_to_url
4. playwright:mock_api_response (optional)
5. [perform test actions]
6. playwright:query_mongo_collection (verify data)

// Cleanup
7. playwright:cleanup_test_data (MongoDB)
8. playwright:close_browser
```

---

## 🚨 Common Pitfalls

### 1. Forgetting Angular Stability

**❌ Wrong:**
```typescript
playwright:navigate_to_url
playwright:click_element  // May fail - Angular not ready!
```

**✅ Right:**
```typescript
playwright:navigate_to_url
playwright:wait_for_angular_stability
playwright:click_element  // Safe - Angular is stable
```

### 2. Not Cleaning Up Sessions

**❌ Wrong:**
```typescript
playwright:launch_browser
playwright:navigate_to_url
// ... test finishes, session left open
```

**✅ Right:**
```typescript
playwright:launch_browser
playwright:navigate_to_url
// ... test code ...
playwright:close_browser  // Always clean up!
```

### 3. Using Generic Selectors

**❌ Wrong:**
```typescript
selector: "div > button:nth-child(3)"  // Fragile
```

**✅ Right:**
```typescript
selector: "[data-testid='submit-button']"  // Robust
```

### 4. Ignoring Performance Baselines

**❌ Wrong:**
```typescript
// Only test once, no comparison
playwright:run_lighthouse_audit
```

**✅ Right:**
```typescript
// Test before changes (baseline)
playwright:run_lighthouse_audit
// [make changes]
// Test after changes (comparison)
playwright:run_lighthouse_audit
```

---

## 📊 Tool Comparison Matrix

### When to Use Which Tool?

| Need | Tool Category | Key Tools |
|------|---------------|-----------|
| Test Angular app | Angular | stability-detection, component-analyzer |
| Find performance issues | Angular + Performance | change-detection-analyzer, bundle-analyzer |
| Accessibility audit | Accessibility | accessibility-testing-tools, material-accessibility |
| Visual regression | Visual | visual-testing-tools |
| API mocking | Network | network-testing-tools |
| Mobile testing | Browser | browser-management (device emulation) |
| Memory leaks | Performance | performance-testing-tools |
| Routing issues | Angular | routing-testing |
| State management | Angular | ngrx-testing |
| Migration to zoneless | Angular | zoneless-testing |

---

## 🔗 Related Resources

### External Documentation
- [Playwright Official Docs](https://playwright.dev/)
- [Angular Official Docs](https://angular.dev/)
- [Angular Style Guide](https://angular.dev/style-guide)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Web Vitals](https://web.dev/vitals/)

### Internal Documentation
- See `INDEX.md` for quick tool lookup
- Read individual tool files in `tools/` for detailed API docs
- Check `tools/angular/` for Angular-specific guidance

---

## 💡 Tips for Success

1. **Start Small**: Test one component/feature at a time
2. **Read Tool Docs**: Each tool file has detailed parameter docs
3. **Use Patterns**: Follow the common testing patterns in this guide
4. **Monitor Tokens**: Use INDEX.md for quick lookups to save tokens
5. **Always Clean Up**: Close browser sessions when done
6. **Test Incrementally**: Small changes → test → iterate
7. **Combine Tools**: Use multiple tools together for comprehensive testing
8. **Check Examples**: Look at the Quick Start and Common Patterns sections

---

## 📝 Quick Reference Card

### Essential Commands

```typescript
// Setup
playwright:launch_browser { "sessionId": "test" }
playwright:navigate_to_url { "url": "https://app.com" }

// Angular
playwright:wait_for_angular_stability { "timeoutSeconds": 30 }
playwright:get_angular_component_tree { }

// Interaction
playwright:fill_field { "selector": "[data-testid='input']", "value": "text" }
playwright:click_element { "selector": "[data-testid='btn']" }

// Testing
playwright:capture_screenshot { "fullPage": true }
playwright:run_lighthouse_audit { "category": "performance" }

// Cleanup
playwright:close_browser { "sessionId": "test" }
```

---

**Last Updated**: 2025-10-21  
**Version**: 1.0  
**Tool Count**: 29 classes, 19 Angular-specific  
**Documentation Files**: 28 tool docs + this guide

**Token Usage Remaining**: ~121K (64% available)