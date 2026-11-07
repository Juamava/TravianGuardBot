using FluentValidation;

using MainCore.UI.Models.Input;

namespace MainCore.UI.Models.Validators
{
    public class VillageSettingInputValidator : AbstractValidator<VillageSettingInput>
    {
        public VillageSettingInputValidator()
        {
            RuleFor(x => x.TrainTroopRepeatTime.Min)
                .LessThanOrEqualTo(x => x.TrainTroopRepeatTime.Max)
                .WithMessage("Minimum next train troop ({PropertyValue}) should be less than maximum next train troop ({ComparisonValue})");
            RuleFor(x => x.TrainTroopRepeatTime.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum next train troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.BarrackAmount.Min)
                .LessThanOrEqualTo(x => x.BarrackAmount.Max)
                .WithMessage("Minimum barrack amount troop ({PropertyValue}) should be less than maximum barrack amount troop ({ComparisonValue})");
            RuleFor(x => x.BarrackAmount.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum barrack amount troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.StableAmount.Min)
                .LessThanOrEqualTo(x => x.StableAmount.Max)
                .WithMessage("Minimum stable amount troop ({PropertyValue}) should be less than maximum stable amount troop ({ComparisonValue})");
            RuleFor(x => x.StableAmount.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum stable amount troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.GreatBarrackAmount.Min)
                .LessThanOrEqualTo(x => x.GreatBarrackAmount.Max)
                .WithMessage("Minimum greatbarrack amount troop ({PropertyValue}) should be less than maximum greatbarrack amount troop ({ComparisonValue})");
            RuleFor(x => x.GreatBarrackAmount.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum greatbarrack amount troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.GreatStableAmount.Min)
                .LessThanOrEqualTo(x => x.GreatStableAmount.Max)
                .WithMessage("Minimum greatstable amount troop ({PropertyValue}) should be less than maximum greatstable amount troop ({ComparisonValue})");
            RuleFor(x => x.GreatStableAmount.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum greatstable amount troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.WorkshopAmount.Min)
                .LessThanOrEqualTo(x => x.WorkshopAmount.Max)
                .WithMessage("Minimum workshop amount troop ({PropertyValue}) should be less than maximum workshop amount troop ({ComparisonValue})");
            RuleFor(x => x.WorkshopAmount.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum workshop amount troop ({PropertyValue}) should be positive number");

            RuleFor(x => x.EvadeTargetX)
                .InclusiveBetween(-400, 400)
                .WithMessage("La X del aliado ({PropertyValue}) tiene que estar entre -400 y 400");
            RuleFor(x => x.EvadeTargetY)
                .InclusiveBetween(-400, 400)
                .WithMessage("La Y del aliado ({PropertyValue}) tiene que estar entre -400 y 400");
            RuleFor(x => x.EvadeLeadSeconds)
                .InclusiveBetween(3, 60)
                .WithMessage("La salida antes del impacto ({PropertyValue}) tiene que estar entre 3 y 60 segundos");
            RuleFor(x => x.EvadeTrainGapSeconds)
                .InclusiveBetween(0, 1800)
                .WithMessage("La separación de un tren ({PropertyValue}) tiene que estar entre 0 y 1800 segundos");
            RuleFor(x => x.EvadeSafetySeconds)
                .InclusiveBetween(0, 120)
                .WithMessage("El margen de vuelta ({PropertyValue}) tiene que estar entre 0 y 120 segundos");
            RuleFor(x => x.EvadeCheckSeconds)
                .InclusiveBetween(30, 1800)
                .WithMessage("La revisión de ataques ({PropertyValue}) tiene que estar entre 30 y 1800 segundos");
        }
    }
}