using System.Text.Json;

using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    /// <summary>
    /// Follows an attack sent from Scouting: looks in /report/offensive for the report of that village
    /// written after the attack left, reads it, takes the bounty off the scouted resources and saves the
    /// details. Looks again every 2 minutes until it finds it, for at most 4 hours.
    /// </summary>
    [Handler]
    public sealed partial class CheckAttackReportTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        RxQueue rxQueue,
        ILogger logger)
    {
        public sealed class Task(AccountId accountId, int cowVillageId) : AccountTask(accountId)
        {
            public int CowVillageId { get; } = cowVillageId;
            protected override string TaskName => $"Attack report #{CowVillageId}";
        }

        private static readonly TimeSpan GiveUp = TimeSpan.FromHours(4);
        private static readonly TimeSpan Again = TimeSpan.FromMinutes(2);
        private const int MaxReportsPerTurn = 8;

        private sealed record Target(string Name, int? X, int? Y, DateTime? SentUtc, string Server);

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var target = GetTarget(task);
            if (target is null || target.SentUtc is null) return Result.Ok();

            if (DateTime.UtcNow - target.SentUtc.Value > GiveUp)
            {
                Save(task, ScoutResult.AttackSent, "No apareció el reporte del ataque en 4 horas", null);
                return Result.Ok();
            }

            var root = target.Server.TrimEnd('/');
            var listUrl = AttackReportParser.ListPageUrl(root, 1);
            var result = await browser.Navigate(listUrl);
            if (result.IsFailed) return Later(task);
            try
            {
                await ScoutParser.GetReportRows(browser.CurrentPage).First.WaitForAsync(new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            var rows = await ScoutParser.ReadReportRows(browser.CurrentPage);
            var name = ScoutRules.CleanVillageName(target.Name);
            var candidates = rows
                .Where(row => name.Length == 0 || name == CowOasisParser.MissingName || row.Subject.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Take(MaxReportsPerTurn)
                .ToList();

            foreach (var row in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = ScoutParser.ReportUrl(listUrl, row.Href);
                if (url.Length == 0) continue;

                AttackReport? report = null;
                try
                {
                    result = await browser.Navigate(url);
                    if (result.IsFailed) continue;
                    report = await AttackReportParser.Read(browser.CurrentPage);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.Warning("Could not read attack report \"{Subject}\": {Message}", row.Subject, ex.Message);
                }

                if (report is null) continue;
                if (report.X != target.X || report.Y != target.Y) continue;

                // Reports are newest first: one older than the attack means this attack has not landed yet.
                if (report.TimeUtc is DateTime time && time < target.SentUtc.Value.AddMinutes(-2)) break;

                var summary = report.AttackerWon || report.Lost < report.Sent
                    ? $"Llegó: botín {report.Bounty:N0} ({report.Carried:N0}/{report.Capacity:N0}), volvieron {report.Sent - report.Lost} de {report.Sent}"
                    : $"Llegó: perdiste las {report.Sent} tropas";
                Save(task, ScoutResult.AttackLanded, summary, report);
                logger.Information("Attack report for {Village}: {Summary}", report.VillageName, summary);
                return Result.Ok();
            }

            return Later(task);
        }

        private Result Later(Task task)
        {
            task.ExecuteAt = DateTime.Now.Add(Again);
            return Result.Ok();
        }

        private void Save(Task task, string state, string message, AttackReport? report)
        {
            using var context = contextFactory.CreateDbContext();
            var row = context.ScoutResults.FirstOrDefault(x => x.CowVillageId == task.CowVillageId);
            if (row is null) return;

            row.AttackState = state;
            row.AttackMessage = message;
            row.AttackAt = DateTime.Now;
            if (report is not null)
            {
                row.AttackReportAt = DateTime.Now;
                row.AttackDetails = JsonSerializer.Serialize(report);

                // What is left after the bounty, when the scouting saw the resources.
                if (row.Wood >= 0) row.Wood = Math.Max(0, row.Wood - report.BountyWood);
                if (row.Clay >= 0) row.Clay = Math.Max(0, row.Clay - report.BountyClay);
                if (row.Iron >= 0) row.Iron = Math.Max(0, row.Iron - report.BountyIron);
                if (row.Crop >= 0) row.Crop = Math.Max(0, row.Crop - report.BountyCrop);
            }

            context.SaveChanges();
            rxQueue.Enqueue(new CowVillagesModified(task.AccountId));
        }

        private Target? GetTarget(Task task)
        {
            using var context = contextFactory.CreateDbContext();
            var village = context.CowVillages
                .Where(x => x.Id == task.CowVillageId && x.CowList.AccountId == task.AccountId.Value)
                .Select(x => new { x.Name, x.Url, x.CoordX, x.CoordY })
                .FirstOrDefault();
            if (village is null) return null;

            var times = context.ScoutResults.Where(x => x.CowVillageId == task.CowVillageId).Select(x => new { x.AttackSentUtc, x.AttackAt }).FirstOrDefault();
            // Attacks sent before AttackSentUtc existed only have AttackAt (local time of the confirmation).
            var sentUtc = times?.AttackSentUtc ?? times?.AttackAt?.ToUniversalTime();
            var server = context.Accounts.Where(x => x.Id == task.AccountId.Value).Select(x => x.Server).FirstOrDefault() ?? "";
            int? x = village.CoordX;
            int? y = village.CoordY;
            if ((x is null || y is null) && CowOasisParser.TryReadCoordinates(village.Url, out var ux, out var uy))
            {
                x = ux;
                y = uy;
            }

            // SQLite gives the date back without a kind; it was saved as UTC.
            DateTime? sent = sentUtc is null ? null : times?.AttackSentUtc is not null ? DateTime.SpecifyKind(sentUtc.Value, DateTimeKind.Utc) : sentUtc;
            return new Target(village.Name ?? "", x, y, sent, server);
        }
    }
}
