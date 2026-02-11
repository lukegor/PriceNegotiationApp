using FluentValidation;
using PriceNegotiationApp.Application.Common;

namespace PriceNegotiationApp.Application.Negotiations.Dto.Requests.UpdateNegotiation
{
    public class UpdateNegotiationRequestDtoValidator : AbstractValidator<UpdateNegotiationRequestDto>
    {
        public UpdateNegotiationRequestDtoValidator()
        {
            RuleFor(x => x.ProposedPrice)
                .NotEmpty()
                .WithMessage("Initial price is required.")
                .GreaterThan(0)
                .PrecisionScale(Constants.StandardSqlPrecision, 2, false)
                .WithMessage("Initial price must be greater than 0.");
        }
    }
}
