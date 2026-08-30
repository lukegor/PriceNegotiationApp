using FluentValidation;

namespace PriceNegotiationApp.Modules.Negotiations.Application.CounterPropose;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class CounterProposalRequestValidator : AbstractValidator<CounterProposalRequest>
#pragma warning restore MA0182
{
    public CounterProposalRequestValidator()
    {
        RuleFor(x => x.ProposedPrice)
            .GreaterThan(0m);
    }
}
