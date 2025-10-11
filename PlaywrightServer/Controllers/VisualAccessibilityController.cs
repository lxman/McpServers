using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using PlaywrightServer.Services;

namespace PlaywrightServer.Controllers;

/// <summary>
/// Visual regression and accessibility testing endpoints
/// </summary>
[ApiController]
[Route("api/visual")]
public class VisualAccessibilityController(
    PlaywrightSessionManager sessionManager,
    ILogger<VisualAccessibilityController> logger)
    : ControllerBase
{
    /// <summary>
    /// Capture screenshot of page or element
    /// </summary>
    [HttpPost("screenshot")]
    public async Task<IActionResult> CaptureScreenshot([FromBody] ElementScreenshotRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"screenshot_{timestamp}_{request.SessionId}.png";
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "screenshots", filename);
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            byte[] bytes;
            if (!string.IsNullOrEmpty(request.Selector))
            {
                string selector = DetermineSelector(request.Selector);
                ILocator element = session.Page.Locator(selector);
                bytes = await element.ScreenshotAsync();
            }
            else
            {
                bytes = await session.Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    FullPage = request.FullPage,
                    Path = outputPath
                });
            }

            await System.IO.File.WriteAllBytesAsync(outputPath, bytes);

            return Ok(new
            {
                success = true,
                filename,
                path = outputPath,
                size = bytes.Length,
                type = string.IsNullOrEmpty(request.Selector) ? "page" : "element"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error capturing screenshot");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Validate ARIA labels and accessibility attributes
    /// </summary>
    [HttpPost("validate-aria")]
    public async Task<IActionResult> ValidateAria([FromBody] ValidateAriaRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            string containerSelector = !string.IsNullOrEmpty(request.ContainerSelector)
                ? DetermineSelector(request.ContainerSelector)
                : "body";

            const string jsCode = """

                                                  (containerSelector) => {
                                                      const container = document.querySelector(containerSelector) || document.body;
                                                      const elementsNeedingLabels = container.querySelectorAll(`
                                                          input:not([type='hidden']), 
                                                          button, 
                                                          select, 
                                                          textarea,
                                                          [role='button'],
                                                          [role='textbox']
                                                      `);
                                                      
                                                      const issues = [];
                                                      
                                                      elementsNeedingLabels.forEach(el => {
                                                          const hasLabel = el.labels && el.labels.length > 0;
                                                          const hasAriaLabel = el.getAttribute('aria-label');
                                                          const hasAriaLabelledBy = el.getAttribute('aria-labelledby');
                                                          const hasTitle = el.getAttribute('title');
                                                          
                                                          if (!hasLabel && !hasAriaLabel && !hasAriaLabelledBy && !hasTitle) {
                                                              issues.push({
                                                                  element: el.tagName.toLowerCase(),
                                                                  type: el.getAttribute('type') || 'N/A',
                                                                  id: el.id || null,
                                                                  className: el.className || null,
                                                                  message: 'Element lacks accessible label'
                                                              });
                                                          }
                                                      });
                                                      
                                                      return {
                                                          totalElements: elementsNeedingLabels.length,
                                                          errors: issues.length,
                                                          passed: elementsNeedingLabels.length - issues.length,
                                                          score: Math.round(((elementsNeedingLabels.length - issues.length) / elementsNeedingLabels.length) * 100) || 0,
                                                          issues
                                                      };
                                                  }
                                  """;

            var result = await session.Page.EvaluateAsync<object>(jsCode, containerSelector);
            return Ok(new { success = true, accessibility = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating ARIA");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Check color contrast ratios for accessibility
    /// </summary>
    [HttpPost("check-contrast")]
    public async Task<IActionResult> CheckContrast([FromBody] ContrastRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            const string jsCode = """

                                                  () => {
                                                      const textElements = document.querySelectorAll('p, span, a, button, h1, h2, h3, h4, h5, h6, li, td, th');
                                                      const issues = [];
                                                      
                                                      textElements.forEach(el => {
                                                          const style = window.getComputedStyle(el);
                                                          const color = style.color;
                                                          const bgColor = style.backgroundColor;
                                                          
                                                          if (color && bgColor && bgColor !== 'rgba(0, 0, 0, 0)') {
                                                              const fontSize = parseFloat(style.fontSize);
                                                              const fontWeight = style.fontWeight;
                                                              const isLargeText = fontSize >= 18 || (fontSize >= 14 && parseInt(fontWeight) >= 700);
                                                              const requiredRatio = isLargeText ? 3 : 4.5;
                                                              
                                                              issues.push({
                                                                  text: el.textContent?.substring(0, 50),
                                                                  color,
                                                                  backgroundColor: bgColor,
                                                                  fontSize: fontSize,
                                                                  requiredRatio,
                                                                  isLargeText
                                                              });
                                                          }
                                                      });
                                                      
                                                      return {
                                                          totalElementsChecked: textElements.length,
                                                          potentialIssues: issues.length,
                                                          sample: issues.slice(0, 10)
                                                      };
                                                  }
                                  """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return Ok(new { success = true, contrast = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking contrast");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Check keyboard navigation accessibility
    /// </summary>
    [HttpPost("check-keyboard-nav")]
    public async Task<IActionResult> CheckKeyboardNavigation([FromBody] KeyboardNavRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var jsCode = """

                                         () => {
                                             const focusableElements = document.querySelectorAll(
                                                 'a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])'
                                             );
                                             
                                             const issues = [];
                                             const passed = [];
                                             
                                             focusableElements.forEach(el => {
                                                 const tabIndex = el.getAttribute('tabindex');
                                                 
                                                 if (tabIndex && parseInt(tabIndex) > 0) {
                                                     issues.push({
                                                         element: el.tagName.toLowerCase(),
                                                         issue: 'Positive tabindex detected',
                                                         tabIndex: parseInt(tabIndex)
                                                     });
                                                 } else {
                                                     passed.push({
                                                         element: el.tagName.toLowerCase(),
                                                         tabIndex: tabIndex || '0'
                                                     });
                                                 }
                                             });
                                             
                                             return {
                                                 totalFocusableElements: focusableElements.length,
                                                 issues: issues.length,
                                                 passed: passed.length,
                                                 details: { issues, passed: passed.slice(0, 5) }
                                             };
                                         }
                         """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return Ok(new { success = true, keyboardNav = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking keyboard navigation");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static string DetermineSelector(string selector)
    {
        if (selector.StartsWith('#') || selector.StartsWith('.') || selector.Contains('['))
            return selector;
        return $"[data-testid='{selector}']";
    }
}

public record ElementScreenshotRequest(string? Selector = null, bool FullPage = false, string? SessionId = "default");
public record ValidateAriaRequest(string? ContainerSelector = null, string? SessionId = "default");
public record ContrastRequest(string? SessionId = "default");
public record KeyboardNavRequest(string? SessionId = "default");