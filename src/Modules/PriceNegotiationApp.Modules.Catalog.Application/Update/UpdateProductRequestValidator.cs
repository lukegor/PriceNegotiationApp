using FluentValidation;

namespace PriceNegotiationApp.Modules.Catalog.Application.Update;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
#pragma warning restore MA0182
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0m);
    }
}
