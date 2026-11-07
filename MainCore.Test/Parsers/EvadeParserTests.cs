using MainCore.Parsers;

namespace MainCore.Test.Parsers
{
    // Fixture: "Examples HTML/Rally point overview hero home.html", copied as HtmlFiles/RallyPointOverview.html by the csproj (three troops coming back, none attacking).
    public class EvadeParserTests(PlaywrightFixture fixture) : ParserTestBase(fixture)
    {
        private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0);

        [Fact]
        public async Task Returning_troops_are_not_attacks()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointOverview.html");

            var attacks = await EvadeParser.ReadIncoming(page, Now);

            attacks.ShouldBeEmpty();
        }

        [Fact]
        public async Task An_incoming_attack_is_read_with_its_arrival()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "RallyPointOverview.html");
            // The same table Travian draws for an attack, only with the attack class.
            await page.EvaluateAsync("() => { const t = document.querySelectorAll('table.troop_details.inReturn')[0]; t.classList.remove('inReturn'); t.classList.add('inAttack'); }");

            var attacks = await EvadeParser.ReadIncoming(page, Now);

            attacks.Count.ShouldBe(1);
            attacks[0].Arrival.ShouldBe(Now.AddSeconds(1504));
            attacks[0].Raid.ShouldBeFalse();
        }
    }
}
