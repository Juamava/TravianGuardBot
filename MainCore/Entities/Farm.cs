using StronglyTypedIds;

#nullable disable

namespace MainCore.Entities
{
    public class Farm
    {
        public int Id { get; set; }

        public int AccountId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int IntervalMin { get; set; } = 540;
        public int IntervalMax { get; set; } = 660;
    }

    [StronglyTypedId]
    public partial struct FarmId
    { }
}