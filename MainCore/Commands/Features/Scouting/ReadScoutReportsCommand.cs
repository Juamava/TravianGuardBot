namespace MainCore.Commands.Features.Scouting
{
    /// <summary>
    /// One turn of a Scouting list: opens /report/scouting, reads the reports of the targets that are
    /// waiting for one, visits the owners' profiles (at most every 12 hours per player) and classifies
    /// every target of the list (fat cow, thin cow or danger 1 to 10).
    /// Problems with a single report are logged and skipped; they never stop the list.
    /// </summary>
    [Handler]
    public sealed partial class ReadScoutReportsCommand(
        IChromeBrowser browser,
        IDbContextFactory<AppDbContext> contextFactory,
        RxQueue rxQueue,
        ILogger logger)
    {
        public sealed record Command(AccountId AccountId, int ListId, string Server) : ICommand;

        private const int MaxReportsPerTurn = 15;
        private static readonly TimeSpan ProfileAge = TimeSpan.FromHours(12);
        private static readonly TimeSpan ReportWait = TimeSpan.FromHours(3);

        private sealed record Pending(int Id, string Name, int? X, int? Y);

        private async ValueTask<Result> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var (accountId, listId, server) = command;
            var root = server.TrimEnd('/');
            var pending = GetPending(listId);
            var threshold = GetThreshold(listId);

            if (pending.Count > 0)
            {
                var listUrl = root + ScoutParser.ReportsPath;
                var result = await browser.Navigate(listUrl);
                if (result.IsFailed) return result;
                await WaitQuietly(ScoutParser.GetReportRows(browser.CurrentPage), 8_000);

                var rows = await ScoutParser.ReadReportRows(browser.CurrentPage);
                var names = pending.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x) && x != CowOasisParser.MissingName).ToList();
                var candidates = rows
                    .Where(row => row.Unread || names.Any(name => row.Subject.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    .Take(MaxReportsPerTurn)
                    .ToList();

                var matched = 0;
                foreach (var row in candidates)
                {
                    if (pending.Count == 0) break;
                    cancellationToken.ThrowIfCancellationRequested();

                    ScoutReport? report = null;
                    try
                    {
                        if (!browser.CurrentUrl.Contains(ScoutParser.ReportsPath, StringComparison.OrdinalIgnoreCase) || await ScoutParser.GetReport(browser.CurrentPage).CountAsync() > 0)
                        {
                            result = await browser.Navigate(listUrl);
                            if (result.IsFailed) break;
                            await WaitQuietly(ScoutParser.GetReportRows(browser.CurrentPage), 8_000);
                        }

                        result = await browser.Click(ScoutParser.GetReportLink(browser.CurrentPage, row.Index));
                        if (result.IsFailed) continue;
                        await WaitQuietly(ScoutParser.GetReport(browser.CurrentPage).First, 8_000);
                        report = await ScoutParser.ReadReport(browser.CurrentPage);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.Warning("Could not read scouting report \"{Subject}\": {Message}", row.Subject, ex.Message);
                    }

                    if (report is null) continue;
                    var target = Match(pending, report);
                    if (target is null) continue;

                    Apply(target.Id, report, threshold);
                    pending.Remove(target);
                    matched++;
                    logger.Information("Scouting report for {Village}: {Defenders} defenders, resources {Wood}/{Clay}/{Iron}/{Crop}", report.VillageName, report.Defenders, report.Wood, report.Clay, report.Iron, report.Crop);
                }

                logger.Information("Read {Matched} scouting reports for list #{ListId}, {Left} still waiting", matched, listId, pending.Count);
                ExpireOld(listId);
            }

            await RefreshProfiles(root, listId, cancellationToken);
            Classify(listId, threshold);
            rxQueue.Enqueue(new CowVillagesModified(accountId));
            return Result.Ok();
        }

        private async System.Threading.Tasks.Task RefreshProfiles(string root, int listId, CancellationToken cancellationToken)
        {
            List<string> players;
            using (var context = contextFactory.CreateDbContext())
            {
                var limit = DateTime.Now - ProfileAge;
                players = context.ScoutResults
                    .Where(x => context.CowVillages.Any(village => village.Id == x.CowVillageId && village.CowListId == listId))
                    .Where(x => x.PlayerUrl != "" && (x.ProfileAt == null || x.ProfileAt < limit))
                    .Select(x => x.PlayerUrl)
                    .Distinct()
                    .Take(10)
                    .ToList();
            }

            foreach (var player in players)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = player.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? player : root + "/" + player.TrimStart('/');
                PlayerProfile? profile = null;
                try
                {
                    var result = await browser.Navigate(url);
                    if (result.IsFailed) continue;
                    await WaitQuietly(browser.CurrentPage.Locator("#playerProfile .playerDetails"), 8_000);
                    profile = await ScoutParser.ReadProfile(browser.CurrentPage);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.Warning("Could not read the profile {Url}: {Message}", url, ex.Message);
                }

                if (profile is null) continue;
                using var context = contextFactory.CreateDbContext();
                var now = DateTime.Now;
                context.ScoutResults
                    .Where(x => x.PlayerUrl == player)
                    .ExecuteUpdate(x => x
                        .SetProperty(row => row.PlayerPopulation, profile.Population)
                        .SetProperty(row => row.PlayerVillages, profile.Villages)
                        .SetProperty(row => row.AttackPoints, profile.AttackPoints)
                        .SetProperty(row => row.DefensePoints, profile.DefensePoints)
                        .SetProperty(row => row.DaysPlaying, profile.DaysPlaying)
                        .SetProperty(row => row.HeroLevel, profile.HeroLevel)
                        .SetProperty(row => row.Alliance, profile.Alliance)
                        .SetProperty(row => row.ProfileAt, now));
            }
        }

        private void Classify(int listId, int threshold)
        {
            using var context = contextFactory.CreateDbContext();
            var rows = context.ScoutResults
                .Where(x => context.CowVillages.Any(village => village.Id == x.CowVillageId && village.CowListId == listId))
                .Where(x => x.ScoutedAt != null)
                .ToList();
            if (rows.Count == 0) return;

            var populations = rows.Select(x => x.PlayerPopulation).ToList();
            foreach (var row in rows)
            {
                var report = new ScoutReport(null, null, "", "", row.PlayerUrl, row.Wood, row.Clay, row.Iron, row.Crop, row.Cranny, row.Defenders, row.ScoutsDied, row.ReportTime);
                row.Class = ScoutRules.Classify(report, threshold);
                row.Danger = ScoutRules.DangerLevel(new DangerInput(
                    row.Defenders,
                    row.ScoutsDied,
                    row.PlayerPopulation,
                    ScoutRules.Percentile(row.PlayerPopulation, populations),
                    row.PlayerVillages,
                    row.AttackPoints,
                    row.DefensePoints,
                    !string.IsNullOrWhiteSpace(row.Alliance),
                    row.HeroLevel));
            }

            context.SaveChanges();

            foreach (var row in rows.Where(x => !x.Pending))
            {
                var label = ScoutRules.Label(row.Class, row.Danger);
                context.CowVillages.Where(x => x.Id == row.CowVillageId).ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, label));
            }
        }

        private void Apply(int cowVillageId, ScoutReport report, int threshold)
        {
            using var context = contextFactory.CreateDbContext();
            var row = context.ScoutResults.FirstOrDefault(x => x.CowVillageId == cowVillageId);
            if (row is null)
            {
                row = new ScoutResult { CowVillageId = cowVillageId };
                context.ScoutResults.Add(row);
            }

            if (row.PlayerUrl != report.PlayerUrl) row.ProfileAt = null;
            row.Pending = false;
            row.ScoutedAt = DateTime.Now;
            row.ReportTime = report.Time;
            row.Wood = report.Wood;
            row.Clay = report.Clay;
            row.Iron = report.Iron;
            row.Crop = report.Crop;
            row.Cranny = report.Cranny;
            row.Defenders = report.Defenders;
            row.ScoutsDied = report.ScoutsDied;
            row.PlayerUrl = report.PlayerUrl;
            row.Class = ScoutRules.Classify(report, threshold);
            context.SaveChanges();

            if (!string.IsNullOrWhiteSpace(report.PlayerName))
            {
                context.CowVillages
                    .Where(x => x.Id == cowVillageId && (x.Owner == null || x.Owner == ""))
                    .ExecuteUpdate(x => x.SetProperty(village => village.Owner, report.PlayerName));
            }
        }

        private void ExpireOld(int listId)
        {
            using var context = contextFactory.CreateDbContext();
            var limit = DateTime.Now - ReportWait;
            var ids = context.ScoutResults
                .Where(x => x.Pending && x.SentAt != null && x.SentAt < limit)
                .Where(x => context.CowVillages.Any(village => village.Id == x.CowVillageId && village.CowListId == listId))
                .Select(x => x.CowVillageId)
                .ToList();
            if (ids.Count == 0) return;

            context.ScoutResults.Where(x => ids.Contains(x.CowVillageId)).ExecuteUpdate(x => x.SetProperty(row => row.Pending, false));
            context.CowVillages.Where(x => ids.Contains(x.Id)).ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, "Sin reporte"));
        }

        private static Pending? Match(List<Pending> pending, ScoutReport report)
        {
            if (report.X is int x && report.Y is int y)
            {
                var byCoordinates = pending.FirstOrDefault(item => item.X == x && item.Y == y);
                if (byCoordinates is not null) return byCoordinates;
            }

            if (string.IsNullOrWhiteSpace(report.VillageName)) return null;
            return pending.FirstOrDefault(item => string.Equals(item.Name, report.VillageName, StringComparison.OrdinalIgnoreCase));
        }

        private List<Pending> GetPending(int listId)
        {
            using var context = contextFactory.CreateDbContext();
            return context.CowVillages
                .Where(x => x.CowListId == listId)
                .Where(x => context.ScoutResults.Any(result => result.CowVillageId == x.Id && result.Pending))
                .Select(x => new { x.Id, x.Name, x.Url, x.CoordX, x.CoordY })
                .AsEnumerable()
                .Select(x =>
                {
                    int? px = x.CoordX;
                    int? py = x.CoordY;
                    if ((px is null || py is null) && CowOasisParser.TryReadCoordinates(x.Url, out var ux, out var uy))
                    {
                        px = ux;
                        py = uy;
                    }
                    return new Pending(x.Id, x.Name ?? "", px, py);
                })
                .ToList();
        }

        private int GetThreshold(int listId)
        {
            using var context = contextFactory.CreateDbContext();
            return context.CowLists.Where(x => x.Id == listId).Select(x => x.FullThreshold).FirstOrDefault();
        }

        private static async System.Threading.Tasks.Task WaitQuietly(ILocator locator, float timeout)
        {
            try
            {
                await locator.First.WaitForAsync(new() { Timeout = timeout });
            }
            catch (TimeoutException)
            {
            }
        }
    }
}
