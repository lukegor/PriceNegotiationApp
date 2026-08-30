using FluentValidation;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth.Login;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
#pragma warning restore MA0182
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
