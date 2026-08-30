using FluentValidation;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Create;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class CreateNegotiationRequestValidator : AbstractValidator<CreateNegotiationRequest>
#pragma warning restore MA0182
{
    public CreateNegotiationRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.ProposedPrice)
            .GreaterThan(0m);
    }
}
