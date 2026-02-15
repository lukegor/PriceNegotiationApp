using FluentValidation;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Requests;

namespace PriceNegotiationApp.Presentation.Negotiations.Validators
{
    public class CreateNegotiationRequestDtoValidator : AbstractValidator<CreateNegotiationRequestDto>
    {
        public CreateNegotiationRequestDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("ProductId is required.");

            RuleFor(x => x.ProposedPrice)
                .GreaterThan(0)
                .PrecisionScale(Constants.StandardSqlPrecision, 2, false)
                .WithMessage("InitialPrice must be greater than zero.");
        }
    }
}
