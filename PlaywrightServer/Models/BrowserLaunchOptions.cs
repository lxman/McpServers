using System.Text.Json;

namespace PlaywrightServer.Models;

public class BrowserLaunchOptions
{
    public int ViewportWidth { get; set; } = 1920;
    public int ViewportHeight { get; set; } = 1080;
    public string? DeviceEmulation { get; set; }
    public string? UserAgent { get; set; }
    public string? Timezone { get; set; }
    public string? Locale { get; set; }
    public string? ColorScheme { get; set; }
    public string? ReducedMotion { get; set; }
    public bool EnableGeolocation { get; set; } = false;
    public bool EnableCamera { get; set; } = false;
    public bool EnableMicrophone { get; set; } = false;
    public string? ExtraHttpHeaders { get; set; }

    /// <summary>
    /// Parse extra HTTP headers from JSON string
    /// </summary>
    public Dictionary<string, string>? GetExtraHttpHeadersDictionary()
    {
        if (string.IsNullOrEmpty(ExtraHttpHeaders))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(ExtraHttpHeaders);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get predefined device configurations
    /// </summary>
    public static DeviceConfiguration? GetDeviceConfiguration(string? deviceEmulation)
    {
        if (string.IsNullOrEmpty(deviceEmulation))
            return null;

        return deviceEmulation.ToLower() switch
        {
            "iphone12" => new DeviceConfiguration
            {
                ViewportWidth = 390,
                ViewportHeight = 844,
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.2 Mobile/15E148 Safari/604.1",
                IsMobile = true,
                HasTouch = true,
                DeviceScaleFactor = 3.0f
            },
            "iphone13" => new DeviceConfiguration
            {
                ViewportWidth = 390,
                ViewportHeight = 844,
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1",
                IsMobile = true,
                HasTouch = true,
                DeviceScaleFactor = 3.0f
            },
            "ipad" => new DeviceConfiguration
            {
                ViewportWidth = 820,
                ViewportHeight = 1180,
                UserAgent = "Mozilla/5.0 (iPad; CPU OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1",
                IsMobile = true,
                HasTouch = true,
                DeviceScaleFactor = 2.0f
            },
            "galaxy_s21" => new DeviceConfiguration
            {
                ViewportWidth = 384,
                ViewportHeight = 854,
                UserAgent = "Mozilla/5.0 (Linux; Android 11; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.120 Mobile Safari/537.36",
                IsMobile = true,
                HasTouch = true,
                DeviceScaleFactor = 2.75f
            },
            "pixel5" => new DeviceConfiguration
            {
                ViewportWidth = 393,
                ViewportHeight = 851,
                UserAgent = "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.120 Mobile Safari/537.36",
                IsMobile = true,
                HasTouch = true,
                DeviceScaleFactor = 2.75f
            },
            _ => null
        };
    }
}

public class DeviceConfiguration
{
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
    public string UserAgent { get; set; } = "";
    public bool IsMobile { get; set; } = false;
    public bool HasTouch { get; set; } = false;
    public float DeviceScaleFactor { get; set; } = 1.0f;
}