using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MainCore.Services
{
    public readonly record struct OasisHit(int X, int Y, double Distance);

    public static partial class OasisCatalog
    {
        private const int MaxPages = 100;

        [GeneratedRegex("^\\s*next\\s*$", RegexOptions.IgnoreCase)]
        private static partial Regex NextLinkText();

        public static string BuildCatalogUrl(string server, int x, int y)
        {
            var host = new Uri(server).Host;
            return $"https://oasis.travibot.com/?l=en&server={host}&x={x}&y={y}";
        }

        public static string BuildOasisUrl(string server, int x, int y)
        {
            var origin = new Uri(server);
            return $"{origin.Scheme}://{origin.Host}/karte.php?x={x}&y={y}";
        }

        public static async Task<List<OasisHit>> Collect(IPage page, string catalogUrl, int originX, int originY, double maxDistance)
        {
            await page.GotoAsync(catalogUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            var found = new Dictionary<(int X, int Y), double>();
            string previousSignature = "";

            for (var pageIndex = 0; pageIndex < MaxPages; pageIndex++)
            {
                var coordinates = await ReadCoordinates(page);
                var signature = string.Join(",", coordinates.Select(coordinate => $"{coordinate.X}|{coordinate.Y}"));
                if (coordinates.Count == 0 || signature == previousSignature) break;
                previousSignature = signature;

                var reachedBeyond = false;
                foreach (var coordinate in coordinates)
                {
                    var distance = Distance(originX, originY, coordinate.X, coordinate.Y);
                    if (distance > maxDistance + 0.001)
                    {
                        reachedBeyond = true;
                        break;
                    }

                    found[(coordinate.X, coordinate.Y)] = distance;
                }

                if (reachedBeyond) break;

                var next = page.Locator("xpath=/html/body/div[2]/div[2]/ul").Locator("a").Filter(new()
                {
                    HasTextRegex = NextLinkText(),
                });
                if (await next.CountAsync() == 0) break;

                try
                {
                    await next.First.ClickAsync(new() { Timeout = 5000 });
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            return found
                .Select(item => new OasisHit(item.Key.X, item.Key.Y, item.Value))
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.X)
                .ThenBy(item => item.Y)
                .ToList();
        }

        public static double Distance(int originX, int originY, int x, int y)
        {
            var deltaX = x - originX;
            var deltaY = y - originY;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        private static async Task<List<(int X, int Y)>> ReadCoordinates(IPage page)
        {
            var jsonResult = await page.EvaluateAsync<JsonElement>(
                """
                () => Array.from(document.querySelectorAll('table a'))
                    .map(link => link.href)
                    .filter(href => href.includes('karte.php'))
                """);

            var coordinates = new List<(int X, int Y)>();
            foreach (var href in jsonResult.EnumerateArray())
            {
                if (href.ValueKind != JsonValueKind.String) continue;
                var url = href.GetString();
                if (url is null || !TryReadCoordinate(url, out var x, out var y)) continue;
                coordinates.Add((x, y));
            }

            return coordinates;
        }

        private static bool TryReadCoordinate(string href, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return false;

            int? parsedX = null;
            int? parsedY = null;
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length != 2) continue;
                if (parts[0] == "x" && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var xv)) parsedX = xv;
                if (parts[0] == "y" && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var yv)) parsedY = yv;
            }

            if (parsedX is null || parsedY is null) return false;
            x = parsedX.Value;
            y = parsedY.Value;
            return true;
        }
    }
}
