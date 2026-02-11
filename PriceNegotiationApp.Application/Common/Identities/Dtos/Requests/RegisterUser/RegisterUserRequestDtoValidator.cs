using FluentValidation;

namespace PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser
{
    public class RegisterUserRequestDtoValidator : AbstractValidator<RegisterUserRequestDto>
    {
        public RegisterUserRequestDtoValidator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty()
                .MaximumLength(50);

            RuleFor(x => x.Name)
                .NotEmpty();

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(12)
                .Matches("[a-z]").WithMessage("Hasło musi zawierać małą literę.")
                .Matches("[A-Z]").WithMessage("Hasło musi zawierać wielką literę.")
                .Matches("[0-9]").WithMessage("Hasło musi zawierać cyfrę.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password)
                .WithMessage("Hasła muszą być identyczne.");

            When(x => !string.IsNullOrEmpty(x.PostalCode), () =>
            {
                RuleFor(x => x.PostalCode).Matches(@"^\d{2}-\d{3}$");
            });
        }
    }
}
