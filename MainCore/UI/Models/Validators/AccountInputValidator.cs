using FluentValidation;

using MainCore.UI.Models.Input;

using MainCore.UI.Models;

namespace MainCore.UI.Models.Validators
{
    public class AccountInputValidator : AbstractValidator<AccountInput>
    {
        public AccountInputValidator()
        {
            RuleFor(x => x.Server)
                .NotEmpty()
                .WithMessage("Falta el servidor.");

            RuleFor(x => x.Server)
                .Must(server => !string.IsNullOrEmpty(server.GetServerUrl()))
                .When(x => !string.IsNullOrWhiteSpace(x.Server))
                .WithMessage("El servidor tiene que ser como https://ts1.x1.international.travian.com");

            RuleFor(x => x.Accesses)
                .NotEmpty()
                .WithName("Access list");

            RuleFor(x => x.Username)
                .NotEmpty()
                .WithName("Nick name");
        }
    }
}