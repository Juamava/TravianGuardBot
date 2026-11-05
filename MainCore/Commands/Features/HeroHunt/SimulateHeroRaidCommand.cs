namespace MainCore.Commands.Features.HeroHunt
{
    /// <summary>
    /// Runs the rally point combat simulator for the hero ALONE against an oasis
    /// ("Examples HTML/Combat simulator form.html" and the two result samples).
    /// Clears the village troops the simulator prefills, keeps the hero with his real HP and
    /// points, checks that the defenders are the animals seen on the tile, simulates and
    /// evaluates the result with <see cref="HeroLevelingRules.Evaluate"/>. Any doubt is a "no".
    /// Pressing "Simulate" sends nothing.
    /// </summary>
    [Handler]
    public sealed partial class SimulateHeroRaidCommand(IChromeBrowser browser)
    {
        public sealed record Command(string Server, int HeroVillageId, int Kid, HeroStats Stats, int Animals) : ICommand;

        private async ValueTask<Result<SimulationVerdict>> HandleAsync(Command command)
        {
            var (server, heroVillageId, kid, stats, animals) = command;
            var page = browser.CurrentPage;

            if (stats.HpPercent is not int hp) return SimulationVerdict.Unreadable("Vida del héroe desconocida");

            var result = await browser.Navigate(CombatSimulatorParser.BuildUrl(server, heroVillageId, kid));
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(CombatSimulatorParser.GetSimulateButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var select = CombatSimulatorParser.GetVillageSelect(page);
            if (await select.CountAsync() != 1 || await select.InputValueAsync() != heroVillageId.ToString())
                return SimulationVerdict.Unreadable("El simulador no está en la aldea del héroe");

            var raid = CombatSimulatorParser.GetRaidRadio(page);
            if (await raid.CountAsync() != 1) return SimulationVerdict.Unreadable("No encontré la opción de atraco");
            if (!await raid.IsCheckedAsync())
            {
                result = await browser.Click(raid);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            // Attacker = only the hero: the simulator prefills the village troops, clear them.
            var units = CombatSimulatorParser.GetAttackerUnitInputs(page);
            var unitCount = await units.CountAsync();
            if (unitCount != 10) return SimulationVerdict.Unreadable("El simulador no tiene las 10 tropas del atacante");
            for (var i = 0; i < unitCount; i++)
            {
                result = await browser.Input(units.Nth(i), "0");
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            var hero = CombatSimulatorParser.GetAttackerHeroCheckbox(page);
            if (await hero.CountAsync() != 1) return SimulationVerdict.Unreadable("No encontré al héroe en el simulador");
            if (!await hero.IsCheckedAsync())
            {
                result = await browser.Click(hero);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }
            if (!await hero.IsCheckedAsync()) return SimulationVerdict.Unreadable("No pude marcar al héroe en el simulador");

            // Real current stats from /hero/attributes.
            result = await browser.Input(CombatSimulatorParser.GetHeroHealthInput(page), hp.ToString());
            if (result.IsFailed) return Result.Fail(result.Errors);

            var strengthOk = await EnsureHeroPoints(CombatSimulatorParser.GetHeroStrengthInput(page), stats.StrengthPoints);
            if (strengthOk.IsFailed) return Result.Fail(strengthOk.Errors);
            if (!strengthOk.Value) return SimulationVerdict.Unreadable("No pude cargar la fuerza del héroe en el simulador");

            var offOk = await EnsureHeroPoints(CombatSimulatorParser.GetHeroOffPointsInput(page), stats.OffBonusPoints);
            if (offOk.IsFailed) return Result.Fail(offOk.Errors);
            if (!offOk.Value) return SimulationVerdict.Unreadable("No pude cargar el bonus de ataque del héroe en el simulador");

            // The defender must be the animals we just saw on the tile.
            var defenders = await CombatSimulatorParser.ReadDefenderTotal(page);
            if (defenders != animals) return SimulationVerdict.Unreadable($"Los animales del simulador ({defenders?.ToString() ?? "?"}) no coinciden con el oasis ({animals})");

            result = await browser.Click(CombatSimulatorParser.GetSimulateButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(CombatSimulatorParser.GetAttackResult(page));
            if (result.IsFailed) return Result.Fail(result.Errors);
            await Task.Delay(500);

            var numbers = await CombatSimulatorParser.ReadResult(page);
            if (numbers is not null && numbers.HeroHealthBefore is int before && before != hp)
                return SimulationVerdict.Unreadable($"El simulador usó {before}% de vida y el héroe tiene {hp}%");

            return HeroLevelingRules.Evaluate(numbers);
        }

        private async Task<Result<bool>> EnsureHeroPoints(ILocator input, int? points)
        {
            if (await input.CountAsync() != 1) return false;

            if (points is int value)
            {
                var result = await browser.Input(input, value.ToString());
                if (result.IsFailed) return Result.Fail(result.Errors);
                return true;
            }

            // Unknown on the attributes page: accept only a readable prefilled value.
            var current = HeroLevelingRules.ParseNumber(await input.InputValueAsync());
            return current is >= 0;
        }
    }
}
