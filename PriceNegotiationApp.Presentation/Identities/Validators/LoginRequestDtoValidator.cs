using FluentValidation;
using PriceNegotiationApp.Contracts.Identities.Dtos.Requests;

namespace PriceNegotiationApp.Presentation.Identities.Validators
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
                .Matches("[a-z]").WithMessage("Password must contain at least 1 small letter.")
                .Matches("[A-Z]").WithMessage("Password must contain at least 1 big letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least 1 digit.");
        }
    }
}
