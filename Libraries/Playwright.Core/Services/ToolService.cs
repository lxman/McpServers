using Microsoft.Playwright;
// ReSharper disable EmptyGeneralCatchClause

namespace Playwright.Core.Services;

public class ToolService
{
    private readonly Dictionary<string, IBrowserContext> _browserContexts = new();
    private readonly Dictionary<string, IPage> _pages = new();
    private readonly Dictionary<string, IBrowser> _browsers = new();

    // Browser Context Management
    public IBrowserContext? GetBrowserContext(string contextId)
    {
        return _browserContexts.GetValueOrDefault(contextId);
    }

    public void StoreBrowserContext(string contextId, IBrowserContext context)
    {
        _browserContexts[contextId] = context;
    }

    public IPage? GetPage(string pageId)
    {
        return _pages.GetValueOrDefault(pageId);
    }

    public void StorePage(string pageId, IPage page)
    {
        _pages[pageId] = page;
    }

    public IBrowser? GetBrowser(string browserId)
    {
        return _browsers.GetValueOrDefault(browserId);
    }

    public void StoreBrowser(string browserId, IBrowser browser)
    {
        _browsers[browserId] = browser;
    }

    // Cleanup
    public async Task CleanupResources()
    {
        foreach (IPage page in _pages.Values)
        {
            try { await page.CloseAsync(); } catch { }
        }
        
        foreach (IBrowserContext context in _browserContexts.Values)
        {
            try { await context.CloseAsync(); } catch { }
        }
        
        foreach (IBrowser browser in _browsers.Values)
        {
            try { await browser.CloseAsync(); } catch { }
        }
        
        _pages.Clear();
        _browserContexts.Clear();
        _browsers.Clear();
    }
}