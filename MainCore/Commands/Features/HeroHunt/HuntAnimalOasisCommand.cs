using MainCore.Infrasturecture.Extensions;

namespace MainCore.Commands.Features.HeroHunt
{
    /// <summary>
    /// Called by RaidCowListTask for an oasis WITH animals on a list with "Levear héroe".
    /// Must start on the oasis tile page (top bar + simulator link). Decides with
    /// <see cref="HeroLevelingRules.DecideAnimalOasis"/>: reads HP once per run, collects
    /// tasks once per run when HP is low, then simulates and sends the hero alone only when
    /// the simulation is safe. Returns the short status for CowVillages.LastSkipReason.
    /// Never returns a failure for hero problems: the troop raid of the list goes on.
    /// </summary>
    [Handler]
    public sealed partial class HuntAnimalOasisCommand(
        IChromeBrowser browser,
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger logger,
        ReadHeroStatsCommand.Handler readHeroStatsCommand,
        CollectHeroTasksCommand.Handler collectHeroTasksCommand,
        SimulateHeroRaidCommand.Handler simulateHeroRaidCommand,
        SendHeroRaidCommand.Handler sendHeroRaidCommand)
    {
        public sealed record Command(string Server, int ListVillageId, string OasisUrl, int Animals, HeroRunState Run) : ICommand;

        private async ValueTask<Result<string>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var (server, listVillageId, oasisUrl, animals, run) = command;

            if (!HeroLevelingRules.TryParseCoordinates(oasisUrl, out var x, out var y)) return HeroLevelingRules.StatusNotSimulated;

            // Everything we need from the tile page before navigating away.
            var link = CowOasisParser.GetSimulatorLink(browser.CurrentPage);
            var kid = await link.CountAsync() > 0 ? CombatSimulatorParser.ParseKid(await link.GetAttributeAsync("href")) : null;
            var heroHome = await HeroParser.IsHeroHome(browser.CurrentPage);
            var heroVillageId = await HeroParser.GetHeroVillageId(browser.CurrentPage);


            for (var step = 0; step < 4; step++)
            {
                if (cancellationToken.IsCancellationRequested) return Cancel.Error;

                var action = HeroLevelingRules.DecideAnimalOasis(true, run, heroHome, heroVillageId, listVillageId);
                logger.Information("Hero at ({X}|{Y}): home {Home}, HP {Hp}%, {Action}", x, y, heroHome, run.HpPercent, action);

                switch (action)
                {
                    case AnimalOasisAction.NeedHeroStats:
                        {
                            var read = await readHeroStatsCommand.HandleAsync(new(), cancellationToken);
                            run.StatsRead = true;
                            if (read.IsFailed) return Save(HeroLevelingRules.StatusUnknown, "No pude leer la vida del héroe.");
                            run.Stats = read.Value;
                            run.HpPercent = read.Value.HpPercent;
                            // Re-check home on the attributes page (same top bar).
                            heroHome = await HeroParser.IsHeroHome(browser.CurrentPage);
                            continue;
                        }

                    case AnimalOasisAction.CollectTasks:
                        {
                            run.TasksCollected = true;
                            var collected = await collectHeroTasksCommand.HandleAsync(new(), cancellationToken);
                            if (collected.IsFailed)
                            {
                                logger.Warning("Collecting tasks failed: {Error}", string.Join(" ", collected.Errors.Select(e => e.Message)));
                                return Save(HeroLevelingRules.StatusLowHp, $"El héroe tiene {run.HpPercent}% de vida y no pude cobrar las tareas.");
                            }
                            if (collected.Value is HeroStats after)
                            {
                                run.Stats = after;
                                run.HpPercent = after.HpPercent;
                                heroHome = await HeroParser.IsHeroHome(browser.CurrentPage);
                            }
                            continue;
                        }

                    case AnimalOasisAction.HeroAway:
                        run.SeenAway = true;
                        return HeroLevelingRules.StatusAway;

                    case AnimalOasisAction.HeroLowHp:
                        return Save(HeroLevelingRules.StatusLowHp, $"El héroe tiene {run.HpPercent}% de vida (mínimo {HeroLevelingRules.MinHpPercent}%). Espera a curarse.");

                    case AnimalOasisAction.HeroUnknownHp:
                    case AnimalOasisAction.HeroOtherVillage:
                    case AnimalOasisAction.HeroAlreadySent:
                    case AnimalOasisAction.SkipTroops:
                        return HeroLevelingRules.StatusFor(action);

                    case AnimalOasisAction.TryHero:
                        return await TryHero(server, heroVillageId!.Value, kid, x, y, run.Stats, animals, run, cancellationToken);
                }
            }

            return HeroLevelingRules.StatusUnknown;
        }

        private async Task<Result<string>> TryHero(string server, int heroVillageId, int? kid, int x, int y, HeroStats stats, int animals, HeroRunState run, CancellationToken cancellationToken)
        {
            if (kid is null) return HeroLevelingRules.StatusNotSimulated;

            var verdict = await simulateHeroRaidCommand.HandleAsync(new(server, heroVillageId, kid.Value, stats, animals), cancellationToken);
            if (verdict.IsFailed)
            {
                logger.Warning("Simulation failed at ({X}|{Y}): {Error}", x, y, string.Join(" ", verdict.Errors.Select(e => e.Message)));
                return HeroLevelingRules.StatusNotSimulated;
            }

            if (!verdict.Value.Send)
            {
                logger.Information("Hero not sent to ({X}|{Y}): {Reason}", x, y, verdict.Value.Reason);
                return HeroLevelingRules.StatusFor(verdict.Value);
            }

            // Send right after the simulation: the animals are the ones we just simulated.
            var sent = await sendHeroRaidCommand.HandleAsync(new(server, heroVillageId, kid.Value, x, y), cancellationToken);
            if (sent.IsFailed)
            {
                logger.Warning("Hero send failed at ({X}|{Y}): {Error}", x, y, string.Join(" ", sent.Errors.Select(e => e.Message)));
                return HeroLevelingRules.StatusNotSent;
            }

            if (sent.Value != HeroSendOutcome.Sent) return HeroLevelingRules.StatusNotSent;

            run.Sent = true;
            return Save(HeroLevelingRules.StatusSent, $"El héroe fue a cazar al oasis ({x}|{y}). {verdict.Value.Reason}.");
        }

        private string Save(string state, string message)
        {
            logger.Information("Hero: {Message}", message);
            if (browser.AccountId == AccountId.Empty) return state;
            using var context = contextFactory.CreateDbContext();
            HeroLevelingStore.SaveAction(context, browser.AccountId, state, message);
            return state;
        }
    }
}
