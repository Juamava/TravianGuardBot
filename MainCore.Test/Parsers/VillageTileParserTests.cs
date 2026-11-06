using MainCore.Parsers;

namespace MainCore.Test.Parsers
{
    public class VillageTileParserTests(PlaywrightFixture fixture) : ParserTestBase(fixture)
    {
        [Fact]
        public async Task Send_troops_is_the_dialog_link_not_the_map_flag()
        {
            await using var context = await CreateContextAsync();
            var page = await CreatePageAsync(context, "VillageTile.html");

            var link = CowOasisParser.GetSendTroopsLink(page);
            (await link.CountAsync()).ShouldBe(1);
            (await link.GetAttributeAsync("href") ?? "").ShouldContain("build.php");

            var details = await CowOasisParser.ReadDetails(page);
            details.Kind.ShouldBe("aldea");
            details.Owner.ShouldBe("petar123");
            details.Population.ShouldBe(10);
            details.Distance.ShouldBe(1.41);
            details.HasSendTroops.ShouldBeTrue();
        }
    }
}
