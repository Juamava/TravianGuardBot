using MainCore.Entities;
using MainCore.Parsers;

namespace MainCore.Test.Parsers
{
    public class CowTileDetailsTests
    {
        [Fact]
        public void Village_fields_keep_owner_population_and_distance()
        {
            var details = CowOasisParser.FromFields(
                new Dictionary<string, string>
                {
                    ["Tribe"] = "Egyptians",
                    ["Alliance"] = "",
                    ["Owner"] = "petar123",
                    ["Population"] = "10",
                    ["Distance"] = "1.41 fields",
                },
                "Село од petar123 (Capital)",
                ["Send troops"]);

            details.Kind.ShouldBe("aldea");
            details.Owner.ShouldBe("petar123");
            details.Tribe.ShouldBe("Egyptians");
            details.Population.ShouldBe(10);
            details.Distance.ShouldBe(1.41);
            details.HasSendTroops.ShouldBeTrue();
        }

        [Fact]
        public void Oasis_link_is_not_a_village()
        {
            var details = CowOasisParser.FromFields(
                new Dictionary<string, string> { ["Distance"] = "3.16 fields" },
                "Unoccupied oasis",
                ["Raid unoccupied oasis"],
                "unoccupied oasis");

            details.Kind.ShouldBe("oasis");
            details.Population.ShouldBe(-1);
            details.HasSendTroops.ShouldBeFalse();
        }

        [Fact]
        public void Coordinates_come_from_the_map_link_and_distance_is_euclidean()
        {
            CowOasisParser.TryReadCoordinates("https://ts21.x2.international.travian.com/karte.php?x=-60&y=22", out var x, out var y).ShouldBeTrue();
            x.ShouldBe(-60);
            y.ShouldBe(22);
            CowOasisParser.TravelDistance(-61, 22, -60, 22).ShouldBe(1);
        }

        [Fact]
        public void Empty_read_does_not_wipe_a_saved_owner()
        {
            var row = new CowVillage
            {
                Name = "Село од petar123 (Capital)",
                Kind = "aldea",
                Owner = "petar123",
                Population = 10,
                Distance = 1.41,
                CoordX = -60,
                CoordY = 22,
            };

            var changed = CowOasisParser.CopyOnto(
                row,
                new CowOasisParser.TileDetails("", "", "", "", "", -1, -1, false),
                "https://ts21.x2.international.travian.com/karte.php?x=-60&y=22",
                -61,
                22,
                replaceName: false);

            changed.ShouldBeFalse();
            row.Owner.ShouldBe("petar123");
            row.Population.ShouldBe(10);
            row.Distance.ShouldBe(1.41);
        }
    }
}
