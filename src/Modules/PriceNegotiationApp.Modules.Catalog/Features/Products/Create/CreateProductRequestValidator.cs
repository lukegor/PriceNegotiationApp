using FluentValidation;

namespace PriceNegotiationApp.Modules.Catalog.Features.Products.Create;

// MA0182: used via DI assembly scanning (AddValidatorsFromAssemblyContaining), invisible to static analysis.
#pragma warning disable MA0182
internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
#pragma warning restore MA0182
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0m);
    }
}
