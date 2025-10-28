# PlaywrightServerMcp - Tool Index

## Overview

Comprehensive browser automation and Angular testing MCP server with 29 specialized tool classes.

**Framework:** .NET 9.0  
**Protocol:** MCP over STDIO  
**Shared Library:** Playwright.Core

## Tool Categories

### Core Testing Tools

- **[playwright-tools](tools/playwright-tools.md)** - Core browser operations (launch, navigate, interact)
- **[browser-management-tools](tools/browser-management-tools.md)** - Session management, tabs, viewports
- **[interaction-testing-tools](tools/interaction-testing-tools.md)** - Element interactions, keyboard, mouse
- **[visual-testing-tools](tools/visual-testing-tools.md)** - Screenshots, visual regression, styling
- **[accessibility-testing-tools](tools/accessibility-testing-tools.md)** - WCAG compliance, ARIA validation
- **[advanced-testing-tools](tools/advanced-testing-tools.md)** - Security testing, file uploads, test data
- **[network-testing-tools](tools/network-testing-tools.md)** - Request mocking, interception, HAR generation
- **[performance-testing-tools](tools/performance-testing-tools.md)** - Core Web Vitals, Lighthouse, coverage
- **[database-testing-tools](tools/database-testing-tools.md)** - MongoDB operations for test data
- **[taderatcs-testing-tools](tools/taderatcs-testing-tools.md)** - Domain-specific testing

### Angular Testing Tools

- **[style-tools](tools/angular/style-tools.md)** - Component styling analysis
- **[bundle-analyzer](tools/angular/bundle-analyzer.md)** - Bundle size analysis by component
- **[change-detection-analyzer](tools/angular/change-detection-analyzer.md)** - Change detection monitoring
- **[circular-dependency-detector](tools/angular/circular-dependency-detector.md)** - Dependency cycle detection
- **[cli-integration](tools/angular/cli-integration.md)** - Angular CLI command execution
- **[component-analyzer](tools/angular/component-analyzer.md)** - Component tree and hierarchy
- **[component-contract-testing](tools/angular/component-contract-testing.md)** - Input/Output validation
- **[configuration-analyzer](tools/angular/configuration-analyzer.md)** - angular.json analysis
- **[lifecycle-monitor](tools/angular/lifecycle-monitor.md)** - Lifecycle hook monitoring
- **[material-accessibility-testing](tools/angular/material-accessibility-testing.md)** - Material WCAG compliance
- **[ngrx-testing](tools/angular/ngrx-testing.md)** - State management testing
- **[performance-tools](tools/angular/performance-tools.md)** - Angular-specific performance
- **[routing-testing](tools/angular/routing-testing.md)** - Router, guards, resolvers
- **[service-dependency-analyzer](tools/angular/service-dependency-analyzer.md)** - DI graph analysis
- **[signal-monitor](tools/angular/signal-monitor.md)** - Signal tracking (Angular 16+)
- **[stability-detection](tools/angular/stability-detection.md)** - Wait for Angular stability
- **[style-guide-compliance](tools/angular/style-guide-compliance.md)** - Style guide validation
- **[testing-integration](tools/angular/testing-integration.md)** - Karma/Jest integration
- **[zoneless-testing](tools/angular/zoneless-testing.md)** - Zoneless migration (Angular 18+)

## Quick Start

1. **Launch Browser**
   ```
   Read: tools/playwright-tools.md → LaunchBrowser method
   ```

2. **Navigate to Angular App**
   ```
   Read: tools/playwright-tools.md → NavigateToUrl method
   Read: tools/angular/stability-detection.md → WaitForAngularStability method
   ```

3. **Analyze Bundle**
   ```
   Read: tools/angular/bundle-analyzer.md → AnalyzeBundleSizeByComponent method
   ```

## Session Management

All tools use session-based isolation. Always:
1. Launch browser with unique `sessionId`
2. Perform operations with same `sessionId`
3. Close browser when done

## Common Patterns

### Basic Automation
1. Read `playwright-tools.md`
2. Launch → Navigate → Interact → Close

### Angular Testing
1. Read `playwright-tools.md` + `angular/stability-detection.md`
2. Launch → Navigate → Wait for Stability → Test
3. Read specific Angular tool as needed

### Visual Regression
1. Read `visual-testing-tools.md`
2. Capture screenshots → Compare with baselines

### Accessibility Testing
1. Read `accessibility-testing-tools.md`
2. Run ARIA validation → Check color contrast → Test keyboard navigation

## Notes

- Each tool file contains complete parameter documentation
- Read only the tools you need to save tokens
- 120 model classes provide type-safe results
- Multi-browser support: Chrome, Firefox, WebKit
