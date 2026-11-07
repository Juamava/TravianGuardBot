#nullable disable

using Riok.Mapperly.Abstractions;

namespace MainCore.DTO
{
    public class FarmDto
    {
        public FarmId Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
    public static partial class FarmMapper
    {
        public static Farm ToEntity(this FarmDto dto, AccountId accountId)
        {
            var entity = dto.ToEntity();
            entity.AccountId = accountId.Value;
            entity.IntervalMin = 9;
            entity.IntervalMax = 11;
            entity.IntervalsInMinutes = 1;
            return entity;
        }

        [MapperIgnoreTarget(nameof(Farm.AccountId))]
        [MapperIgnoreTarget(nameof(Farm.Id))]
        [MapperIgnoreTarget(nameof(Farm.IsActive))]
        [MapperIgnoreTarget(nameof(Farm.IntervalMin))]
        [MapperIgnoreTarget(nameof(Farm.IntervalMax))]
        [MapperIgnoreTarget(nameof(Farm.IntervalsInMinutes))]
        [MapperIgnoreTarget(nameof(Farm.MinAttackMinutes))]
        [MapperIgnoreTarget(nameof(Farm.LastAttackedAt))]
        public static partial void To(this FarmDto dto, Farm entity);

        [MapperIgnoreTarget(nameof(Farm.AccountId))]
        [MapperIgnoreTarget(nameof(Farm.IntervalMin))]
        [MapperIgnoreTarget(nameof(Farm.IntervalMax))]
        [MapperIgnoreTarget(nameof(Farm.IntervalsInMinutes))]
        [MapperIgnoreTarget(nameof(Farm.MinAttackMinutes))]
        [MapperIgnoreTarget(nameof(Farm.LastAttackedAt))]
        private static partial Farm ToEntity(this FarmDto dto);

        private static int ToInt(this FarmId farmId) => farmId.Value;
    }
}