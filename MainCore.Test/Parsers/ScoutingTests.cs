using MainCore.Enums;
using MainCore.Parsers;

namespace MainCore.Test.Parsers
{
    // Fixtures follow the live scouting report and profile pages of ts21.x2.international (November 2026).
    public class ScoutingParserTests(PlaywrightFixture fixture) : ParserTestBase(fixture)
    {
        [Fact]
        public async Task Report_with_resources_and_no_defense_is_read()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "ScoutReportResources.html");

            var report = await ScoutParser.ReadReport(page);
            report.ShouldNotBeNull();
            report.X.ShouldBe(-59);
            report.Y.ShouldBe(29);
            report.VillageName.ShouldBe("Aldea de Markkis19");
            report.PlayerName.ShouldBe("Markkis19");
            report.PlayerUrl.ShouldBe("/profile/1857");
            (report.Wood, report.Clay, report.Iron, report.Crop).ShouldBe((12, 10, 9, 11));
            report.Cranny.ShouldBe(0);
            report.Defenders.ShouldBe(0);
            report.ScoutsDied.ShouldBeFalse();
            report.Time.ShouldBe("07.11.26, 00:30:53");

            ScoutRules.Classify(report, 0).ShouldBe(ScoutRules.Thin);
        }

        [Fact]
        public async Task Report_where_the_scouts_died_is_danger()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "ScoutReportScoutsDied.html");

            var report = await ScoutParser.ReadReport(page);
            report.ShouldNotBeNull();
            report.ScoutsDied.ShouldBeTrue();
            report.Defenders.ShouldBe(-1);
            report.Wood.ShouldBe(-1);
            ScoutRules.Classify(report, 0).ShouldBe(ScoutRules.Danger);
        }

        [Fact]
        public async Task Report_list_takes_the_subject_link_not_the_read_icon()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "ScoutReportList.html");

            var rows = await ScoutParser.ReadReportRows(page);
            rows.Count.ShouldBe(2);
            rows[0].Subject.ShouldBe("Alexandria scouts Village de alex35");
            rows[0].Unread.ShouldBeTrue();
            rows[0].Href.ShouldBe("?id=950001%7Cabc123&s=1");
            rows[1].Unread.ShouldBeFalse();

            ScoutParser.ReportUrl("https://ts21.x2.international.travian.com/report/scouting?page=2", rows[0].Href)
                .ShouldBe("https://ts21.x2.international.travian.com/report/scouting?id=950001%7Cabc123&s=1");
            ScoutParser.ListPageUrl("https://ts21.x2.international.travian.com", 1).ShouldBe("https://ts21.x2.international.travian.com/report/scouting");
            ScoutParser.ListPageUrl("https://ts21.x2.international.travian.com", 3).ShouldBe("https://ts21.x2.international.travian.com/report/scouting?page=3");
        }

        [Fact]
        public async Task Home_troops_are_read_from_the_rally_point()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointHero.html");

            var troops = await RallyPointTroops.Read(page);
            troops.Select(x => x.Letter).ShouldBe(['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H']);
            troops[0].Name.ShouldBe("Slave Militia");
            troops[0].Available.ShouldBe(7);
            troops[3].Name.ShouldBe("Sopdu Explorer");
            troops[3].Available.ShouldBe(0);
        }

        [Fact]
        public async Task Attack_report_is_read()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "AttackReportRaid.html");

            var report = await AttackReportParser.Read(page);
            report.ShouldNotBeNull();
            (report.X, report.Y).ShouldBe((-59, 29));
            report.AttackerWon.ShouldBeTrue();
            report.TimeUtc.ShouldBe(new DateTime(2026, 11, 7, 1, 48, 5, DateTimeKind.Utc));
            report.Attacker.Count.ShouldBe(1);
            report.Attacker[0].Name.ShouldBe("Slave Militia");
            (report.Sent, report.Lost, report.Attacker[0].Survived).ShouldBe((50, 3, 47));
            (report.BountyWood, report.BountyClay, report.BountyIron, report.BountyCrop).ShouldBe((320, 280, 150, 0));
            report.Bounty.ShouldBe(750);
            (report.Carried, report.Capacity).ShouldBe((750, 1500));
            report.Defender.Single().ShouldBe(new AttackUnit("Phalanx", 4, 4));
        }

        [Fact]
        public async Task Player_profile_is_read()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "PlayerProfile.html");

            var profile = await ScoutParser.ReadProfile(page);
            profile.ShouldNotBeNull();
            profile.Tribe.ShouldBe("Gauls");
            profile.Alliance.ShouldBe("");
            profile.Villages.ShouldBe(1);
            profile.DaysPlaying.ShouldBe(3);
            profile.Population.ShouldBe(15);
            profile.AttackPoints.ShouldBe(0);
            profile.DefensePoints.ShouldBe(1250);
            profile.HeroLevel.ShouldBe(1);
        }
    }

    public class ScoutRulesTests
    {
        private static ScoutReport Report(int wood, int clay, int iron, int crop, int defenders = 0, bool died = false)
            => new(0, 0, "v", "p", "/profile/1", wood, clay, iron, crop, 0, defenders, died, "");

        [Fact]
        public void Full_warehouse_without_defense_is_a_fat_cow()
        {
            ScoutRules.Classify(Report(1200, 1200, 1200, 900), 0).ShouldBe(ScoutRules.Fat);
        }

        [Fact]
        public void Uneven_resources_without_defense_is_a_thin_cow()
        {
            ScoutRules.Classify(Report(400, 1200, 90, 900), 0).ShouldBe(ScoutRules.Thin);
        }

        [Fact]
        public void Threshold_also_makes_a_fat_cow()
        {
            ScoutRules.Classify(Report(1000, 900, 800, 700), 3000).ShouldBe(ScoutRules.Fat);
            ScoutRules.Classify(Report(1000, 900, 800, 100), 3000).ShouldBe(ScoutRules.Thin);
        }

        [Fact]
        public void Small_equal_amounts_are_not_full()
        {
            ScoutRules.Classify(Report(10, 10, 10, 10), 0).ShouldBe(ScoutRules.Thin);
        }

        [Fact]
        public void Any_defender_is_danger()
        {
            ScoutRules.Classify(Report(1200, 1200, 1200, 900, defenders: 3), 0).ShouldBe(ScoutRules.Danger);
        }

        [Fact]
        public void Danger_grows_with_defense_and_size()
        {
            var empty = ScoutRules.DangerLevel(new DangerInput(0, false, 15, 0, 1, 0, 0, false, 1));
            var mid = ScoutRules.DangerLevel(new DangerInput(40, false, 300, 0.5, 2, 2000, 500, false, 5));
            var big = ScoutRules.DangerLevel(new DangerInput(-1, true, 900, 1, 5, 200000, 50000, true, 30));

            empty.ShouldBe(1);
            mid.ShouldBeGreaterThan(empty);
            big.ShouldBeGreaterThan(mid);
            big.ShouldBe(10);
        }

        [Fact]
        public void Danger_explains_each_reason()
        {
            var (level, parts) = ScoutRules.DangerBreakdown(new DangerInput(-1, true, 900, 1, 5, 200000, 50000, true, 30));
            level.ShouldBe(10);
            var text = ScoutRules.Explain(level, parts);
            text.ShouldStartWith("Peligro 10 de 10");
            text.ShouldContain("Mató a los espías");
            text.ShouldContain("Está en una alianza");
            text.ShouldContain("5 aldeas");
        }

        [Theory]
        [InlineData("Villaggio di Corsara (Capital) ‭(‭−‭57‬‬|‭18‬‬)‬", "Villaggio di Corsara (Capital)")]
        [InlineData("Aldea de Markkis19 (-59|29)", "Aldea de Markkis19")]
        [InlineData("GoldSnake`s village", "GoldSnake`s village")]
        public void Village_name_loses_the_coordinates(string raw, string clean)
        {
            ScoutRules.CleanVillageName(raw).ShouldBe(clean);
        }

        [Theory]
        [InlineData("2 25 16", 1794018316L, 0)]
        [InlineData("23 25 16", 1794018316L, -180)]
        [InlineData("5 25 16", 1794018316L, 180)]
        public void Server_offset_comes_from_the_top_bar(string shown, long unix, int minutes)
        {
            AttackReportParser.ServerOffset(shown, unix).ShouldBe(TimeSpan.FromMinutes(minutes));
        }

        [Fact]
        public void Percentile_compares_with_the_list()
        {
            ScoutRules.Percentile(100, [10, 50, 100, 200, 400]).ShouldBe(0.5);
            ScoutRules.Percentile(400, [10, 50, 100, 200, 400]).ShouldBe(1);
            ScoutRules.Percentile(100, [100]).ShouldBe(-1);
        }

        [Theory]
        [InlineData(68713, -59, 29)]
        [InlineData(1, -200, 200)]
        [InlineData(160801, 200, -200)]
        public void Map_id_becomes_coordinates(int mapId, int x, int y)
        {
            ScoutRules.FromMapId(mapId).ShouldBe((x, y));
        }

        [Theory]
        [InlineData(TribeEnums.Egyptians, "D:1")]
        [InlineData(TribeEnums.Gauls, "C:1")]
        [InlineData(TribeEnums.Spartan, "B:1")]
        [InlineData(TribeEnums.Teutons, "D:1")]
        public void Default_scout_plan_uses_the_tribe_scout(TribeEnums tribe, string plan)
        {
            ScoutRules.DefaultScoutPlan(tribe).ShouldBe(plan);
        }
    }
}
