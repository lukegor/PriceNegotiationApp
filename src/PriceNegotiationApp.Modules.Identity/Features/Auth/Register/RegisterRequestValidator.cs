using FluentValidation;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth.Register;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
#pragma warning restore MA0182
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.")
            .Matches(@"[!@#$%^&*()\-_=+\[\]{}|;:'"",.<>?/\\]").WithMessage("Password must contain at least one special character.");
    }
}
