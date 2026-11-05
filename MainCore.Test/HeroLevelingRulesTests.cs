using MainCore.Parsers;

namespace MainCore.Test
{
    public class HeroLevelingRulesTests
    {
        private static SimulationNumbers Numbers(
            int heroInitial = 1,
            int heroLoss = 0,
            int otherAttacker = 0,
            int[]? defenderInitial = null,
            int[]? defenderLoss = null,
            int? healthBefore = 96,
            int? healthAfter = 95)
        {
            var attackerInitial = Enumerable.Repeat<int?>(0, 10).ToList();
            attackerInitial[0] = otherAttacker;
            attackerInitial.Add(heroInitial);
            var attackerLoss = Enumerable.Repeat<int?>(0, 10).ToList();
            attackerLoss.Add(heroLoss);
            return new SimulationNumbers(
                attackerInitial,
                attackerLoss,
                (defenderInitial ?? [2, 0, 0, 0, 0, 0, 0, 0, 0, 0]).Select(x => (int?)x).ToList(),
                (defenderLoss ?? [-2, 0, 0, 0, 0, 0, 0, 0, 0, 0]).Select(x => (int?)x).ToList(),
                healthBefore,
                healthAfter);
        }

        [Theory]
        [InlineData(49, false)]
        [InlineData(50, true)]
        [InlineData(null, false)]
        public void Adventure_and_hunt_need_50_percent(int? hp, bool expected)
        {
            HeroLevelingRules.CanGoOnAdventure(hp).ShouldBe(expected);
            HeroLevelingRules.CanHunt(hp).ShouldBe(expected);
        }

        [Fact]
        public void Evaluate_sends_when_hero_survives_and_kills()
        {
            var verdict = HeroLevelingRules.Evaluate(Numbers());
            verdict.Send.ShouldBeTrue(verdict.Reason);
            verdict.Outcome.ShouldBe(SimulationOutcome.Safe);
        }

        [Fact]
        public void Evaluate_refuses_when_hero_dies()
        {
            var verdict = HeroLevelingRules.Evaluate(Numbers(heroLoss: -1, healthAfter: 0));
            verdict.Send.ShouldBeFalse();
            verdict.Outcome.ShouldBe(SimulationOutcome.TooStrong);
            HeroLevelingRules.StatusFor(verdict).ShouldBe("Muy fuerte");
        }

        [Fact]
        public void Evaluate_refuses_when_hero_loss_is_minus_one_even_if_health_looks_fine()
        {
            HeroLevelingRules.Evaluate(Numbers(heroLoss: -1, healthAfter: 80)).Send.ShouldBeFalse();
        }

        [Fact]
        public void Evaluate_refuses_when_nothing_dies()
        {
            HeroLevelingRules.Evaluate(Numbers(defenderLoss: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0])).Send.ShouldBeFalse();
        }

        [Fact]
        public void Evaluate_refuses_without_animals()
        {
            HeroLevelingRules.Evaluate(Numbers(defenderInitial: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0])).Send.ShouldBeFalse();
        }

        [Fact]
        public void Evaluate_refuses_when_other_troops_are_in_the_simulation()
        {
            HeroLevelingRules.Evaluate(Numbers(otherAttacker: 7)).Send.ShouldBeFalse();
        }

        [Fact]
        public void Evaluate_refuses_when_hero_is_not_in_the_simulation()
        {
            HeroLevelingRules.Evaluate(Numbers(heroInitial: 0)).Send.ShouldBeFalse();
        }

        [Theory]
        [InlineData(24, false)]
        [InlineData(25, true)]
        public void Evaluate_requires_health_margin(int after, bool expected)
        {
            HeroLevelingRules.Evaluate(Numbers(healthAfter: after)).Send.ShouldBe(expected);
        }

        [Fact]
        public void Evaluate_refuses_unknown_health_after_fight()
        {
            HeroLevelingRules.Evaluate(Numbers(healthAfter: null)).Send.ShouldBeFalse();
        }

        [Fact]
        public void Evaluate_refuses_null_or_unreadable_result()
        {
            HeroLevelingRules.Evaluate(null).Send.ShouldBeFalse();
            HeroLevelingRules.Evaluate(null).Outcome.ShouldBe(SimulationOutcome.Unreadable);
            HeroLevelingRules.StatusFor(HeroLevelingRules.Evaluate(null)).ShouldBe("Sin simular");

            var broken = Numbers() with { AttackerLoss = [.. Enumerable.Repeat<int?>(0, 10), null] };
            HeroLevelingRules.Evaluate(broken).Send.ShouldBeFalse();

            var tooShort = Numbers() with { AttackerInitial = [0, 0, 1] };
            HeroLevelingRules.Evaluate(tooShort).Send.ShouldBeFalse();
        }

        [Theory]
        [InlineData("‭‭−‭1‬‬‬", -1)]
        [InlineData("‭0‬", 0)]
        [InlineData("‭3,770‬", 3770)]
        [InlineData("‭‭96‬%‬", 96)]
        [InlineData(" 16 ", 16)]
        public void ParseNumber_reads_travian_numbers(string text, int expected)
        {
            HeroLevelingRules.ParseNumber(text).ShouldBe(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1-2")]
        [InlineData(null)]
        public void ParseNumber_rejects_garbage(string? text)
        {
            HeroLevelingRules.ParseNumber(text).ShouldBeNull();
        }

        [Fact]
        public void ParseHealthLine_reads_before_and_after()
        {
            HeroLevelingRules.ParseHealthLine("Hero's health lowered from 96 to 0").ShouldBe(((int?)96, (int?)0));
            HeroLevelingRules.ParseHealthLine("Hero's health lowered from 96 to 95").ShouldBe(((int?)96, (int?)95));
            HeroLevelingRules.ParseHealthLine("Something else").ShouldBe(((int?)null, (int?)null));
            HeroLevelingRules.ParseHealthLine("from 50 to 60").ShouldBe(((int?)null, (int?)null));
        }

        [Fact]
        public void ToNumbers_requires_exactly_one_health_line()
        {
            string[] attacker = ["0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "1"];
            string[] loss = ["0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"];
            string[] defender = ["2", "0", "0", "0", "0", "0", "0", "0", "0", "0"];
            string[] defenderLoss = ["−2", "0", "0", "0", "0", "0", "0", "0", "0", "0"];

            CombatSimulatorParser.ToNumbers(attacker, loss, defender, defenderLoss, ["Hero's health lowered from 96 to 95", "x 1 to 2"])!.HeroHealthAfter.ShouldBeNull();
            CombatSimulatorParser.ToNumbers(attacker, loss, defender, defenderLoss, [])!.HeroHealthAfter.ShouldBeNull();
            CombatSimulatorParser.ToNumbers(attacker, loss, defender, defenderLoss, ["Hero's health lowered from 96 to 95"])!.HeroHealthAfter.ShouldBe(95);
            CombatSimulatorParser.ToNumbers(null, loss, defender, defenderLoss, []).ShouldBeNull();
        }

        [Fact]
        public void Kid_and_newdid_are_read_from_links()
        {
            CombatSimulatorParser.ParseKid("/build.php?id=39&tt=3&screen=combatSimulator&kid=70310").ShouldBe(70310);
            CombatSimulatorParser.ParseKid("/build.php?id=39").ShouldBeNull();
            HeroParser.ParseNewdid("/build.php?newdid=28876&id=39&&tt=2").ShouldBe(28876);
            HeroParser.ParseNewdid(null).ShouldBeNull();
        }

        private static HeroRunState Run(bool sent = false, bool statsRead = true, int? hp = 80, bool collected = false, bool seenAway = false)
        {
            return new HeroRunState { Sent = sent, StatsRead = statsRead, HpPercent = hp, TasksCollected = collected, SeenAway = seenAway };
        }

        [Fact]
        public void Animal_oasis_with_checkbox_off_is_the_normal_skip()
        {
            var action = HeroLevelingRules.DecideAnimalOasis(false, Run(), heroHome: true, heroVillageId: 10, listVillageId: 10);
            action.ShouldBe(AnimalOasisAction.SkipTroops);
            HeroLevelingRules.StatusFor(action).ShouldBe("Hay tropas");
        }

        [Fact]
        public void Animal_oasis_with_hero_home_and_enough_hp_tries_the_hero()
        {
            HeroLevelingRules.DecideAnimalOasis(true, Run(hp: 50), true, 10, 10).ShouldBe(AnimalOasisAction.TryHero);
        }

        [Fact]
        public void Animal_oasis_with_hero_away_is_skipped()
        {
            var action = HeroLevelingRules.DecideAnimalOasis(true, Run(), heroHome: false, heroVillageId: 10, listVillageId: 10);
            action.ShouldBe(AnimalOasisAction.HeroAway);
            HeroLevelingRules.StatusFor(action).ShouldBe("Héroe fuera");
        }

        [Fact]
        public void Animal_oasis_after_hero_was_sent_is_the_normal_skip()
        {
            var action = HeroLevelingRules.DecideAnimalOasis(true, Run(sent: true), heroHome: true, heroVillageId: 10, listVillageId: 10);
            action.ShouldBe(AnimalOasisAction.HeroAlreadySent);
            HeroLevelingRules.StatusFor(action).ShouldBe("Hay tropas");
        }

        [Fact]
        public void Animal_oasis_reads_hp_first()
        {
            HeroLevelingRules.DecideAnimalOasis(true, Run(statsRead: false, hp: null), true, 10, 10).ShouldBe(AnimalOasisAction.NeedHeroStats);
        }

        [Fact]
        public void Animal_oasis_with_low_hp_collects_tasks_once_then_skips()
        {
            HeroLevelingRules.DecideAnimalOasis(true, Run(hp: 49, collected: false), true, 10, 10).ShouldBe(AnimalOasisAction.CollectTasks);

            var action = HeroLevelingRules.DecideAnimalOasis(true, Run(hp: 49, collected: true), true, 10, 10);
            action.ShouldBe(AnimalOasisAction.HeroLowHp);
            HeroLevelingRules.StatusFor(action).ShouldBe("Héroe con poca vida");
        }

        [Fact]
        public void Animal_oasis_after_tasks_restored_hp_tries_the_hero()
        {
            HeroLevelingRules.DecideAnimalOasis(true, Run(hp: 100, collected: true), true, 10, 10).ShouldBe(AnimalOasisAction.TryHero);
        }

        [Fact]
        public void Animal_oasis_with_unreadable_hp_never_sends()
        {
            var action = HeroLevelingRules.DecideAnimalOasis(true, Run(statsRead: true, hp: null), true, 10, 10);
            action.ShouldBe(AnimalOasisAction.HeroUnknownHp);
            HeroLevelingRules.StatusFor(action).ShouldBe("Héroe sin datos");
        }

        [Theory]
        [InlineData(11, 10)]
        [InlineData(null, 10)]
        [InlineData(null, 0)]
        public void Animal_oasis_with_hero_in_another_village_is_skipped(int? heroVillageId, int listVillageId)
        {
            var action = HeroLevelingRules.DecideAnimalOasis(true, Run(), true, heroVillageId, listVillageId);
            action.ShouldBe(AnimalOasisAction.HeroOtherVillage);
            HeroLevelingRules.StatusFor(action).ShouldBe("Héroe en otra aldea");
        }

        [Fact]
        public void Animal_oasis_without_list_village_only_needs_the_hero_home()
        {
            HeroLevelingRules.DecideAnimalOasis(true, Run(), true, 10, 0).ShouldBe(AnimalOasisAction.TryHero);
        }

        [Fact]
        public void HeroMightGo_only_while_the_hero_can_still_leave()
        {
            HeroLevelingRules.HeroMightGo(false, Run()).ShouldBeFalse();
            HeroLevelingRules.HeroMightGo(true, Run(statsRead: false, hp: null)).ShouldBeTrue();
            HeroLevelingRules.HeroMightGo(true, Run()).ShouldBeTrue();
            HeroLevelingRules.HeroMightGo(true, Run(hp: 30, collected: false)).ShouldBeTrue();
            HeroLevelingRules.HeroMightGo(true, Run(hp: 30, collected: true)).ShouldBeFalse();
            HeroLevelingRules.HeroMightGo(true, Run(sent: true)).ShouldBeFalse();
            HeroLevelingRules.HeroMightGo(true, Run(seenAway: true)).ShouldBeFalse();
            HeroLevelingRules.HeroMightGo(true, Run(statsRead: true, hp: null)).ShouldBeFalse();
        }

        [Fact]
        public void HeroRunState_reset_clears_the_run()
        {
            var run = Run(sent: true, hp: 30, collected: true, seenAway: true);
            run.Reset();
            run.Sent.ShouldBeFalse();
            run.StatsRead.ShouldBeFalse();
            run.HpPercent.ShouldBeNull();
            run.TasksCollected.ShouldBeFalse();
            run.SeenAway.ShouldBeFalse();
        }

        [Fact]
        public void Hero_statuses_are_known_for_the_farming_status_column()
        {
            HeroLevelingRules.HeroStatuses.ShouldContain("Héroe enviado");
            HeroLevelingRules.HeroStatuses.ShouldContain("Muy fuerte");
            HeroLevelingRules.HeroStatuses.ShouldNotContain("Hay tropas");
        }
    }
}
