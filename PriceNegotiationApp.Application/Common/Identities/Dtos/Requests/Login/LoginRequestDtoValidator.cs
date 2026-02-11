using FluentValidation;

namespace PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.Login
{
    public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
    {
        public LoginRequestDtoValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .WithMessage("Username is required.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(12)
                .Matches("[a-z]").WithMessage("Hasło musi zawierać małą literę.")
                .Matches("[A-Z]").WithMessage("Hasło musi zawierać wielką literę.")
                .Matches("[0-9]").WithMessage("Hasło musi zawierać cyfrę.");
        }
    }
}
