using MainCore.Parsers;

namespace MainCore.Test.Parsers
{
    // Fixtures in HtmlFiles are trimmed copies (scripts and svg removed) of the live pages saved in "Examples HTML".
    public class HeroLevelingParserTests(PlaywrightFixture fixture) : ParserTestBase(fixture)
    {
        [Fact]
        public async Task Hero_attributes_are_read()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "HeroAttributes.html");

            var stats = await HeroParser.ReadStats(page);
            stats.Level.ShouldBe(5);
            stats.HpPercent.ShouldBe(96);
            stats.Experience.ShouldBe(829);
            stats.SpeedFieldsPerHour.ShouldBe(28);
            stats.StrengthPoints.ShouldBe(8);
            stats.OffBonusPoints.ShouldBe(4);

            (await HeroParser.IsHeroHome(page)).ShouldBeTrue();
            (await HeroParser.GetHeroVillageId(page)).ShouldBe(28876);
        }

        [Fact]
        public async Task Simulator_hero_dies_is_refused()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "SimulatorHeroDies.html");

            var numbers = await CombatSimulatorParser.ReadResult(page);
            numbers.ShouldNotBeNull();
            numbers.AttackerInitial[10].ShouldBe(1);
            numbers.AttackerLoss[10].ShouldBe(-1);
            numbers.HeroHealthBefore.ShouldBe(96);
            numbers.HeroHealthAfter.ShouldBe(0);
            numbers.DefenderInitial.Sum(x => x!.Value).ShouldBe(42);

            HeroLevelingRules.Evaluate(numbers).Send.ShouldBeFalse();
        }

        [Fact]
        public async Task Simulator_hero_survives_is_sent()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "SimulatorHeroSurvives.html");

            var numbers = await CombatSimulatorParser.ReadResult(page);
            numbers.ShouldNotBeNull();
            numbers.AttackerLoss[10].ShouldBe(0);
            numbers.HeroHealthAfter.ShouldBe(95);
            numbers.DefenderLoss[0].ShouldBe(-2);

            var verdict = HeroLevelingRules.Evaluate(numbers);
            verdict.Send.ShouldBeTrue(verdict.Reason);
        }

        [Fact]
        public async Task Simulator_form_has_hero_inputs()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "SimulatorHeroSurvives.html");

            (await CombatSimulatorParser.GetAttackerUnitInputs(page).CountAsync()).ShouldBe(10);
            (await CombatSimulatorParser.GetAttackerHeroCheckbox(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetHeroHealthInput(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetHeroStrengthInput(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetHeroOffPointsInput(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetVillageSelect(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetRaidRadio(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.GetSimulateButton(page).CountAsync()).ShouldBe(1);
            (await CombatSimulatorParser.ReadDefenderTotal(page)).ShouldNotBeNull();
        }

        [Fact]
        public async Task Oasis_animals_are_counted()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "OasisWithAnimals.html");
            (await CowOasisParser.ReadAnimalCount(page)).ShouldBe(42);
            CombatSimulatorParser.ParseKid(await CowOasisParser.GetSimulatorLink(page).GetAttributeAsync("href")).ShouldBe(70310);

            var empty = await CreatePageAsync(context, "OasisEmpty.html");
            (await CowOasisParser.ReadAnimalCount(empty)).ShouldBe(0);
        }

        [Fact]
        public async Task Rally_point_form_with_hero_is_accepted_only_for_the_right_target()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointHero.html");

            (await RallyPointHeroParser.CheckSendForm(page, -66, 25)).Ok.ShouldBeTrue();
            (await RallyPointHeroParser.CheckSendForm(page, -71, 26)).Ok.ShouldBeFalse();

            // Not filled yet: the hero is empty, so the filled-form check refuses.
            (await RallyPointHeroParser.CheckFilledForm(page, -66, 25)).Ok.ShouldBeFalse();

            await RallyPointHeroParser.GetHeroInput(page).FillAsync("1");
            (await RallyPointHeroParser.CheckFilledForm(page, -66, 25)).Ok.ShouldBeTrue();

            await page.Locator("table#troops input[name='troop[t1]']").FillAsync("7");
            (await RallyPointHeroParser.CheckFilledForm(page, -66, 25)).Ok.ShouldBeFalse();
        }

        [Fact]
        public async Task Confirmation_with_hero_alone_is_accepted_only_for_the_right_target()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointConfirmHero.html");

            var check = await RallyPointHeroParser.CheckConfirmation(page, -71, 26);
            check.Ok.ShouldBeTrue(check.Reason);
            (await RallyPointHeroParser.CheckConfirmation(page, -66, 25)).Ok.ShouldBeFalse();
            (await RallyPointHeroParser.GetConfirmButton(page).CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task Confirmation_with_other_troops_is_refused()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointConfirmHero.html");

            await page.EvaluateAsync("() => document.querySelector(\"#troopSendForm input[name='troops[0][t1]']\").value = '7'");
            (await RallyPointHeroParser.CheckConfirmation(page, -71, 26)).Ok.ShouldBeFalse();
        }

        [Fact]
        public async Task Confirmation_without_hero_is_refused()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointConfirmHero.html");

            await page.EvaluateAsync("() => document.querySelector(\"#troopSendForm input[name='troops[0][t11]']\").value = '0'");
            (await RallyPointHeroParser.CheckConfirmation(page, -71, 26)).Ok.ShouldBeFalse();
        }
    }
}
