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
        public int IntervalMin { get; set; } = 9;
        public int IntervalMax { get; set; } = 11;
        public int IntervalsInMinutes { get; set; } = 1;
    }

    [StronglyTypedId]
    public partial struct FarmId
    { }
}