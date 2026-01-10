using Microsoft.Playwright;

namespace backend.Services
{
    /// <summary>
    /// Centralized Playwright browser host.
    /// - Launches Chromium once
    /// - Reused across requests
    /// - Container-safe
    /// </summary>
    public static class PlaywrightHost
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static IPlaywright? _playwright;
        private static IBrowser? _browser;

        public static async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser != null)
                return _browser;

            await _lock.WaitAsync();
            try
            {
                if (_browser != null)
                    return _browser;

                _playwright = await Playwright.CreateAsync();

                _browser = await _playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions
                    {
                        Headless = true,

                        // REQUIRED for Docker / Linux
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-gpu",
                            "--no-zygote",
                            "--single-process"
                        }
                    });

                return _browser;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Optional graceful shutdown (call on app exit if desired).
        /// </summary>
        public static async Task ShutdownAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;
        }
    }
}
