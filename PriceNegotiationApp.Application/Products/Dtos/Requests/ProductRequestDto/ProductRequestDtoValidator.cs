using FluentValidation;
using PriceNegotiationApp.Application.Common;

namespace PriceNegotiationApp.Application.Products.Dtos.Requests.ProductRequestDto
{
    public class ProductRequestDtoValidator : AbstractValidator<ProductRequestDto>
    {
        public ProductRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Product name cannot be empty.")
                .MaximumLength(200)
                .WithMessage("Product name cannot exceed 200 characters.");

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .PrecisionScale(Constants.StandardSqlPrecision, 2, false)
                .WithMessage("Product price must be greater than zero.");
        }
    }
}
