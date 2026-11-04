namespace MainCore.Services
{
    public static class AdminBrowser
    {
        private static IPlaywright? _playwright;
        private static IBrowserContext? _context;
        private static int _closing;

        public static async Task Open(string url, Action onClosed)
        {
            _playwright = await Playwright.CreateAsync();
            var profile = Path.Combine(AppContext.BaseDirectory, "Data", "Cache", "admin-panel");
            Directory.CreateDirectory(profile);

            _context = await _playwright.Chromium.LaunchPersistentContextAsync(profile, new()
            {
                Channel = "chrome",
                Headless = false,
                ViewportSize = ViewportSize.NoViewport,
                Args =
                [
                    "--ignore-certificate-errors",
                    "--no-default-browser-check",
                    "--no-first-run",
                    "--ash-no-nudges",
                    "--mute-audio",
                    "--disable-gpu",
                    "--disable-search-engine-choice-screen",
                    "--webrtc-ip-handling-policy=disable_non_proxied_udp",
                    "--force-webrtc-ip-handling-policy",
                ],
            });

            _context.Close += (_, _) =>
            {
                if (Volatile.Read(ref _closing) == 1) return;
                onClosed();
            };

            var page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();
            await page.GotoAsync(url);
        }

        public static async Task Close()
        {
            if (Interlocked.Exchange(ref _closing, 1) == 1) return;

            try
            {
                if (_context is not null) await _context.CloseAsync();
            }
            catch
            {
                // The window may already be gone.
            }

            _playwright?.Dispose();
            _playwright = null;
            _context = null;
        }
    }
}
