using FluentValidation;
using PriceNegotiationApp.Application.Common;

namespace PriceNegotiationApp.Application.Negotiations.Dto.Requests.CreateNegotiation
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
