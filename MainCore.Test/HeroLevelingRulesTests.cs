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
        [InlineData(false, 100, true, true, HeroLevelingAction.WaitForReturn)]
        [InlineData(true, 50, true, true, HeroLevelingAction.Adventure)]
        [InlineData(true, 96, false, true, HeroLevelingAction.HuntOasis)]
        [InlineData(true, 49, true, true, HeroLevelingAction.CollectTasks)]
        [InlineData(true, 49, true, false, HeroLevelingAction.WaitForHealing)]
        [InlineData(true, 10, false, false, HeroLevelingAction.WaitForHealing)]
        public void Decide_follows_hp_threshold(bool home, int hp, bool adventure, bool tasks, HeroLevelingAction expected)
        {
            HeroLevelingRules.Decide(home, hp, adventure, tasks).ShouldBe(expected);
        }

        [Fact]
        public void Decide_with_unknown_hp_does_nothing()
        {
            HeroLevelingRules.Decide(true, null, true, true).ShouldBe(HeroLevelingAction.UnknownHealth);
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
        }

        [Fact]
        public void Evaluate_refuses_when_hero_dies()
        {
            HeroLevelingRules.Evaluate(Numbers(heroLoss: -1, healthAfter: 0)).Send.ShouldBeFalse();
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

        [Theory]
        [InlineData("Oasis", true)]
        [InlineData("  oasis ", true)]
        [InlineData("OASIS", true)]
        [InlineData("Oasis 2", false)]
        [InlineData(null, false)]
        public void IsOasisList_is_case_insensitive_and_trimmed(string? name, bool expected)
        {
            HeroLevelingRules.IsOasisList(name).ShouldBe(expected);
        }

        [Fact]
        public void OrderCandidates_sorts_by_distance_and_skips_bad_urls()
        {
            var ordered = HeroLevelingRules.OrderCandidates(
            [
                (1, "https://ts.example/karte.php?x=-76&y=28"),
                (2, "https://ts.example/karte.php?x=-66&y=25"),
                (3, "not a url"),
                (4, "https://ts.example/karte.php?x=-69&y=21"),
            ], -69, 25);

            ordered.Select(x => x.Id).ShouldBe([2, 4, 1]);
            ordered[0].Distance.ShouldBe(3);
        }

        [Fact]
        public void RoundTrip_uses_speed()
        {
            HeroLevelingRules.RoundTrip(14, 28).ShouldBe(TimeSpan.FromHours(1));
            HeroLevelingRules.RoundTrip(14, null).ShouldBeNull();
            HeroLevelingRules.RoundTrip(14, 0).ShouldBeNull();
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
    }
}
